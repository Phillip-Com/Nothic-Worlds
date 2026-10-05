namespace NothicWorlds.Rendering;

/// <summary>
/// How much of the screen's resolution the 3D view is drawn at, as a percentage, before it's
/// scaled up to fill it (VISION.md REN-03). Menus and text are always drawn at full resolution.
/// </summary>
public enum RenderScale
{
    /// <summary>Half: much faster on weak graphics, visibly softer.</summary>
    Half = 50,

    /// <summary>Three quarters: faster, a little softer.</summary>
    ThreeQuarters = 75,

    /// <summary>The full resolution.</summary>
    Full = 100,
}
