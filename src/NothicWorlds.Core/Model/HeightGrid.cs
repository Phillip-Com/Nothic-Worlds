using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// A body's sculpted heights (VISION.md BOD-04): one height in whole meters per cell of the
/// same <see cref="CubeSphere"/> grid as painted terrain (<see cref="TerrainGrid"/>), measured
/// up from the body's radius. The owner's design is heights plus shape edits; this is the
/// heights.
/// </summary>
/// <remarks>
/// <para>Owner's choice: ±32 km in 1 m steps, true to scale (the view can exaggerate them).</para>
/// <para>Immutable: sculpting returns a new grid that shares every tile the stroke didn't touch,
/// so undo snapshots and world copies stay cheap. Flat tiles (all zero) take no memory.</para>
/// <para>Each brush shapes the ground with a soft edge: full strength along the stroke's path,
/// easing smoothly to nothing at its radius from it, so a stroke's ridge is perfectly even
/// however it's drawn.</para>
/// </remarks>
public sealed class HeightGrid
{
    /// <summary>Cells along each edge of a face (the same as painted terrain).</summary>
    public const int FaceSize = CubeGridBrush.FaceSize;

    /// <summary>Cells on one face.</summary>
    public const int CellsPerFace = CubeGridBrush.CellsPerFace;

    /// <summary>Cells on the whole body.</summary>
    public const int CellCount = CubeGridBrush.CellCount;

    /// <summary>The highest a cell can be, in meters above the body's radius.</summary>
    public const short MaxHeightMeters = short.MaxValue;

    /// <summary>The lowest a cell can be, in meters (below the radius).</summary>
    public const short MinHeightMeters = -short.MaxValue;

    private const int TileSize = CubeGridBrush.TileSize;
    private const int TilesPerSide = CubeGridBrush.TilesPerSide;
    private const int TilesPerFace = CubeGridBrush.TilesPerFace;
    private const int TileCount = CubeGridBrush.TileCount;
    private const int CellsPerTile = CubeGridBrush.CellsPerTile;

    // Smoothing averages each cell with points this fraction of the brush radius away (at
    // least the next cell), so a big brush smooths big features.
    private const double SmoothingReach = 1.0 / 8;
    private const int MaxSmoothingCells = 32;

    // Null for a flat tile. Never modified once the grid is created.
    private readonly short[]?[] _tiles;

    // The highest cell, worked out the first time it's asked for (the grid never changes).
    private short? _highest;

    private HeightGrid(short[]?[] tiles)
    {
        _tiles = tiles;
    }

    /// <summary>A grid with nothing sculpted: every cell at height 0.</summary>
    public static HeightGrid Empty { get; } = new(new short[]?[TileCount]);

    /// <summary>True if nothing is sculpted.</summary>
    public bool IsEmpty => Array.TrueForAll(_tiles, tile => tile is null);

    /// <summary>A cell's height, in meters above the body's radius.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The cell isn't on the grid.</exception>
    public short HeightAt(CubeCell cell)
    {
        if (cell.Face is < 0 or >= CubeSphere.FaceCount || cell.Column is < 0 or >= FaceSize
            || cell.Row is < 0 or >= FaceSize)
        {
            throw new ArgumentOutOfRangeException(nameof(cell), "The cell isn't on the grid.");
        }

        return Height(cell.Face, cell.Column, cell.Row);
    }

    /// <summary>The height where a direction from the body's center meets its surface.</summary>
    /// <exception cref="ArgumentException">The direction is zero-length or not finite.</exception>
    public short HeightAt(Vector3D direction) =>
        HeightAt(CubeSphere.CellAt(direction, FaceSize));

    /// <summary>
    /// The height where a direction meets the surface, blended smoothly between the four
    /// nearest cells' middles on its face (as the planet shader draws it), in meters.
    /// </summary>
    /// <exception cref="ArgumentException">The direction is zero-length or not finite.</exception>
    public double SampleAt(Vector3D direction)
    {
        CubeCell cell = CubeSphere.CellAt(direction, FaceSize);
        (double across, double down) = CubeSphere.FacePosition(cell.Face, direction);
        double x = Math.Clamp(across * FaceSize - 0.5, 0, FaceSize - 1);
        double y = Math.Clamp(down * FaceSize - 0.5, 0, FaceSize - 1);
        int left = Math.Min((int)x, FaceSize - 2);
        int top = Math.Min((int)y, FaceSize - 2);
        double fx = x - left;
        double fy = y - top;
        double upper = Height(cell.Face, left, top) * (1 - fx)
            + Height(cell.Face, left + 1, top) * fx;
        double lower = Height(cell.Face, left, top + 1) * (1 - fx)
            + Height(cell.Face, left + 1, top + 1) * fx;
        return upper * (1 - fy) + lower * fy;
    }

    /// <summary>The highest cell, in meters (0 if nothing is raised).</summary>
    public short Highest => _highest ??= _tiles.Max(tile => tile?.Max() ?? 0);

