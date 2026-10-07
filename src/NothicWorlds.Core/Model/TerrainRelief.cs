using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// The ground that painted terrain shapes (VISION.md BOD-07; owner's choices): each terrain
/// type sets the ground to its own height, and where two types meet the heights merge over a
/// width set by the steeper of their two edges, from long, gentle slopes to a sheer cliff.
/// Sculpting adds on top (<see cref="Shaped"/>), so changing a type's height re-shapes all the
/// ground painted with it and keeps what was sculpted. Unpainted ground is at height 0, with a
/// gentle edge.
/// </summary>
/// <remarks>
/// <para>Worked out a tile at a time from the codes around it: for each cell, how far it is
/// from the nearest cell of another type (across its own type's ground) and which type that
/// is. On its side of the line between them, the ground runs from halfway between the two
/// heights, at the line, to its own height, over half the transition's width. Where three
/// types meet, the nearest other one decides.</para>
/// <para>A paint stroke only re-works the tiles near it (<see cref="Update"/>), so painting
/// stays quick.</para>
/// </remarks>
public static class TerrainRelief
{
    /// <summary>
    /// How wide, in cells, the gentlest transition between two types is (about 240 km on an
    /// Earth-sized world). A cliff's is 0.
    /// </summary>
    public const int GentlestWidthCells = 24;

    private const int TileSize = CubeGridBrush.TileSize;
    private const int TilesPerSide = CubeGridBrush.TilesPerSide;
    private const int FaceSize = CubeGridBrush.FaceSize;

    // How many cells around a tile are read to find each cell's nearest other type: half the
    // widest transition, and one more.
    private const int Pad = GentlestWidthCells / 2 + 1;
    private const int Padded = TileSize + 2 * Pad;
    private const float Diagonal = 1.41421356f;

    private static readonly (int Dx, int Dy)[] _sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    // Each tile's neighbors within reach of its padding (worked out once: the same for every
    // world).
    private static readonly Lazy<int[][]> _near = new(() =>
        [.. Enumerable.Range(0, CubeGridBrush.TileCount)
            .Select(index => TilesNear(index).ToArray())]);

    /// <summary>The ground the painted terrain shapes, everywhere on a body.</summary>
    public static HeightGrid BaseHeights(TerrainGrid terrain, IReadOnlyList<TerrainType> types)
    {
        var look = new Lookup(types);
        var tiles = new short[]?[CubeGridBrush.TileCount];
        for (int index = 0; index < tiles.Length; index++)
        {
            tiles[index] = WorkTile(terrain, look, index);
        }

        return HeightGrid.FromTiles(tiles);
    }

    /// <summary>
    /// The ground <paramref name="after"/> shapes, worked out from <paramref name="previous"/>
    /// (what <paramref name="before"/> shaped, with the same types) by re-working only the
    /// tiles near those whose painting changed.
    /// </summary>
    public static HeightGrid Update(HeightGrid previous, TerrainGrid before, TerrainGrid after,
        IReadOnlyList<TerrainType> types)
    {
        var changed = new HashSet<int>();
        for (int index = 0; index < CubeGridBrush.TileCount; index++)
        {
            if (!ReferenceEquals(before.Tile(index), after.Tile(index)))
            {
                changed.UnionWith(_near.Value[index]);
            }
        }

        if (changed.Count == 0)
        {
            return previous;
        }

        var look = new Lookup(types);
        var tiles = new short[]?[CubeGridBrush.TileCount];
        for (int index = 0; index < tiles.Length; index++)
        {
            tiles[index] = changed.Contains(index)
                ? WorkTile(after, look, index)
                : previous.Tile(index);
        }

        return HeightGrid.FromTiles(tiles);
    }

    /// <summary>
    /// The ground <paramref name="terrain"/> shapes after its types' heights or edges changed
    /// from <paramref name="before"/> to <paramref name="after"/>, worked out from
    /// <paramref name="previous"/> (what it shaped with <paramref name="before"/>) by
    /// re-working only the tiles near ground painted with a type that changed.
    /// </summary>
    public static HeightGrid Rework(HeightGrid previous, TerrainGrid terrain,
        IReadOnlyList<TerrainType> before, IReadOnlyList<TerrainType> after)
    {
        Lookup was = new(before), now = new(after);
        var changed = new bool[256];
        bool any = false;
        for (int code = 1; code < 256; code++)
        {
            changed[code] = was.Height[code] != now.Height[code]
                || was.Edge[code] != now.Edge[code];
            any |= changed[code];
        }

        if (!any)
        {
            return previous;
        }

        var affected = new HashSet<int>();
        for (int index = 0; index < CubeGridBrush.TileCount; index++)
        {
            if (terrain.Tile(index) is byte[] codes && Array.Exists(codes, code => changed[code]))
            {
                affected.UnionWith(_near.Value[index]);
            }
        }

        var tiles = new short[]?[CubeGridBrush.TileCount];
        for (int index = 0; index < tiles.Length; index++)
        {
            tiles[index] = affected.Contains(index)
                ? WorkTile(terrain, now, index)
                : previous.Tile(index);
        }

        return HeightGrid.FromTiles(tiles);
    }

