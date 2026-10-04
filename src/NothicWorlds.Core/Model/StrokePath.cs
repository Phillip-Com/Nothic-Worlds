using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// The path of a brush stroke over a sphere (the shortest way from its start to its end), for
/// measuring how far any point is from it. Measuring from the path itself, rather than from
/// round stamps along it, keeps a stroke's edges and ridge perfectly even.
/// </summary>
public sealed class StrokePath
{
    // Each piece of the path: its ends, the axis of its great circle, and whether it's a point.
    private readonly List<(Vector3D Start, Vector3D End, Vector3D Axis, bool IsPoint)> _pieces;

    /// <summary>The path from <paramref name="from"/> to <paramref name="to"/>.</summary>
    /// <exception cref="ArgumentException">An end isn't a finite, non-zero direction.</exception>
    public StrokePath(Vector3D from, Vector3D to)
    {
        // In pieces of at most 22.5° (stamps for the widest brush), so no piece is ever half a
        // circle or more, where "between the ends" stops meaning one thing.
        List<Vector3D> points = CubeGridBrush.Stamps(from, to, CubeGridBrush.MaxRadiusDegrees);
        _pieces = [];
        for (int index = 1; index < points.Count; index++)
        {
            Vector3D start = points[index - 1];
            Vector3D end = points[index];
            Vector3D axis = Cross(start, end);
            double length = axis.Length;
            _pieces.Add(length < 1e-12
                ? (start, end, Vector3D.Zero, true)
                : (start, end, axis * (1 / length), false));
        }
    }

    /// <summary>
    /// How far a unit direction is from the path, in radians of arc: to the nearest point on
    /// it, whether along the way or at an end.
    /// </summary>
    public double DistanceTo(Vector3D point)
    {
        double nearest = double.PositiveInfinity;
        foreach ((Vector3D start, Vector3D end, Vector3D axis, bool isPoint) in _pieces)
        {
            double toEnds = Math.Min(Angle(point, start), Angle(point, end));
            if (isPoint)
            {
                nearest = Math.Min(nearest, toEnds);
                continue;
            }

            // The point's shadow on the piece's great circle counts if it falls between the ends.
            double off = point.Dot(axis);
            Vector3D shadow = point - axis * off;
            bool between = Cross(start, shadow).Dot(axis) >= 0
                && Cross(shadow, end).Dot(axis) >= 0;
            nearest = Math.Min(nearest,
                between ? Math.Asin(Math.Min(1, Math.Abs(off))) : toEnds);
        }

        return nearest;
    }

    private static double Angle(Vector3D a, Vector3D b) => Math.Acos(Math.Clamp(a.Dot(b), -1, 1));

    private static Vector3D Cross(Vector3D a, Vector3D b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}
