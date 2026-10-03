using NothicWorlds.Core.Maps;

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

    /// <summary>
    /// The terrain painted on the surface (VISION.md BOD-05). Immutable, so copies of the world
    /// and undo snapshots can safely share it.
    /// </summary>
    public TerrainGrid Terrain { get; set; } = TerrainGrid.Empty;

    /// <summary>Returns an independent copy (e.g. a snapshot for undo).</summary>
    public SurfaceSettings Clone()
    {
        var copy = new SurfaceSettings();
        copy.RestoreFrom(this);
        return copy;
    }

    /// <summary>
    /// Makes these settings an independent copy of <paramref name="source"/> (e.g. to undo an
    /// edit). Nothing is shared with the source except immutable parts.
    /// </summary>
    public void RestoreFrom(SurfaceSettings source)
    {
        FillColor = source.FillColor;
        Terrain = source.Terrain;  // Immutable, safe to share.
        Map = source.Map is SurfaceMap map
            ? new SurfaceMap
            {
                AssetName = map.AssetName,
                Projection = map.Projection,
                Calibration = map.Calibration,  // Immutable, safe to share.
            }
            : null;
        Pieces.Clear();
        Pieces.AddRange(source.Pieces.Select(piece => piece.Clone()));
    }

    /// <summary>
    /// True if <paramref name="other"/> would save exactly the same surface: the same map, map
    /// type, calibration, fill color, pieces (in the same order, with the same names, cuts,
    /// and placement), and painted terrain. Used to tell whether the world still matches its
    /// saved file.
    /// </summary>
    public bool HasSameContent(SurfaceSettings other)
    {
        return FillColor == other.FillColor
            && Terrain.HasSameCells(other.Terrain)
            && SameMap(Map, other.Map)
            && Pieces.Count == other.Pieces.Count
            && Pieces.Zip(other.Pieces).All(pair => SamePiece(pair.First, pair.Second));
    }

    private static bool SameMap(SurfaceMap? a, SurfaceMap? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        return a.AssetName == b.AssetName
            && a.Projection == b.Projection
            && SameCalibration(a.Calibration, b.Calibration);
    }

    private static bool SameCalibration(MapCalibration? a, MapCalibration? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;  // Immutable, so usually shared between copies.
        }

        return a is not null && b is not null
            && a.Latitudes.SequenceEqual(b.Latitudes)
            && a.Longitudes.SequenceEqual(b.Longitudes);
    }

    private static bool SameWarp(IReadOnlyList<ImagePoint>? a, IReadOnlyList<ImagePoint>? b)
    {
        return ReferenceEquals(a, b) || (a is not null && b is not null && a.SequenceEqual(b));
    }

    private static bool SamePiece(MapPiece a, MapPiece b)
    {
        return a.Id == b.Id
            && a.Name == b.Name
            && a.AssetName == b.AssetName
            && a.Center == b.Center
            && a.RotationDegrees == b.RotationDegrees
            && a.WidthDegrees == b.WidthDegrees
            && SameWarp(a.WarpedPoints, b.WarpedPoints)
            && (ReferenceEquals(a.Outline, b.Outline)
                || (a.Outline.SourceAspectRatio == b.Outline.SourceAspectRatio
                    && a.Outline.Points.SequenceEqual(b.Outline.Points)));
    }
}
