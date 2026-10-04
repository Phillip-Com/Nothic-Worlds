using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// The terrain painted on a body (VISION.md BOD-05): one terrain code per cell of a
/// <see cref="CubeSphere"/> grid with <see cref="FaceSize"/> × <see cref="FaceSize"/> cells per
/// face (about 10 km across on an Earth-sized planet). Code 0 means unpainted; other codes are
/// <see cref="TerrainType.Code"/>s.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: painting returns a new grid. The cells are stored in square tiles, and a new grid
/// shares every tile the change didn't touch with the old one. That keeps undo snapshots and
/// world copies cheap even though a fully painted body has six million cells.
/// </para>
/// <para>
/// Unpainted tiles take no memory, so a body with a little painting stays small.
/// </para>
/// </remarks>
public sealed class TerrainGrid
{
    /// <summary>Cells along each edge of a face (owner's choice: the "medium" detail).</summary>
    public const int FaceSize = CubeGridBrush.FaceSize;

    /// <summary>Cells on one face.</summary>
    public const int CellsPerFace = CubeGridBrush.CellsPerFace;

    /// <summary>Cells on the whole body.</summary>
    public const int CellCount = CubeGridBrush.CellCount;

    /// <summary>The smallest brush radius, in degrees of arc.</summary>
    public const double MinBrushRadiusDegrees = CubeGridBrush.MinRadiusDegrees;

    /// <summary>The largest brush radius, in degrees of arc (a whole hemisphere).</summary>
    public const double MaxBrushRadiusDegrees = CubeGridBrush.MaxRadiusDegrees;

    private const int TileSize = CubeGridBrush.TileSize;
    private const int TilesPerSide = CubeGridBrush.TilesPerSide;
    private const int TilesPerFace = CubeGridBrush.TilesPerFace;
    private const int TileCount = CubeGridBrush.TileCount;
    private const int CellsPerTile = CubeGridBrush.CellsPerTile;

    // Null for a tile with nothing painted. Never modified once the grid is created.
    private readonly byte[]?[] _tiles;

    private TerrainGrid(byte[]?[] tiles)
    {
        _tiles = tiles;
    }

    /// <summary>A grid with nothing painted.</summary>
    public static TerrainGrid Empty { get; } = new(new byte[]?[TileCount]);

    /// <summary>True if nothing is painted.</summary>
    public bool IsEmpty => Array.TrueForAll(_tiles, tile => tile is null);

    /// <summary>The terrain code in a cell (0 if unpainted).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The cell isn't on the grid.</exception>
    public byte CodeAt(CubeCell cell)
    {
        if (cell.Face is < 0 or >= CubeSphere.FaceCount || cell.Column is < 0 or >= FaceSize
            || cell.Row is < 0 or >= FaceSize)
        {
            throw new ArgumentOutOfRangeException(nameof(cell), "The cell isn't on the grid.");
        }

        byte[]? tile = _tiles[TileIndex(cell.Face, cell.Column / TileSize, cell.Row / TileSize)];
        return tile?[CellIndex(cell.Column, cell.Row)] ?? 0;
    }

    /// <summary>
    /// The terrain code where a direction from the body's center meets its surface.
    /// </summary>
    /// <exception cref="ArgumentException">The direction is zero-length or not finite.</exception>
    public byte CodeAt(Vector3D direction)
    {
        return CodeAt(CubeSphere.CellAt(direction, FaceSize));
    }

    /// <summary>True if any cell has terrain code <paramref name="code"/>.</summary>
    public bool Uses(byte code)
    {
        return code == 0
            ? Array.Exists(_tiles, tile => tile is null || tile.AsSpan().Contains(code))
            : Array.Exists(_tiles, tile => tile is not null && tile.AsSpan().Contains(code));
    }

    /// <summary>
    /// Paints a round brush stamp: every cell whose center lies within
    /// <paramref name="radiusDegrees"/> of arc from <paramref name="center"/> gets
    /// <paramref name="code"/> (0 erases). Returns this grid if nothing changed.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The center isn't a finite, non-zero direction, or the radius is out of range.
    /// </exception>
    public TerrainGrid Paint(Vector3D center, double radiusDegrees, byte code)
    {
        return PaintStamps([CubeGridBrush.Normalized(center)], radiusDegrees, code);
    }

