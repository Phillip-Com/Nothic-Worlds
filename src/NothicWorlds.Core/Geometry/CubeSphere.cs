namespace NothicWorlds.Core.Geometry;

/// <summary>
/// Divides a sphere into square cells by blowing a cube up into a ball (VISION.md BOD-05): six
/// faces, each a grid of <c>size × size</c> cells. Unlike a latitude/longitude grid, cells stay
/// close to the same size everywhere (within about 1.5×), with no crowding at the poles.
/// </summary>
/// <remarks>
/// <para>
/// Faces, in order: +X, −X, +Y (north), −Y (south), +Z, −Z, using the axes of
/// <see cref="SphericalCoordinates"/>. On each face, columns run along the face's "right" axis
/// and rows run down its "up" axis (row 0 at the top), seen from outside the sphere.
/// </para>
/// <para>
/// Cells are spaced by equal <i>angles</i> rather than equal distances on the cube (an
/// "equal-angle" cube), which keeps their sizes even. <c>godot/Rendering/planet.gdshader</c>
/// repeats this math, and must match it.
/// </para>
/// </remarks>
public static class CubeSphere
{
    /// <summary>The number of faces.</summary>
    public const int FaceCount = 6;

    // Each face's outward direction, "right" axis, and "up" axis. Right × up = outward, so every
    // face is seen the same way round from outside.
    private static readonly (Vector3D Normal, Vector3D Right, Vector3D Up)[] _faces =
    [
        (new(1, 0, 0), new(0, 0, -1), new(0, 1, 0)),   // +X
        (new(-1, 0, 0), new(0, 0, 1), new(0, 1, 0)),   // −X
        (new(0, 1, 0), new(1, 0, 0), new(0, 0, -1)),   // +Y (north)
        (new(0, -1, 0), new(1, 0, 0), new(0, 0, 1)),   // −Y (south)
        (new(0, 0, 1), new(1, 0, 0), new(0, 1, 0)),    // +Z
        (new(0, 0, -1), new(-1, 0, 0), new(0, 1, 0)),  // −Z
    ];

    /// <summary>
    /// A face's outward direction, its "right" axis (along the columns), and its "up" axis
    /// (against the rows).
    /// </summary>
    internal static (Vector3D Normal, Vector3D Right, Vector3D Up) FaceAxes(int face) =>
        _faces[face];

    /// <summary>
    /// The cell a direction from the sphere's center points into, on a grid of
    /// <paramref name="size"/> × <paramref name="size"/> cells per face. The direction doesn't
    /// need to be unit length.
    /// </summary>
    /// <exception cref="ArgumentException">The direction is zero-length or not finite.</exception>
    public static CubeCell CellAt(Vector3D direction, int size)
    {
        RequireSize(size);
        double length = direction.Length;
        if (length == 0.0 || !double.IsFinite(length))
        {
            throw new ArgumentException(
                "Direction must be a finite, non-zero vector.", nameof(direction));
        }

        int face = FaceOf(direction);
        (Vector3D normal, Vector3D right, Vector3D up) = _faces[face];
        double forward = direction.Dot(normal);  // Always positive on its own face.
        double across = ToFaceCoordinate(direction.Dot(right) / forward);
        double down = -ToFaceCoordinate(direction.Dot(up) / forward);
        return new CubeCell(face, ToIndex(across, size), ToIndex(down, size));
    }

    /// <summary>
    /// Where a direction meets a face, as positions across (left edge 0, right edge 1) and down
    /// (top edge 0, bottom edge 1) it: the inverse of <see cref="Direction"/>. The direction
    /// should point into that face (as <see cref="CellAt"/> picks it).
    /// </summary>
    internal static (double Across, double Down) FacePosition(int face, Vector3D direction)
    {
        (Vector3D normal, Vector3D right, Vector3D up) = _faces[face];
        double forward = direction.Dot(normal);
        double across = ToFaceCoordinate(direction.Dot(right) / forward);
        double down = -ToFaceCoordinate(direction.Dot(up) / forward);
        return ((across + 1) / 2, (down + 1) / 2);
    }

