using Godot;

namespace NothicWorlds.UI;

/// <summary>The look shared by the side panels (System, Map, Terrain, Journal, Regions).</summary>
public static class PanelStyle
{
    // The panel's own margins, top and bottom together (see SidePanel).
    private const int VerticalMargins = 16;

    /// <summary>
    /// Makes a side panel as tall as what's in it, from <paramref name="top"/> pixels down,
    /// rather than always reaching the bottom of the window (VISION.md UI-07), but never past
    /// <paramref name="bottomMargin"/> pixels from it: then it scrolls. Kept up to date as its
    /// contents and the window change size.
    /// </summary>
    public static void FitHeight(Control panel, Control content, int top, int bottomMargin)
    {
        panel.AnchorBottom = 0;
        void Fit()
        {
            if (!panel.IsInsideTree())
            {
                return;
            }

            float room = panel.GetViewportRect().Size.Y - top - bottomMargin;
            float wanted = content.GetCombinedMinimumSize().Y + VerticalMargins;
            panel.OffsetTop = top;
            panel.OffsetBottom = top + Math.Max(Math.Min(wanted, room), 0);
        }

        content.MinimumSizeChanged += Fit;
        panel.GetViewport().SizeChanged += Fit;
        Callable.From(Fit).CallDeferred();
    }

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