    /// <summary>
    /// The ground seen: the terrain's heights with the sculpting on top (each cell kept within
    /// what a height can hold). Tiles with nothing on one side are shared, not copied; and
    /// given what was shaped <paramref name="previously"/>, so are tiles where neither side
    /// changed, so the globe only redraws what did.
    /// </summary>
    public static HeightGrid Shaped(HeightGrid baseHeights, HeightGrid sculpted,
        (HeightGrid Base, HeightGrid Sculpted, HeightGrid Shaped)? previously = null)
    {
        if (baseHeights.IsEmpty)
        {
            return sculpted;
        }

        if (sculpted.IsEmpty)
        {
            return baseHeights;
        }

        var tiles = new short[]?[CubeGridBrush.TileCount];
        for (int index = 0; index < tiles.Length; index++)
        {
            short[]? under = baseHeights.Tile(index), over = sculpted.Tile(index);
            if (under is null || over is null)
            {
                tiles[index] = under ?? over;
                continue;
            }

            if (previously is var (oldBase, oldSculpted, oldShaped)
                && ReferenceEquals(under, oldBase.Tile(index))
                && ReferenceEquals(over, oldSculpted.Tile(index)))
            {
                tiles[index] = oldShaped.Tile(index);
                continue;
            }

            var sum = new short[under.Length];
            for (int cell = 0; cell < sum.Length; cell++)
            {
                sum[cell] = (short)Math.Clamp(under[cell] + over[cell],
                    HeightGrid.MinHeightMeters, HeightGrid.MaxHeightMeters);
            }

            tiles[index] = sum;
        }

        return HeightGrid.FromTiles(tiles);
    }

    /// <summary>
    /// How wide, in cells, the transition is where types with these edges meet (the steeper
    /// edge wins): <see cref="GentlestWidthCells"/> for two gentle ones, 0 for a cliff.
    /// </summary>
    public static double TransitionCells(double edge, double otherEdge)
    {
        double steeper = Math.Clamp(Math.Max(edge, otherEdge), 0, 1);
        return GentlestWidthCells * (1 - steeper) * (1 - steeper);
    }

    // One tile's heights, or null if they're all 0.
    private static short[]? WorkTile(TerrainGrid terrain, Lookup look, int index)
    {
        // Nothing painted anywhere near: flat.
        if (Array.TrueForAll(_near.Value[index], near => terrain.Tile(near) is null))
        {
            return null;
        }

        int face = index / (TilesPerSide * TilesPerSide);
        int tileRow = index / TilesPerSide % TilesPerSide;
        int tileColumn = index % TilesPerSide;
        byte[] codes = PaddedCodes(terrain, face, tileColumn, tileRow);

        // Most tiles are one terrain all around: one height throughout.
        if (Array.TrueForAll(codes, code => code == codes[0]))
        {
            short height = look.Height[codes[0]];
            return height == 0 ? null : Filled(height);
        }

        (float[] distance, byte[] other) = NearestOther(codes);
        var heights = new short[TileSize * TileSize];
        bool any = false;
        for (int row = 0; row < TileSize; row++)
        {
            for (int column = 0; column < TileSize; column++)
            {
                int at = (row + Pad) * Padded + column + Pad;
                short height = HeightAt(look, codes[at], other[at], distance[at]);
                heights[row * TileSize + column] = height;
                any |= height != 0;
            }
        }

        return any ? heights : null;
    }

    // A cell's height: its own type's, eased toward halfway to its nearest other type's as it
    // comes within half the transition's width of the line between them.
    private static short HeightAt(Lookup look, byte own, byte other, float distance)
    {
        short ownHeight = look.Height[own];
        if (own == other || float.IsPositiveInfinity(distance))
        {
            return ownHeight;
        }

        double half = TransitionCells(look.Edge[own], look.Edge[other]) / 2;
        double middle = (ownHeight + look.Height[other]) / 2.0;
        double t = distance >= half ? 1 : SmoothStep(distance / half);
        return (short)Math.Round(middle + (ownHeight - middle) * t);
    }

    private static double SmoothStep(double x)
    {
        double t = Math.Clamp(x, 0, 1);
        return t * t * (3 - 2 * t);
    }