    /// <summary>
    /// Paints a brush stroke from <paramref name="from"/> to <paramref name="to"/> along the
    /// shortest path over the surface, with round ends, as one change. Returns this grid if
    /// nothing changed.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// An end isn't a finite, non-zero direction, or the radius is out of range.
    /// </exception>
    public TerrainGrid PaintStroke(Vector3D from, Vector3D to, double radiusDegrees, byte code)
    {
        return PaintStamps(CubeGridBrush.Stamps(from, to, radiusDegrees), radiusDegrees, code);
    }

    /// <summary>
    /// Changes every cell with code <paramref name="from"/> to <paramref name="to"/> (e.g. 0 to
    /// clear a deleted terrain type). Returns this grid if nothing changed.
    /// </summary>
    public TerrainGrid Replace(byte from, byte to)
    {
        if (from == to || !Uses(from))
        {
            return this;
        }

        var tiles = new byte[]?[TileCount];
        for (int index = 0; index < TileCount; index++)
        {
            byte[]? tile = _tiles[index];
            if (tile is null)
            {
                tiles[index] = from == 0 ? Filled(to) : null;
                continue;
            }

            if (!tile.AsSpan().Contains(from))
            {
                tiles[index] = tile;
                continue;
            }

            byte[] copy = (byte[])tile.Clone();
            copy.AsSpan().Replace(from, to);
            tiles[index] = NullIfUnpainted(copy);
        }

        return new TerrainGrid(tiles);
    }

    /// <summary>True if both grids have the same code in every cell.</summary>
    public bool HasSameCells(TerrainGrid other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        for (int index = 0; index < TileCount; index++)
        {
            byte[]? mine = _tiles[index];
            byte[]? theirs = other._tiles[index];
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
    /// those on the graphics card after a brush stroke. Quick: it compares tiles by identity,
    /// so it may include a face whose cells ended up the same.
    /// </summary>
    public IReadOnlyList<int> FacesChangedFrom(TerrainGrid other)
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
    /// Copies one face's codes into <paramref name="destination"/>, row by row from the top
    /// (<see cref="CellsPerFace"/> bytes).
    /// </summary>
    /// <exception cref="ArgumentException">The destination is too small.</exception>
    public void CopyFace(int face, Span<byte> destination)
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
            Span<byte> line = destination.Slice(row * FaceSize, FaceSize);
            for (int tileColumn = 0; tileColumn < TilesPerSide; tileColumn++)
            {
                Span<byte> part = line.Slice(tileColumn * TileSize, TileSize);
                byte[]? tile = _tiles[TileIndex(face, tileColumn, row / TileSize)];
                if (tile is null)
                {
                    part.Clear();
                }
                else
                {
                    tile.AsSpan((row % TileSize) * TileSize, TileSize).CopyTo(part);
                }
            }
        }
    }

    /// <summary>
    /// Creates a grid from every cell's code: the six faces in order, each row by row from the
    /// top (<see cref="CellCount"/> bytes, the same layout as <see cref="CopyFace"/>).
    /// </summary>
    /// <exception cref="ArgumentException">The wrong number of cells.</exception>
    public static TerrainGrid FromCells(ReadOnlySpan<byte> cells)
    {
        if (cells.Length != CellCount)
        {
            throw new ArgumentException(
                $"A terrain grid has {CellCount} cells, not {cells.Length}.", nameof(cells));
        }

        var tiles = new byte[]?[TileCount];
        for (int face = 0; face < CubeSphere.FaceCount; face++)
        {
            ReadOnlySpan<byte> faceCells = cells.Slice(face * CellsPerFace, CellsPerFace);
            for (int tileRow = 0; tileRow < TilesPerSide; tileRow++)
            {
                for (int tileColumn = 0; tileColumn < TilesPerSide; tileColumn++)
                {
                    tiles[TileIndex(face, tileColumn, tileRow)] =
                        ReadTile(faceCells, tileColumn, tileRow);
                }
            }
        }

        return new TerrainGrid(tiles);
    }

    private static byte[]? ReadTile(ReadOnlySpan<byte> faceCells, int tileColumn, int tileRow)
    {
        var tile = new byte[CellsPerTile];
        for (int row = 0; row < TileSize; row++)
        {
            int start = (tileRow * TileSize + row) * FaceSize + tileColumn * TileSize;
            faceCells.Slice(start, TileSize).CopyTo(tile.AsSpan(row * TileSize, TileSize));
        }

        return NullIfUnpainted(tile);
    }

