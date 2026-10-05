using Godot;
using NothicWorlds.Rendering;

namespace NothicWorlds.UI;

/// <summary>
/// File ▸ Settings (VISION.md REN-03; owner's choice: a settings window with presets): settings
/// for this computer, not a world. A Graphics Quality preset (Low, Standard, High) sets every
/// graphics option at once; each option can then be set on its own (the preset then reads
/// Custom). Changes apply straight away and are remembered.
/// </summary>
public partial class SettingsWindow : AcceptDialog
{
    private const float ContentWidth = 420;

    private Dropdown _quality = null!;
    private Dropdown _relief = null!;
    private Dropdown _clouds = null!;
    private Dropdown _antiAliasing = null!;
    private Dropdown _resolution = null!;
    private Dropdown _frameRate = null!;
    private CheckBox _highQualityMaps = null!;
    private bool _showing;

    /// <summary>The graphics options. Set it before adding the window to the tree.</summary>
    public GraphicsSettings Graphics { get; init; } = null!;

    public override void _Ready()
    {
        Title = "Settings";
        OkButtonText = "Close";
        var layout = new VBoxContainer { CustomMinimumSize = new Vector2(ContentWidth, 0) };
        layout.AddChild(new Label { Text = "Graphics", ThemeTypeVariation = "HeaderSmall" });
        var grid = new GridContainer { Columns = 2 };
        _quality = Row(grid, "Quality",
            "Sets all the options below at once. Low is lightest, for slower computers; High is " +
            "finest. Changing one option on its own makes this Custom.",
            [("Low", (int)GraphicsQuality.Low), ("Standard", (int)GraphicsQuality.Standard),
                ("High", (int)GraphicsQuality.High), ("Custom", (int)GraphicsQuality.Custom)]);
        _quality.SetItemDisabled(_quality.GetItemIndex((int)GraphicsQuality.Custom), true);
        _quality.ItemSelected += _ => ChooseQuality();
        _relief = Row(grid, "Relief detail",
            "How finely sculpted mountains and basins are drawn: sharper outlines cost more",
            [("Low", (int)ReliefDetail.Low), ("Standard", (int)ReliefDetail.Standard),
                ("High", (int)ReliefDetail.High)]);
        _clouds = Row(grid, "Cloud detail",
            "How finely live weather's clouds are drawn: High adds ragged edges",
            [("Low", (int)CloudDetail.Low), ("High", (int)CloudDetail.High)]);
        _antiAliasing = Row(grid, "Smooth edges",
            "Smooths the jagged edges of globes, rings, and lines. 4× is the cleanest and uses " +
            "more video memory",
            [("Off", (int)AntiAliasing.Off), ("Quick (FXAA)", (int)AntiAliasing.Fxaa),
                ("Finest (4× MSAA)", (int)AntiAliasing.Msaa4X)]);
        _resolution = Row(grid, "3D resolution",
            "Draws the 3D view at less than the screen's resolution and scales it up: faster, " +
            "softer. Menus and text stay sharp",
            [("100%", (int)RenderScale.Full), ("75%", (int)RenderScale.ThreeQuarters),
                ("50%", (int)RenderScale.Half)]);
        _frameRate = Row(grid, "Frame rate",
            "The most frames a second: fewer saves battery and heat on a laptop",
            [("Match the screen", (int)FrameRateLimit.MatchScreen),
                ("60 a second", (int)FrameRateLimit.Sixty),
                ("30 a second", (int)FrameRateLimit.Thirty)]);
        Dropdown[] options = [_relief, _clouds, _antiAliasing, _resolution, _frameRate];
        foreach (Dropdown option in options)
        {
            option.ItemSelected += _ => ChooseOptions();
        }

        layout.AddChild(grid);
        layout.AddChild(Note(
            $"This computer's graphics: {RenderingServer.GetVideoAdapterName().Trim()} " +
            $"(starts on {GraphicsSettings.DefaultQuality})."));

        // The Advanced tier (owner's choice): heavier options, off unless chosen, and in no
        // preset.
        layout.AddChild(new HSeparator());
        layout.AddChild(new Label { Text = "Advanced", ThemeTypeVariation = "HeaderSmall" });
        _highQualityMaps = new CheckBox
        {
            Text = "High-quality maps",
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Keeps imported maps uncompressed, for perfectly crisp detail",
        };
        _highQualityMaps.Toggled += on =>
        {
            if (!_showing)
            {
                Graphics.HighQualityMaps = on;
            }
        };
        layout.AddChild(_highQualityMaps);
        layout.AddChild(Note(
            "Uses about 3½ times the video memory for maps (an 8k map: about 500 MB instead " +
            "of 140 MB). For computers with plenty of graphics memory."));
        AddChild(layout);
        AboutToPopup += Refresh;
    }

    /// <summary>Shows the settings in effect.</summary>
    public void Refresh()
    {
        _showing = true;
        GraphicsOptions options = Graphics.Options;
        Pick(_quality, (int)options.Quality);
        Pick(_relief, (int)options.ReliefDetail);
        Pick(_clouds, (int)options.CloudDetail);
        Pick(_antiAliasing, (int)options.AntiAliasing);
        Pick(_resolution, (int)options.RenderScale);
        Pick(_frameRate, (int)options.FrameRateLimit);
        _highQualityMaps.SetPressedNoSignal(Graphics.HighQualityMaps);
        _showing = false;
    }

    // A preset sets every option.
    private void ChooseQuality()
    {
        if (_showing)
        {
            return;
        }

        Graphics.Change(GraphicsOptions.For((GraphicsQuality)_quality.GetSelectedId()));
        Refresh();
    }

    // One option set on its own (the preset shown follows: Custom unless they match one).
    private void ChooseOptions()
    {
        if (_showing)
        {
            return;
        }

        Graphics.Change(new GraphicsOptions(
            (ReliefDetail)_relief.GetSelectedId(),
            (CloudDetail)_clouds.GetSelectedId(),
            (AntiAliasing)_antiAliasing.GetSelectedId(),
            (RenderScale)_resolution.GetSelectedId(),
            (FrameRateLimit)_frameRate.GetSelectedId()));
        Refresh();
    }

    private static Dropdown Row(GridContainer grid, string name, string tip,
        (string Text, int Id)[] items)
    {
        grid.AddChild(new Label
        {
            Text = name,
            TooltipText = tip,
            MouseFilter = Control.MouseFilterEnum.Pass,  // So its tooltip shows
        });
        var dropdown = new Dropdown
        {
            TooltipText = tip,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        foreach ((string text, int id) in items)
        {
            dropdown.AddItem(text, id);
        }

        grid.AddChild(dropdown);
        return dropdown;
    }

    private static Label Note(string text) => new()
    {
        Text = text,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        CustomMinimumSize = new Vector2(ContentWidth, 0),
        Modulate = new Color(1, 1, 1, 0.6f),
    };

    private static void Pick(Dropdown dropdown, int id) =>
        dropdown.Select(dropdown.GetItemIndex(id));
}
