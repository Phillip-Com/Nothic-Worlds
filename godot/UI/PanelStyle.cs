using Godot;

namespace NothicWorlds.UI;

/// <summary>The look shared by the side panels (System, Map, Terrain, Journal, Regions).</summary>
public static class PanelStyle
{
    /// <summary>
    /// A solid dark background with an 8-pixel margin. Solid, so the markers and labels drawn
    /// over the globe (beneath the panels) don't show through.
    /// </summary>
    public static StyleBoxFlat SidePanel() => new()
    {
        BgColor = new Color(0.09f, 0.09f, 0.11f),
        ContentMarginLeft = 8,
        ContentMarginRight = 8,
        ContentMarginTop = 8,
        ContentMarginBottom = 8,
    };
}