    /// <summary>
    /// Raises the ground along a stroke from <paramref name="from"/> to <paramref name="to"/>
    /// by up to <paramref name="meters"/> (negative lowers it), as one change. Returns this
    /// grid if nothing changed.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// An end isn't a finite, non-zero direction, the radius is out of range, or the height
    /// isn't finite.
    /// </exception>
    public HeightGrid Raise(Vector3D from, Vector3D to, double radiusDegrees, double meters)
    {
        RequireFinite(meters, nameof(meters));
        return Sculpt(from, to, radiusDegrees, (_, _, _, height, weight) =>
            height + meters * weight);
    }

    /// <summary>
    /// Levels the ground along a stroke toward <paramref name="targetMeters"/> (e.g. the height
    /// where the stroke began), by <paramref name="amount"/> (0 to 1) at the stroke's middle.
    /// Returns this grid if nothing changed.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// An end isn't a finite, non-zero direction, or the radius or amount is out of range.
    /// </exception>
    public HeightGrid Flatten(
        Vector3D from, Vector3D to, double radiusDegrees, double targetMeters, double amount)
    {
        RequireFinite(targetMeters, nameof(targetMeters));
        RequireAmount(amount);
        return Sculpt(from, to, radiusDegrees, (_, _, _, height, weight) =>
            height + (targetMeters - height) * amount * weight);
    }

    /// <summary>
    /// Evens out bumps along a stroke: each cell moves toward the average of the ground around
    /// it (as it was before the stroke), by <paramref name="amount"/> (0 to 1) at the stroke's
    /// middle. Returns this grid if nothing changed.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// An end isn't a finite, non-zero direction, or the radius or amount is out of range.
    /// </exception>
    public HeightGrid Smooth(Vector3D from, Vector3D to, double radiusDegrees, double amount)
    {
        RequireAmount(amount);
        double cellDegrees = 90.0 / FaceSize;
        int reach = (int)Math.Clamp(Math.Round(radiusDegrees * SmoothingReach / cellDegrees),
            1, MaxSmoothingCells);
        return Sculpt(from, to, radiusDegrees, (face, column, row, height, weight) =>
            height + (Average(face, column, row, reach) - height) * amount * weight);
    }

