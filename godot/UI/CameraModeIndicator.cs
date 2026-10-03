using Godot;
using NothicWorlds.Controls;

namespace NothicWorlds.UI;

/// <summary>
/// Bottom-left readout of what the camera is doing. It always shows the current pan mode
/// (Surface or View, which switches with zoom). While the user is moving the camera, it also
/// shows "Orbiting" or "Panning" in bright text.
/// </summary>
public partial class CameraModeIndicator : CanvasLayer
{
    private const int ScreenMargin = 12;

    private static readonly Color _activeColor = new(1.0f, 0.85f, 0.4f);
    private static readonly Color _mutedColor = new(0.75f, 0.78f, 0.85f, 0.8f);

    private Label _actionLabel = null!;
    private Label _modeLabel = null!;
    private CameraAction? _shownAction;
    private string? _shownMode;

    /// <summary>The camera to report on.</summary>
    [Export] public PlanetCamera? Camera { get; set; }

    public override void _Ready()
    {
        var box = new VBoxContainer();
        box.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        box.GrowVertical = Control.GrowDirection.Begin;
        box.Position = new Vector2(ScreenMargin, -ScreenMargin);
        AddChild(box);

        _actionLabel = CreateLabel(_activeColor);
        _modeLabel = CreateLabel(_mutedColor);
        box.AddChild(_actionLabel);
        box.AddChild(_modeLabel);

        if (Camera is null)
        {
            GD.PushError("CameraModeIndicator has no camera assigned.");
            Visible = false;
        }
    }

    public override void _Process(double delta)
    {
        if (Camera is null)
        {
            return;
        }

        // Only touch the labels when something changed.
        CameraAction action = Camera.CurrentAction;
        if (action != _shownAction)
        {
            _shownAction = action;
            _actionLabel.Text = action switch
            {
                CameraAction.Orbiting => "Orbiting",
                CameraAction.Panning => "Panning",
                _ => "",
            };
            _actionLabel.Visible = action != CameraAction.None;
        }

        string mode = Camera.IsLocalView
            ? "Local view: the ground, north up  (zoom out for the globe)"
            : Camera.PanMode == PanMode.Surface
                ? "Pan: Surface  (zoom out to slide the view)"
                : "Pan: View  (zoom in to pan across the surface)";
        if (mode != _shownMode)
        {
            _shownMode = mode;
            _modeLabel.Text = mode;
        }
    }

    private static Label CreateLabel(Color color)
    {
        var label = new Label();
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_shadow_color", Colors.Black);
        return label;
    }
}
