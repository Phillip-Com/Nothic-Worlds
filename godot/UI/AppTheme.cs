using Godot;

namespace NothicWorlds.UI;

/// <summary>
/// The look of every panel, menu, and dialog (VISION.md UI-07): buttons that look like buttons,
/// a clear color for the chosen mode, on/off switches drawn as switches, and section headers.
/// Set once on the window, so everything under it, dialogs included, picks it up; panels still
/// set their own backgrounds (<see cref="PanelStyle"/>).
/// </summary>
public static class AppTheme
{
    /// <summary>The theme variation for a section's heading (a Label).</summary>
    public const string SectionHeader = "SectionHeader";

    /// <summary>The color of the chosen mode, a switch that's on, and focus.</summary>
    public static readonly Color Accent = new(0.27f, 0.47f, 0.78f);

    // An on/off switch's size, in pixels.
    private const int SwitchWidth = 38;
    private const int SwitchHeight = 20;

    private static readonly Color _buttonColor = new(0.17f, 0.18f, 0.22f);
    private static readonly Color _buttonBorder = new(0.29f, 0.31f, 0.37f);
    private static readonly Color _hoverColor = new(0.23f, 0.25f, 0.30f);
    private static readonly Color _hoverBorder = new(0.40f, 0.43f, 0.51f);
    private static readonly Color _disabledColor = new(0.12f, 0.12f, 0.14f);
    private static readonly Color _disabledBorder = new(0.19f, 0.19f, 0.22f);
    private static readonly Color _disabledText = new(0.46f, 0.46f, 0.50f);
    private static readonly Color _headerText = new(0.66f, 0.78f, 0.96f);

    /// <summary>Builds the theme.</summary>
    public static Theme Create()
    {
        var theme = new Theme();
        foreach (string type in (string[])["Button", "OptionButton", "MenuButton"])
        {
            SetButtonLook(theme, type);
        }

        // The menus along the top stay light until pointed at, as menus usually are.
        // Its margins match the other looks' (and their border), so its text keeps its room
        // when it's pointed at or open.
        theme.SetStylebox("normal", "MenuButton", new StyleBoxEmpty
        {
            ContentMarginLeft = 11,
            ContentMarginRight = 11,
            ContentMarginTop = 5,
            ContentMarginBottom = 5,
        });

        SetSwitchLook(theme);
        theme.SetTypeVariation(SectionHeader, "Label");
        theme.SetFontSize("font_size", SectionHeader, 17);
        theme.SetColor("font_color", SectionHeader, _headerText);
        return theme;
    }

    // Buttons: a filled, outlined box; brighter when pointed at; the accent when pressed or
    // chosen (a mode's button); dim, with dim text, when it can't be used.
    private static void SetButtonLook(Theme theme, string type)
    {
        theme.SetStylebox("normal", type, Box(_buttonColor, _buttonBorder));
        theme.SetStylebox("hover", type, Box(_hoverColor, _hoverBorder));
        theme.SetStylebox("pressed", type, Box(Accent.Darkened(0.2f), Accent));
        theme.SetStylebox("hover_pressed", type, Box(Accent, Accent.Lightened(0.25f)));
        theme.SetStylebox("disabled", type, Box(_disabledColor, _disabledBorder));
        var focus = Box(Colors.Transparent, Accent);
        focus.DrawCenter = false;
        theme.SetStylebox("focus", type, focus);
        theme.SetColor("font_disabled_color", type, _disabledText);
        theme.SetColor("font_pressed_color", type, Colors.White);
        theme.SetColor("font_hover_pressed_color", type, Colors.White);
    }

    private static StyleBoxFlat Box(Color fill, Color border)
    {
        // Margins include the border, so every look of a button is the same size.
        var box = new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = border,
            ContentMarginLeft = 11,
            ContentMarginRight = 11,
            ContentMarginTop = 5,
            ContentMarginBottom = 5,
        };
        box.SetBorderWidthAll(1);
        box.SetCornerRadiusAll(4);
        return box;
    }

    // On/off switches (CheckButton): a pill with a knob, at the left and grey when off, at the
    // right and in the accent when on, so which they are reads at a glance.
    private static void SetSwitchLook(Theme theme)
    {
        theme.SetIcon("checked", "CheckButton", Switch(on: true, enabled: true));
        theme.SetIcon("unchecked", "CheckButton", Switch(on: false, enabled: true));
        theme.SetIcon("checked_disabled", "CheckButton", Switch(on: true, enabled: false));
        theme.SetIcon("unchecked_disabled", "CheckButton", Switch(on: false, enabled: false));
        theme.SetIcon("checked_mirrored", "CheckButton", Switch(on: true, enabled: true));
        theme.SetIcon("unchecked_mirrored", "CheckButton", Switch(on: false, enabled: true));
        theme.SetIcon("checked_disabled_mirrored", "CheckButton",
            Switch(on: true, enabled: false));
        theme.SetIcon("unchecked_disabled_mirrored", "CheckButton",
            Switch(on: false, enabled: false));
    }

    // A switch drawn smoothly: each pixel is covered by the pill (and the knob) by how far it
    // lies inside its edge.
    private static ImageTexture Switch(bool on, bool enabled)
    {
        float radius = SwitchHeight / 2f;
        Color track = on ? Accent : new Color(0.33f, 0.34f, 0.39f);
        Color knob = on ? Colors.White : new Color(0.80f, 0.81f, 0.84f);
        float alpha = enabled ? 1 : 0.4f;
        var knobCenter = new Vector2(on ? SwitchWidth - radius : radius, radius);
        var image = Image.CreateEmpty(SwitchWidth, SwitchHeight, false, Image.Format.Rgba8);
        for (int y = 0; y < SwitchHeight; y++)
        {
            for (int x = 0; x < SwitchWidth; x++)
            {
                var at = new Vector2(x + 0.5f, y + 0.5f);
                float pill = Coverage(PillDistance(at, SwitchWidth, SwitchHeight));
                float dot = Coverage(at.DistanceTo(knobCenter) - (radius - 3));
                Color color = track.Lerp(knob, dot);
                color.A = pill * alpha;
                image.SetPixel(x, y, color);
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    // How far a point is outside a pill (a rectangle with fully rounded ends) filling the
    // given size; negative inside.
    private static float PillDistance(Vector2 at, int width, int height)
    {
        float radius = height / 2f;
        float x = Mathf.Clamp(at.X, radius, width - radius);
        return at.DistanceTo(new Vector2(x, radius)) - radius;
    }

    // How much of a pixel lies inside an edge `distance` away (negative: inside).
    private static float Coverage(float distance) => Mathf.Clamp(0.5f - distance, 0, 1);
}