    /// <summary>True if both grids have the same height in every cell.</summary>
    public bool HasSameCells(HeightGrid other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        for (int index = 0; index < TileCount; index++)
        {
            short[]? mine = _tiles[index];
            short[]? theirs = other._tiles[index];
            if (!ReferenceEquals(mine, theirs)
                && (mine is null || theirs is null || !mine.AsSpan().SequenceEqual(theirs)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The faces (0 to 5) that may differ from <paramref name="other"/>, e.g. to update only
    /// those on the graphics card after a stroke. Quick: it compares tiles by identity.
    /// </summary>
    public IReadOnlyList<int> FacesChangedFrom(HeightGrid other)
    {
        var faces = new List<int>();
        for (int face = 0; face < CubeSphere.FaceCount; face++)
        {
            int first = face * TilesPerFace;
            for (int index = first; index < first + TilesPerFace; index++)
            {
                if (!ReferenceEquals(_tiles[index], other._tiles[index]))
                {
                    faces.Add(face);
                    break;
                }
            }
        }

        return faces;
    }

    /// <summary>
    /// Copies one face's heights into <paramref name="destination"/>, row by row from the top
    /// (<see cref="CellsPerFace"/> values).
    /// </summary>
    /// <exception cref="ArgumentException">The destination is too small.</exception>
    public void CopyFace(int face, Span<short> destination)
    {
        if (face is < 0 or >= CubeSphere.FaceCount)
        {
            throw new ArgumentOutOfRangeException(nameof(face));
        }

        if (destination.Length < CellsPerFace)
        {
            throw new ArgumentException("The destination is too small.", nameof(destination));
        }

        for (int row = 0; row < FaceSize; row++)
        {
            Span<short> line = destination.Slice(row * FaceSize, FaceSize);
            for (int tileColumn = 0; tileColumn < TilesPerSide; tileColumn++)
            {
                Span<short> part = line.Slice(tileColumn * TileSize, TileSize);
                short[]? tile = _tiles[CubeGridBrush.TileIndex(face, tileColumn, row / TileSize)];
                if (tile is null)
                {
                    part.Clear();
                }
                else
                {
                    tile.AsSpan(row % TileSize * TileSize, TileSize).CopyTo(part);
                }
            }
        }
    }

    /// <summary>
    /// Creates a grid from every cell's height: the six faces in order, each row by row from
    /// the top (<see cref="CellCount"/> values, the same layout as <see cref="CopyFace"/>).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The wrong number of cells, or a height below <see cref="MinHeightMeters"/>.
    /// </exception>
    public static HeightGrid FromCells(ReadOnlySpan<short> cells)
    {
        if (cells.Length != CellCount)
        {
            throw new ArgumentException(
                $"A height grid has {CellCount} cells, not {cells.Length}.", nameof(cells));
        }

        if (cells.Contains(short.MinValue))
        {
            throw new ArgumentException(
                $"Heights must be at least {MinHeightMeters} m.", nameof(cells));
        }

        var tiles = new short[]?[TileCount];
        for (int face = 0; face < CubeSphere.FaceCount; face++)
        {
            ReadOnlySpan<short> faceCells = cells.Slice(face * CellsPerFace, CellsPerFace);
            for (int tileRow = 0; tileRow < TilesPerSide; tileRow++)
            {
                for (int tileColumn = 0; tileColumn < TilesPerSide; tileColumn++)
                {
                    var tile = new short[CellsPerTile];
                    for (int row = 0; row < TileSize; row++)
                    {
                        int start = (tileRow * TileSize + row) * FaceSize + tileColumn * TileSize;
                        faceCells.Slice(start, TileSize)
                            .CopyTo(tile.AsSpan(row * TileSize, TileSize));
                    }

                    tiles[CubeGridBrush.TileIndex(face, tileColumn, tileRow)] = NullIfFlat(tile);
                }
            }
        }

        return new HeightGrid(tiles);
    }

    // The new height of a cell from where it is and how strongly the brush reaches it (0 to
    // 1), given its face, column, and row.
    private delegate double CellChange(int face, int column, int row, short height, double weight);

    // Applies a brush along a stroke in one pass, copying each touched tile once.
    private HeightGrid Sculpt(Vector3D from, Vector3D to, double radiusDegrees, CellChange change)
    {
        CubeGridBrush.RequireRadius(radiusDegrees);
        var path = new StrokePath(from, to);
        double radius = double.DegreesToRadians(radiusDegrees);
        short[]?[]? changed = null;
        for (int index = 0; index < TileCount; index++)
        {
            (Vector3D tileCenter, double reach) = CubeGridBrush.TileBounds[index];
            if (path.DistanceTo(tileCenter) <= radius + reach
                && SculptTile(index, path, radius, change) is short[] sculpted)
            {
                changed ??= (short[]?[])_tiles.Clone();
                changed[index] = NullIfFlat(sculpted);
            }
        }

        return changed is null ? this : new HeightGrid(changed);
    }

    // Returns a sculpted copy of the tile, or null if no cell changed.
    private short[]? SculptTile(int index, StrokePath path, double radius, CellChange change)
    {
        int face = index / TilesPerFace;
        int firstRow = index % TilesPerFace / TilesPerSide * TileSize;
        int firstColumn = index % TilesPerSide * TileSize;
        short[]? tile = _tiles[index];
        short[]? copy = null;
        for (int row = 0; row < TileSize; row++)
        {
            for (int column = 0; column < TileSize; column++)
            {
                Vector3D point = CubeGridBrush.CellPoint(face, firstColumn + column,
                    firstRow + row);
                point *= 1 / point.Length;
                double angle = path.DistanceTo(point);
                if (angle >= radius)
                {
                    continue;
                }

                // Full strength on the path, easing to nothing at the edge (a cosine bump).
                double weight = 0.5 * (1 + Math.Cos(Math.PI * angle / radius));
                int cell = row * TileSize + column;
                short height = tile?[cell] ?? 0;
                short sculpted = Clamped(change(face, firstColumn + column, firstRow + row,
                    height, weight));
                if (sculpted != height)
                {
                    copy ??= tile is null ? new short[CellsPerTile] : (short[])tile.Clone();
                    copy[cell] = sculpted;
                }
            }
        }

        return copy;
    }

    // The mean height of a cell and the eight points `reach` cells away round it (across face
    // edges onto the neighboring faces too).
    private double Average(int face, int column, int row, int reach)
    {
        double sum = 0;
        for (int down = -1; down <= 1; down++)
        {
            for (int across = -1; across <= 1; across++)
            {
                sum += Height(face, column + across * reach, row + down * reach);
            }
        }

        return sum / 9;
    }

    // A cell's height, for any column or row, even past the face's edge (that cell is on a
    // neighboring face).
    private short Height(int face, int column, int row)
    {
        if (column is < 0 or >= FaceSize || row is < 0 or >= FaceSize)
        {
            CubeCell cell = CubeSphere.CellAt(CubeGridBrush.CellPoint(face, column, row),
                FaceSize);
            (face, column, row) = (cell.Face, cell.Column, cell.Row);
        }

        short[]? tile = _tiles[CubeGridBrush.TileIndex(face, column / TileSize, row / TileSize)];
        return tile?[CubeGridBrush.CellIndex(column, row)] ?? 0;
    }

    private static short Clamped(double meters) => (short)Math.Clamp(
        Math.Round(meters, MidpointRounding.AwayFromZero), MinHeightMeters, MaxHeightMeters);

    private static short[]? NullIfFlat(short[] tile) =>
        tile.AsSpan().ContainsAnyExcept((short)0) ? tile : null;

    private static void RequireFinite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentException("The height must be a number.", name);
        }
    }

    private static void RequireAmount(double amount)
    {
        if (!double.IsFinite(amount) || amount is < 0 or > 1)
        {
            throw new ArgumentException("The amount must be 0 to 1.", nameof(amount));
        }
    }
}
