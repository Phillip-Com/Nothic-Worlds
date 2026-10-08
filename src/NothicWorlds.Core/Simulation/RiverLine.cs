using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A river's course as it's drawn (VISION.md BOD-11), from orbit and up close alike: a drawn
/// river's points with its long stretches cut into steps, so it follows the globe, or a natural
/// river's cells smoothed, so it doesn't zigzag from cell to cell.
/// </summary>
public static class RiverLine
{
    /// <summary>The longest step along a drawn river, in degrees of arc.</summary>
    public const double StepDegrees = 0.5;

    // How many times a natural river's cell-to-cell course is smoothed.
    private const int SmoothingPasses = 2;

    /// <summary>The course of <paramref name="course"/> as drawn, as unit directions.</summary>
    public static List<Vector3D> PathOf(RiverCourseShown course)
    {
        if (course.Kind == RiverKind.Drawn)
        {
            return Densified(course.Points);
        }

        List<Vector3D> path = [.. course.Points];
        for (int pass = 0; pass < SmoothingPasses; pass++)
        {
            path = Smoothed(path);
        }

        return path;
    }

    private static List<Vector3D> Densified(IReadOnlyList<Vector3D> points)
    {
        var path = new List<Vector3D> { points[0] };
        for (int i = 1; i < points.Count; i++)
        {
            double degrees = double.RadiansToDegrees(
                Math.Acos(Math.Clamp(points[i - 1].Dot(points[i]), -1, 1)));
            int steps = Math.Max(1, (int)Math.Ceiling(degrees / StepDegrees));
            for (int step = 1; step <= steps; step++)
            {
                double share = (double)step / steps;
                path.Add(step == steps
                    ? points[i]
                    : Unit(points[i - 1] * (1 - share) + points[i] * share));
            }
        }

        return path;
    }

    // Chaikin's corner cutting: each corner is replaced by two points a quarter of the way
    // along its sides. The source and the mouth stay where they are.
    private static List<Vector3D> Smoothed(List<Vector3D> path)
    {
        if (path.Count < 3)
        {
            return path;
        }

        var smooth = new List<Vector3D> { path[0] };
        for (int i = 0; i < path.Count - 1; i++)
        {
            smooth.Add(Unit(path[i] * 0.75 + path[i + 1] * 0.25));
            smooth.Add(Unit(path[i] * 0.25 + path[i + 1] * 0.75));
        }

        smooth.Add(path[^1]);
        return smooth;
    }

    private static Vector3D Unit(Vector3D v) => v * (1 / v.Length);
}
