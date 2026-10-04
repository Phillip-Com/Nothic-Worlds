using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// The geometry shared by the grids brushed onto a body's surface (painted terrain, sculpted
/// heights): a <see cref="CubeSphere"/> grid of <see cref="FaceSize"/> × <see cref="FaceSize"/>
/// cells a face, kept in square tiles, and round brushes stroked across it.
/// </summary>
internal static class CubeGridBrush
{
    /// <summary>Cells along each edge of a face (about 10 km on an Earth-sized planet).</summary>
    public const int FaceSize = 1024;

    /// <summary>Cells on one face.</summary>
    public const int CellsPerFace = FaceSize * FaceSize;

    /// <summary>Cells on the whole body.</summary>
    public const int CellCount = CubeSphere.FaceCount * CellsPerFace;

    /// <summary>The smallest brush radius, in degrees of arc.</summary>
    public const double MinRadiusDegrees = 0.01;

    /// <summary>The largest brush radius, in degrees of arc (a whole hemisphere).</summary>
    public const double MaxRadiusDegrees = 90;

    // Tiles are TileSize × TileSize cells: small enough that a brush stroke copies little, and
    // few enough (1,536) to scan quickly.
    public const int TileSize = 64;
    public const int TilesPerSide = FaceSize / TileSize;
    public const int TilesPerFace = TilesPerSide * TilesPerSide;
    public const int TileCount = CubeSphere.FaceCount * TilesPerFace;
    public const int CellsPerTile = TileSize * TileSize;

    // A long stroke is a row of brush stamps; this caps how many, so even an absurd stroke (a
    // tiny brush dragged halfway round) stays quick.
    private const int MaxStampsPerStroke = 2048;

    /// <summary>
    /// Where each cell's center sits on the flat cube, from the face's middle (-1..1), indexed
    /// by column (across) or row (down). The same for every face. See CubeSphere.Direction.
    /// </summary>
    public static double[] CellOffsets { get; } = CreateCellOffsets();

    /// <summary>
    /// Each tile's middle and how far its farthest corner is from it (radians), for skipping
    /// tiles a brush can't reach.
    /// </summary>
    public static (Vector3D Center, double Reach)[] TileBounds { get; } = CreateTileBounds();

    /// <summary>
    /// Where a cell's center is on the flat cube, for any column or row, even past the face's
    /// edge (the point then lies over a neighboring face; normalize it for a direction).
    /// </summary>
    public static Vector3D CellPoint(int face, int column, int row)
    {
        (Vector3D normal, Vector3D right, Vector3D up) = CubeSphere.FaceAxes(face);
        return normal + right * Offset(column) - up * Offset(row);
    }

    /// <summary>
    /// The brush stamps along a stroke from <paramref name="from"/> to <paramref name="to"/>
    /// over the shortest path, a quarter of a radius apart so the stroke's edges look straight.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// An end isn't a finite, non-zero direction, or the radius is out of range.
    /// </exception>
    public static List<Vector3D> Stamps(Vector3D from, Vector3D to, double radiusDegrees)
    {
        Vector3D start = Normalized(from);
        Vector3D end = Normalized(to);
        RequireRadius(radiusDegrees);
        double arc = Math.Acos(Math.Clamp(start.Dot(end), -1, 1));
        double spacing = double.DegreesToRadians(radiusDegrees) / 4;
        int steps = (int)Math.Clamp(Math.Ceiling(arc / spacing), 1, MaxStampsPerStroke);
        var stamps = new List<Vector3D>(steps + 1);
        for (int step = 0; step <= steps; step++)
        {
            stamps.Add(Between(start, end, arc, (double)step / steps));
        }

        return stamps;
    }

    /// <summary>A unit vector, or an exception for a zero or non-finite one.</summary>
    /// <exception cref="ArgumentException">The direction can't be normalized.</exception>
    public static Vector3D Normalized(Vector3D direction)
    {
        double length = direction.Length;
        if (length == 0 || !double.IsFinite(length))
        {
            throw new ArgumentException(
                "Direction must be a finite, non-zero vector.", nameof(direction));
        }

        return direction * (1 / length);
    }

    /// <summary>Throws if a brush radius is out of range.</summary>
    /// <exception cref="ArgumentException">The radius is out of range.</exception>
    public static void RequireRadius(double radiusDegrees)
    {
        if (!double.IsFinite(radiusDegrees)
            || radiusDegrees is < MinRadiusDegrees or > MaxRadiusDegrees)
        {
            throw new ArgumentException(
                $"The brush radius must be {MinRadiusDegrees}° to {MaxRadiusDegrees}°.",
                nameof(radiusDegrees));
        }
    }

    /// <summary>The tile holding a cell.</summary>
    public static int TileIndex(int face, int tileColumn, int tileRow) =>
        face * TilesPerFace + tileRow * TilesPerSide + tileColumn;

    /// <summary>A cell's place within its tile.</summary>
    public static int CellIndex(int column, int row) =>
        row % TileSize * TileSize + column % TileSize;

    // The point a fraction of the way along the arc between two unit directions.
    private static Vector3D Between(Vector3D start, Vector3D end, double arc, double fraction)
    {
        if (arc < 1e-9)
        {
            return start;
        }

        if (Math.PI - arc < 1e-9)
        {
            // Opposite points: any path is shortest. Go over the start's own "east".
            Vector3D side = Math.Abs(start.Y) < 0.9 ? new(start.Z, 0, -start.X) : new(1, 0, 0);
            side = side - start * side.Dot(start);
            side = side * (1 / side.Length);
            double angle = arc * fraction;
            return start * Math.Cos(angle) + side * Math.Sin(angle);
        }

        double sin = Math.Sin(arc);
        return start * (Math.Sin((1 - fraction) * arc) / sin)
            + end * (Math.Sin(fraction * arc) / sin);
    }

    private static double Offset(int index) => index is >= 0 and < FaceSize
        ? CellOffsets[index]
        : Math.Tan(((index + 0.5) / FaceSize * 2 - 1) * Math.PI / 4);

    private static double[] CreateCellOffsets()
    {
        var offsets = new double[FaceSize];
        for (int index = 0; index < FaceSize; index++)
        {
            double spread = (index + 0.5) / FaceSize * 2 - 1;
            offsets[index] = Math.Tan(spread * Math.PI / 4);
        }

        return offsets;
    }

    private static (Vector3D, double)[] CreateTileBounds()
    {
        const double margin = 1e-6;  // Guards against rounding at the very edge.
        var bounds = new (Vector3D, double)[TileCount];
        for (int index = 0; index < TileCount; index++)
        {
            int face = index / TilesPerFace;
            double top = (double)(index % TilesPerFace / TilesPerSide) / TilesPerSide;
            double left = (double)(index % TilesPerSide) / TilesPerSide;
            double size = 1.0 / TilesPerSide;
            Vector3D center = CubeSphere.Direction(face, left + size / 2, top + size / 2);

            // Tile edges are great circles, so the farthest point from the middle is a corner.
            double reach = 0;
            foreach ((double across, double down) in new[]
                { (left, top), (left + size, top), (left, top + size), (left + size, top + size) })
            {
                Vector3D corner = CubeSphere.Direction(face, across, down);
                reach = Math.Max(reach, Math.Acos(Math.Clamp(corner.Dot(center), -1, 1)));
            }

            bounds[index] = (center, reach + margin);
        }

        return bounds;
    }
}
