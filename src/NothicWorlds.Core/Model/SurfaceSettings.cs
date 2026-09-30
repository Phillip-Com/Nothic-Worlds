namespace NothicWorlds.Core.Model;

/// <summary>What's drawn on a body's surface (VISION.md MAP-01, MAP-03, MAP-04).</summary>
public sealed class SurfaceSettings
{
    /// <summary>
    /// The default fill color: a pale ice white (matches <c>planet.gdshader</c>).
    /// </summary>
    public static readonly RgbColor DefaultFillColor = new(230, 237, 245);

    /// <summary>The map wrapped onto the surface, or null if there isn't one.</summary>
    public SurfaceMap? Map { get; set; }

    /// <summary>
    /// Color used where the map doesn't cover the globe (a flat map's polar caps, a polar map's
    /// southern hemisphere).
    /// </summary>
    public RgbColor FillColor { get; set; } = DefaultFillColor;
}
