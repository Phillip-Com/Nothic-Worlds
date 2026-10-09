using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// The fine relief of a body's painted terrain up close (VISION.md BOD-12): each type's
/// <see cref="TerrainRoughness"/>, blended between neighboring cells' types like the heights
/// between them, so the relief carries on across the line between two types without a step.
/// Only where the terrain shapes the ground (VISION.md BOD-07). Immutable, so it's safe to use
/// from several threads at once.
/// </summary>
public sealed class RoughGround
{
    private const int FaceSize = TerrainGrid.FaceSize;

    private readonly TerrainGrid _terrain;
    private readonly double _radiusKm;
    private readonly int _seed;
    private readonly double _belowKm;
    private readonly double _fewestKm;

    // Each code's variation, feature size, and roughness (0 roughness for codes with no type,
    // unpainted, or no variation: no relief).
    private readonly int[] _variation = new int[256];
    private readonly double[] _sizeKm = new double[256];
    private readonly double[] _roughness = new double[256];

    private RoughGround(TerrainGrid terrain, IReadOnlyList<TerrainType> types, double radiusKm,
        int seed)
    {
        (_terrain, _radiusKm, _seed) = (terrain, radiusKm, seed);

        // As TerrainRelief makes the type's own features: no smaller than three cells, and
        // none drawn smaller than four.
        double cellKm = radiusKm * Math.PI / 2 / FaceSize;
        (_belowKm, _fewestKm) = (3 * cellKm, 4 * cellKm);
        foreach (TerrainType type in types)
        {
            _variation[type.Code] = Math.Clamp(type.VariationMeters, 0,
                TerrainType.MaxVariationMeters);
            _sizeKm[type.Code] = type.FeatureSizeKm;
            _roughness[type.Code] = _variation[type.Code] > 0
                ? Math.Clamp(type.Roughness, 0, 1)
                : 0;
        }
    }

    /// <summary>
    /// The fine relief of <paramref name="terrain"/> painted with <paramref name="types"/> on
    /// a body <paramref name="radiusKm"/> in radius with seed <paramref name="seed"/>
    /// (<see cref="TerrainRelief.SeedFor"/>), or null if nothing is painted or no type is
    /// rough. Quick: nothing is worked out until it's asked for.
    /// </summary>
    public static RoughGround? For(TerrainGrid terrain, IReadOnlyList<TerrainType> types,
        double radiusKm, int seed)
    {
        bool rough = !terrain.IsEmpty
            && types.Any(type => type.Roughness > 0 && type.VariationMeters > 0);
        return rough ? new RoughGround(terrain, types, radiusKm, seed) : null;
    }

    /// <summary>
    /// How far the fine relief lifts (positive) or sinks the ground at a unit direction, in
    /// meters, with no features smaller than <paramref name="smallestMeters"/> (see
    /// <see cref="TerrainRoughness.Offset"/>). <paramref name="riverMeters"/> gives how far a
    /// place is past the nearest river's banks (null: no rivers), asked only where the ground is
    /// rough.
    /// </summary>
    public double OffsetAt(Vector3D direction, double smallestMeters,
        Func<Vector3D, double>? riverMeters = null)
    {
        // Ground drawn too coarsely for any of the features has none (most of it, far off).
        if (smallestMeters >= _belowKm * 1000)
        {
            return 0;
        }

        // The four cells whose middles surround the place, as HeightGrid.SampleSteepAt finds
        // them, each weighted by how near it is.
        CubeCell cell = CubeSphere.CellAt(direction, FaceSize);
        (double across, double down) = CubeSphere.FacePosition(cell.Face, direction);
        double x = Math.Clamp(across * FaceSize - 0.5, 0, FaceSize - 1);
        double y = Math.Clamp(down * FaceSize - 0.5, 0, FaceSize - 1);
        int left = Math.Min((int)x, FaceSize - 2);
        int top = Math.Min((int)y, FaceSize - 2);
        double fx = x - left, fy = y - top;
        Span<byte> codes = [
            Code(cell.Face, left, top), Code(cell.Face, left + 1, top),
            Code(cell.Face, left, top + 1), Code(cell.Face, left + 1, top + 1)];
        Span<double> weights = [
            (1 - fx) * (1 - fy), fx * (1 - fy), (1 - fx) * fy, fx * fy];

        // Each type's relief is worked out once, however many of the cells it's in; the rivers
        // are looked for only once some is.
        double total = 0, river = double.NaN;
        for (int i = 0; i < 4; i++)
        {
            byte code = codes[i];
            if (_roughness[code] == 0 || codes[..i].Contains(code))
            {
                continue;
            }

            double weight = 0;
            for (int j = i; j < 4; j++)
            {
                weight += codes[j] == code ? weights[j] : 0;
            }

            if (weight > 0)
            {
                if (double.IsNaN(river))
                {
                    river = riverMeters?.Invoke(direction) ?? double.PositiveInfinity;
                }

                total += weight * TerrainRoughness.Offset(direction, _radiusKm,
                    _variation[code], Math.Max(_sizeKm[code], _fewestKm), _belowKm,
                    _roughness[code], smallestMeters, _seed + code * 7_919, river);
            }
        }

        return total;
    }

    private byte Code(int face, int column, int row) =>
        _terrain.CodeAt(new CubeCell(face, column, row));
}