    // For each cell of the padded block: how far it is from the nearest cell of another type,
    // going only through cells of its own type (0.5 for a cell beside one, as the line lies
    // between them), and which type that is. Two sweeps of a chamfer distance transform.
    private static (float[] Distance, byte[] Other) NearestOther(byte[] codes)
    {
        var distance = new float[codes.Length];
        var other = new byte[codes.Length];
        for (int y = 0; y < Padded; y++)
        {
            for (int x = 0; x < Padded; x++)
            {
                int at = y * Padded + x;
                distance[at] = float.PositiveInfinity;
                other[at] = codes[at];
                foreach ((int dx, int dy) in _sides)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < Padded && ny < Padded
                        && codes[ny * Padded + nx] != codes[at])
                    {
                        distance[at] = 0.5f;
                        other[at] = codes[ny * Padded + nx];
                        break;
                    }
                }
            }
        }

        Sweep(codes, distance, other, forward: true);
        Sweep(codes, distance, other, forward: false);
        return (distance, other);
    }

    private static void Sweep(byte[] codes, float[] distance, byte[] other, bool forward)
    {
        int step = forward ? 1 : -1;
        int start = forward ? 0 : Padded - 1;
        for (int y = start; y >= 0 && y < Padded; y += step)
        {
            for (int x = start; x >= 0 && x < Padded; x += step)
            {
                int at = y * Padded + x;
                Relax(x - step, y, 1);
                Relax(x - step, y - step, Diagonal);
                Relax(x, y - step, 1);
                Relax(x + step, y - step, Diagonal);

                void Relax(int nx, int ny, float length)
                {
                    if (nx < 0 || ny < 0 || nx >= Padded || ny >= Padded)
                    {
                        return;
                    }

                    int from = ny * Padded + nx;
                    if (codes[from] == codes[at] && distance[from] + length < distance[at])
                    {
                        distance[at] = distance[from] + length;
                        other[at] = other[from];
                    }
                }
            }
        }
    }

    // The codes of a tile and the cells around it, Pad deep; around a face's edge, from the
    // neighboring face.
    private static byte[] PaddedCodes(TerrainGrid terrain, int face, int tileColumn, int tileRow)
    {
        var codes = new byte[Padded * Padded];
        for (int y = 0; y < Padded; y++)
        {
            int row = tileRow * TileSize + y - Pad;
            for (int x = 0; x < Padded; x++)
            {
                int column = tileColumn * TileSize + x - Pad;
                codes[y * Padded + x] = column is >= 0 and < FaceSize && row is >= 0 and < FaceSize
                    ? terrain.CodeAt(new CubeCell(face, column, row))
                    : terrain.CodeAt(CubeGridBrush.CellPoint(face, column, row));
            }
        }

        return codes;
    }

    // The tile and the tiles whose padding reaches into it (around a face's edge, on the
    // neighboring faces too): those holding points just beyond Pad all around it.
    private static IEnumerable<int> TilesNear(int index)
    {
        int face = index / (TilesPerSide * TilesPerSide);
        int tileRow = index / TilesPerSide % TilesPerSide;
        int tileColumn = index % TilesPerSide;
        int[] columns = Around(tileColumn), rows = Around(tileRow);
        var near = new HashSet<int> { index };
        foreach (int column in columns)
        {
            foreach (int row in rows)
            {
                near.Add(TileOf(face, column, row));
            }
        }

        return near;
    }

    // Columns (or rows) from just before a tile's padding to just after it, half a tile apart.
    private static int[] Around(int tile)
    {
        int first = tile * TileSize - Pad - 1, last = (tile + 1) * TileSize + Pad;
        var steps = new List<int>();
        for (int at = first; at < last; at += TileSize / 2)
        {
            steps.Add(at);
        }

        steps.Add(last);
        return [.. steps];
    }

    private static int TileOf(int face, int column, int row)
    {
        CubeCell cell = column is >= 0 and < FaceSize && row is >= 0 and < FaceSize
            ? new CubeCell(face, column, row)
            : CubeSphere.CellAt(CubeGridBrush.CellPoint(face, column, row), FaceSize);
        return CubeGridBrush.TileIndex(cell.Face, cell.Column / TileSize, cell.Row / TileSize);
    }

    private static short[] Filled(short height)
    {
        var tile = new short[TileSize * TileSize];
        Array.Fill(tile, height);
        return tile;
    }

    // Each code's height and edge (0 and gentle for unpainted, or a code with no type).
    private sealed class Lookup
    {
        public Lookup(IReadOnlyList<TerrainType> types)
        {
            foreach (TerrainType type in types)
            {
                Height[type.Code] = (short)Math.Clamp(type.HeightMeters,
                    TerrainType.MinHeightMeters, TerrainType.MaxHeightMeters);
                Edge[type.Code] = Math.Clamp(type.Edge, 0, 1);
            }
        }

        public short[] Height { get; } = new short[256];

        public double[] Edge { get; } = new double[256];
    }
}
