using Godot;
using NothicWorlds.Core.Geometry;

namespace NothicWorlds.UI;

/// <summary>
/// The "Preparing the area…" screen over the first-person view while the area round the eye
/// loads (VISION.md REN-06; owner's choice, 2026-10-12): a progress bar, what it's waiting on,
/// and how to go back, so the ground isn't seen popping in. It fades out once the area is in.
/// It only shows what it's given; <see cref="Controls.FirstPersonMode"/> works it out.
/// </summary>
public partial class AreaLoadingScreen : CanvasLayer
{
    private const double FadeSeconds = 0.4;

    private readonly Control _cover;
    private readonly ProgressBar _bar;
    private readonly Label _stage;
    private Tween? _fade;

    /// <summary>Makes the screen, hidden until begun.</summary>
    public AreaLoadingScreen()
    {
        Layer = 10;  // Over the first-person view's overlay (5) and the panels kept shown
        Visible = false;

        // It takes the mouse, so nothing under it can be clicked unseen.
        _cover = new ColorRect
        {
            Color = new Color(0.04f, 0.05f, 0.07f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _cover.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_cover);

        var middle = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        middle.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _cover.AddChild(middle);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
        column.AddThemeConstantOverride("separation", 10);
        middle.AddChild(column);

        var title = new Label
        {
            Text = "Preparing the area…",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 24);
        column.AddChild(title);
        _bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0,
            CustomMinimumSize = new Vector2(0, 22),
        };
        column.AddChild(_bar);
        _stage = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        column.AddChild(_stage);
        var hint = new Label
        {
            Text = "Esc goes back to the map",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1, 1, 1, 0.6f),
        };
        column.AddChild(hint);
    }

    /// <summary>
    /// Whether it covers the view (shown and not fading out): the view takes no input but Esc.
    /// </summary>
    public bool Covers { get; private set; }

    /// <summary>Shows the screen, with nothing done yet.</summary>
    public void Begin()
    {
        _fade?.Kill();
        _fade = null;
        _cover.Modulate = Colors.White;
        _bar.Value = 0;
        _stage.Text = StageText(AreaLoadingStage.Roads);
        Visible = true;
        Covers = true;
    }

    /// <summary>
    /// Shows how far <paramref name="loading"/> has got, and fades out once it's ready.
    /// </summary>
    public void Show(AreaLoading loading)
    {
        if (!Covers)
        {
            return;
        }

        _bar.Value = loading.Done;
        _stage.Text = StageText(loading.Stage);
        if (loading.IsReady)
        {
            Covers = false;
            _fade = CreateTween();
            _fade.TweenProperty(_cover, "modulate:a", 0.0, FadeSeconds);
            _fade.TweenCallback(Callable.From(() => Visible = false));
        }
    }

    /// <summary>Hides the screen at once (on leaving).</summary>
    public void End()
    {
        _fade?.Kill();
        _fade = null;
        Visible = false;
        Covers = false;
    }

    // What the screen says it's waiting on.
    private static string StageText(AreaLoadingStage stage) => stage switch
    {
        AreaLoadingStage.Roads => "Laying the roads…",
        AreaLoadingStage.Ground => "Building the ground around you…",
        AreaLoadingStage.Rivers => "Drawing the rivers and roads…",
        AreaLoadingStage.Plants => "Placing the plants…",
        _ => "Ready",
    };
}
