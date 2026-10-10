using Godot;

namespace NothicWorlds.UI;

/// <summary>
/// What's shown over the first-person view (VISION.md REN-06; owner's choice: a compass, readouts
/// on hover, and a panel for the spot): the controls along the top left, a compass along the
/// top with pinned places on it, a rangefinder (a crosshair in the middle with the distance to
/// the ground there under it; the distance under the mouse goes in the hover readout), the
/// name and place of the body under the mouse, the time and weather at the spot in
/// the bottom left, the rain or snow falling over everything, and (REN-08) the minimap, a
/// Calendar button, and switches for fog, clouds, and night vision, in the top right. It only
/// shows what it's given; <see cref="Controls.FirstPersonMode"/> works it out.
/// </summary>
public partial class FirstPersonHud : CanvasLayer
{
    private static readonly Shader _fallingShader =
        GD.Load<Shader>("res://Rendering/falling_weather.gdshader");

    private readonly ColorRect _falling;
    private readonly ShaderMaterial _fallingMaterial = new() { Shader = _fallingShader };
    private readonly Label _help;
    private readonly CompassStrip _compass;
    private readonly Label _hover;
    private readonly Label _range;
    private readonly Label _info;

    /// <summary>Makes the overlay, hidden until shown.</summary>
    public FirstPersonHud()
    {
        Layer = 5;
        Visible = false;

        _falling = new ColorRect
        {
            Material = _fallingMaterial,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        _falling.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_falling);

        _help = Outlined(new Label { Position = new Vector2(12, 12) });
        _help.Modulate = new Color(1, 1, 1, 0.85f);
        AddChild(_help);

        _compass = new CompassStrip
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            OffsetLeft = -280,
            OffsetRight = 280,
            OffsetTop = 64,
            OffsetBottom = 134,
        };
        AddChild(_compass);

        // The rangefinder's crosshair, and what it reads under it.
        var crosshair = new Control
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        crosshair.Draw += () => DrawCrosshair(crosshair);
        AddChild(crosshair);
        _range = Outlined(new Label
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -150,
            OffsetRight = 150,
            OffsetTop = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        _range.Modulate = new Color(1, 1, 1, 0.85f);
        AddChild(_range);

        _hover = Outlined(new Label { Visible = false });
        AddChild(_hover);

        _info = Outlined(new Label
        {
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetLeft = 12,
            OffsetTop = -12,
            OffsetBottom = -12,
            GrowVertical = Control.GrowDirection.Begin,
        });
        AddChild(_info);

        // The minimap and the calendar's button, in the top right (VISION.md REN-08).
        var corner = new VBoxContainer
        {
            AnchorLeft = 1,
            AnchorRight = 1,
            OffsetLeft = -Minimap.Diameter - 16,
            OffsetRight = -16,
            OffsetTop = 16,
        };
        Minimap = new Minimap();
        corner.AddChild(Minimap);
        CalendarButton = new Button
        {
            Text = "Calendar (T)",
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Open or close the calendar: click a day to run the clock there",
        };
        corner.AddChild(CalendarButton);
        FogSwitch = Switch("Fog (G)", "Show or hide the haze that fades the distance");
        corner.AddChild(FogSwitch);
        CloudsSwitch = Switch("Clouds (K)",
            "Show or hide the clouds, here and on the globe (as View ▸ Clouds)");
        corner.AddChild(CloudsSwitch);
        TerrainSwitch = Switch("Terrain (R)",
            "Show or hide the painted terrain colors on the ground here (the map keeps its own)");
        corner.AddChild(TerrainSwitch);
        NightVisionSwitch = Switch("Night Vision (N)",
            "Light up the night (and dim places) as if at dusk, to see the ground");
        corner.AddChild(NightVisionSwitch);
        AddChild(corner);
    }

    /// <summary>Shows or hides the fog (haze) while standing.</summary>
    public Button FogSwitch { get; }

    /// <summary>Shows or hides the clouds.</summary>
    public Button CloudsSwitch { get; }

    /// <summary>Shows or hides the painted terrain on the ground stood on.</summary>
    public Button TerrainSwitch { get; }

    /// <summary>Turns night vision on or off.</summary>
    public Button NightVisionSwitch { get; }

    /// <summary>The minimap in the top right.</summary>
    public Minimap Minimap { get; }

    /// <summary>Opens or closes the calendar over the view.</summary>
    public Button CalendarButton { get; }

    /// <summary>The controls and what mode the view is in, along the top left.</summary>
    public void SetHelp(string text) => _help.Text = text;

    /// <summary>The way the view faces, in degrees clockwise from north.</summary>
    public void SetHeading(double degrees) => _compass.HeadingDegrees = (float)degrees;

    /// <summary>
    /// The pinned places to mark on the compass, nearest first: each one's bearing in degrees
    /// clockwise from north, color, and label.
    /// </summary>
    public void SetPins(IEnumerable<(double BearingDegrees, Color Color, string Label)> pins) =>
        _compass.SetPins(pins);

    /// <summary>What the rangefinder reads at the view's middle, under the crosshair.</summary>
    public void SetRange(string text) => _range.Text = text;

    /// <summary>
    /// The readout for the body under the mouse at <paramref name="mouse"/>, or none.
    /// </summary>
    public void SetHover(string? text, Vector2 mouse)
    {
        _hover.Visible = text is not null;
        if (text is not null)
        {
            _hover.Text = text;
            _hover.Position = mouse + new Vector2(16, 12);
        }
    }

    /// <summary>The time and weather at the spot, in the bottom left.</summary>
    public void SetInfo(string text) => _info.Text = text;

    /// <summary>
    /// Rain and snow falling, each 0 (none) to 1 (as hard as it gets), drifting
    /// <paramref name="slant"/> sideways for each unit of fall, lit by
    /// <paramref name="light"/> (1 by day).
    /// </summary>
    public void SetFalling(double rain, double snow, double slant, double light)
    {
        _falling.Visible = rain > 0 || snow > 0;
        _fallingMaterial.SetShaderParameter("rain", (float)rain);
        _fallingMaterial.SetShaderParameter("snow", (float)snow);
        _fallingMaterial.SetShaderParameter("slant", (float)slant);
        _fallingMaterial.SetShaderParameter("light", (float)light);
    }

    // A small cross at the middle of the view, in white outlined in black, open in the
    // middle so the spot it measures isn't hidden.
    private static void DrawCrosshair(Control at)
    {
        foreach ((Color color, float width) in new[] { (Colors.Black, 3f), (Colors.White, 1f) })
        {
            foreach (Vector2 way in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
            {
                at.DrawLine(way * 3, way * 9, color, width);
            }
        }
    }

    // White text outlined in black, which reads against any sky or ground.
    private static Label Outlined(Label label)
    {
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 4);
        return label;
    }

    // An on/off button for the corner, its pressed look showing it's on.
    private static Button Switch(string text, string tooltip) => new()
    {
        Text = text,
        ToggleMode = true,
        FocusMode = Control.FocusModeEnum.None,
        TooltipText = tooltip,
    };
}