    // Paints every stamp in one pass, copying each touched tile once.
    private TerrainGrid PaintStamps(
        IReadOnlyList<Vector3D> stamps, double radiusDegrees, byte code)
    {
        CubeGridBrush.RequireRadius(radiusDegrees);
        double radius = double.DegreesToRadians(radiusDegrees);
        double minimumDot = Math.Cos(radius);
        byte[]?[]? changed = null;
        var nearStamps = new List<Vector3D>();

        for (int index = 0; index < TileCount; index++)
        {
            byte[]? tile = _tiles[index];
            if (tile is null && code == 0)
            {
                continue;  // Nothing to erase.
            }

            (Vector3D tileCenter, double reach) = CubeGridBrush.TileBounds[index];
            nearStamps.Clear();
            bool covered = false;
            foreach (Vector3D stamp in stamps)
            {
                double distance = Math.Acos(Math.Clamp(stamp.Dot(tileCenter), -1, 1));
                covered |= distance + reach <= radius;
                if (distance <= radius + reach)
                {
                    nearStamps.Add(stamp);
                }
            }

            // A tile wholly inside a stamp is filled at once (big brushes cover many).
            byte[]? painted = null;
            if (covered)
            {
                painted = FillTile(tile, code);
            }
            else if (nearStamps.Count > 0)
            {
                painted = PaintTile(index, tile, nearStamps, minimumDot, code);
            }

            if (painted is not null)
            {
                changed ??= (byte[]?[])_tiles.Clone();
                changed[index] = NullIfUnpainted(painted);
            }
        }

        return changed is null ? this : new TerrainGrid(changed);
    }

    // Returns the tile filled with one code, or null if it already was.
    private static byte[]? FillTile(byte[]? tile, byte code)
    {
        bool alreadyFilled = tile is null
            ? code == 0
            : !tile.AsSpan().ContainsAnyExcept(code);
        if (alreadyFilled)
        {
            return null;
        }

        var filled = new byte[CellsPerTile];
        filled.AsSpan().Fill(code);
        return filled;
    }

    // Returns a painted copy of the tile, or null if no cell changed.
    private static byte[]? PaintTile(
        int index, byte[]? tile, List<Vector3D> stamps, double minimumDot, byte code)
    {
        int face = index / TilesPerFace;
        int firstRow = index % TilesPerFace / TilesPerSide * TileSize;
        int firstColumn = index % TilesPerSide * TileSize;
        (Vector3D normal, Vector3D right, Vector3D up) = CubeSphere.FaceAxes(face);
        byte[]? copy = null;

        for (int row = 0; row < TileSize; row++)
        {
            Vector3D rowPoint = normal - up * CubeGridBrush.CellOffsets[firstRow + row];
            for (int column = 0; column < TileSize; column++)
            {
                int cell = row * TileSize + column;
                if ((tile?[cell] ?? 0) == code)
                {
                    continue;
                }

                // The cell's center on the flat cube; it's inside a stamp if the angle to the
                // stamp is small enough, i.e. its dot product is big enough for its length.
                Vector3D point =
                    rowPoint + right * CubeGridBrush.CellOffsets[firstColumn + column];
                double limit = minimumDot * point.Length;
                foreach (Vector3D stamp in stamps)
                {
                    if (point.Dot(stamp) >= limit)
                    {
                        copy ??= tile is null ? new byte[CellsPerTile] : (byte[])tile.Clone();
                        copy[cell] = code;
                        break;
                    }
                }
            }
        }

        return copy;
    }

    private static byte[]? Filled(byte code)
    {
        if (code == 0)
        {
            return null;
        }

        var tile = new byte[CellsPerTile];
        tile.AsSpan().Fill(code);
        return tile;
    }

    private static byte[]? NullIfUnpainted(byte[] tile)
    {
        return tile.AsSpan().ContainsAnyExcept((byte)0) ? tile : null;
    }

    private static int TileIndex(int face, int tileColumn, int tileRow) =>
        CubeGridBrush.TileIndex(face, tileColumn, tileRow);

    private static int CellIndex(int column, int row) => CubeGridBrush.CellIndex(column, row);
}
