namespace NothicWorlds.Core.Model;

/// <summary>What's drawn on a body's surface (VISION.md MAP-01, MAP-03, MAP-04).</summary>
public sealed class SurfaceSettings
{
    /// <summary>
    /// The default fill color: a pale ice white (matches <c>planet.gdshader</c>).
    /// </summary>
    public static readonly RgbColor DefaultFillColor = new(230, 237, 245);

    /// <summary>
    /// The most pieces a planet can have (owner: "up to a few dozen"). Pieces are drawn live, so
    /// each one adds a little rendering cost; the limit keeps the Base tier fast.
    /// </summary>
    public const int MaxPieces = 32;

    /// <summary>The map wrapped onto the surface, or null if there isn't one.</summary>
    public SurfaceMap? Map { get; set; }

    /// <summary>
    /// Pieces laid on top of the map (VISION.md MAP-02), bottom to top: later pieces cover
    /// earlier ones.
    /// </summary>
    public List<MapPiece> Pieces { get; } = [];

    /// <summary>
    /// Color used where the map doesn't cover the globe (a flat map's polar caps, a polar map's
    /// southern hemisphere).
    /// </summary>
    public RgbColor FillColor { get; set; } = DefaultFillColor;
}
