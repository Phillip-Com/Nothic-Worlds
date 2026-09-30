namespace NothicWorlds.Controls;

/// <summary>How panning behaves. The camera switches automatically based on zoom.</summary>
public enum PanMode
{
    /// <summary>Zoomed in close: panning slides across the planet's surface.</summary>
    Surface,

    /// <summary>
    /// Zoomed out: panning slides the whole view, moving the planet across the screen.
    /// </summary>
    View,
}