    /// <summary>
    /// The unit direction from the sphere's center to the middle of a cell, on a grid of
    /// <paramref name="size"/> × <paramref name="size"/> cells per face.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The cell isn't on the grid.</exception>
    public static Vector3D CellCenter(CubeCell cell, int size)
    {
        RequireSize(size);
        if (cell.Face is < 0 or >= FaceCount || cell.Column < 0 || cell.Column >= size
            || cell.Row < 0 || cell.Row >= size)
        {
            throw new ArgumentOutOfRangeException(nameof(cell), "The cell isn't on the grid.");
        }

        return Direction(cell.Face, Spread(cell.Column + 0.5, size),
            Spread(cell.Row + 0.5, size));
    }

    /// <summary>
    /// The unit direction to a point on a face, given as positions across (left edge 0, right
    /// edge 1) and down (top edge 0, bottom edge 1) the face. Positions between cells work too,
    /// e.g. for corners.
    /// </summary>
    public static Vector3D Direction(int face, double across, double down)
    {
        (Vector3D normal, Vector3D right, Vector3D up) = _faces[face];
        Vector3D point = normal
            + right * FromFaceCoordinate(across * 2 - 1)
            - up * FromFaceCoordinate(down * 2 - 1);
        return point * (1 / point.Length);
    }

    /// <summary>
    /// The up to eight cells around a cell (sides and corners), on a grid of
    /// <paramref name="size"/> × <paramref name="size"/> cells per face, across the face's
    /// edges too (VISION.md BOD-11: water running downhill). A cube's corner has only seven.
    /// </summary>
    public static IEnumerable<CubeCell> Neighbors(CubeCell cell, int size)
    {
        bool inside = cell.Column > 0 && cell.Row > 0
            && cell.Column < size - 1 && cell.Row < size - 1;
        var seen = new HashSet<CubeCell>();
        for (int rows = -1; rows <= 1; rows++)
        {
            for (int columns = -1; columns <= 1; columns++)
            {
                if (rows == 0 && columns == 0)
                {
                    continue;
                }

                // Off the face, the cell over the edge is found by direction.
                CubeCell next = inside
                    ? cell with { Column = cell.Column + columns, Row = cell.Row + rows }
                    : CellAt(Direction(cell.Face, Spread(cell.Column + columns + 0.5, size),
                        Spread(cell.Row + rows + 0.5, size)), size);
                if (next != cell && seen.Add(next))
                {
                    yield return next;
                }
            }
        }
    }

    // The face whose outward direction is closest: the largest component wins.
    private static int FaceOf(Vector3D direction)
    {
        double x = Math.Abs(direction.X);
        double y = Math.Abs(direction.Y);
        double z = Math.Abs(direction.Z);
        if (x >= y && x >= z)
        {
            return direction.X >= 0 ? 0 : 1;
        }

        if (y >= z)
        {
            return direction.Y >= 0 ? 2 : 3;
        }

        return direction.Z >= 0 ? 4 : 5;
    }

    // The flat cube position (-1..1, as tan of the angle) to the equal-angle position (-1..1).
    private static double ToFaceCoordinate(double flat) => Math.Atan(flat) * 4 / Math.PI;

    // The equal-angle position (-1..1) back to the flat cube position.
    private static double FromFaceCoordinate(double spread) => Math.Tan(spread * Math.PI / 4);

    // An equal-angle position (-1..1) to a cell index, keeping the far edge in the last cell.
    private static int ToIndex(double spread, int size) =>
        Math.Clamp((int)Math.Floor((spread + 1) / 2 * size), 0, size - 1);

    // A position counted in cells (e.g. 3.5 = the middle of cell 3) to 0..1 across the face.
    private static double Spread(double cells, int size) => cells / size;

    private static void RequireSize(int size)
    {
        if (size < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "A face needs at least 1 cell.");
        }
    }
}
