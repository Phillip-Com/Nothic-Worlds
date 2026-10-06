using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Maps;

/// <summary>
/// Snapping map pieces to the latitude and longitude grid (VISION.md MAP-02; owner's choice:
/// a piece's center or its edges snap, to a step of the grid that can be chosen).
/// </summary>
/// <remarks>
/// Grid lines are every <c>step</c> degrees from the equator and the prime meridian, as the
/// globe draws them (every 15° there, so 15°, 5°, and 1° steps all meet its lines).
/// </remarks>
public static class GridSnap
{
    // At most this many refining moves; it stops as soon as the points are on their lines.
    private const int RefinePasses = 50;

    /// <summary>The steps offered, in degrees: the drawn grid, then finer ones.</summary>
    public static readonly IReadOnlyList<double> Steps = [15.0, 5.0, 1.0];

    // Where a piece's center and the middles of its four edges are, in its bounding box.
    private static readonly (double U, double V)[] _snapPoints =
        [(0.5, 0.5), (0.0, 0.5), (1.0, 0.5), (0.5, 0.0), (0.5, 1.0)];

    /// <summary>The grid crossing nearest <paramref name="spot"/>.</summary>
    /// <param name="spot">Where to start from.</param>
    /// <param name="stepDegrees">The grid's spacing, more than 0 and at most 90.</param>
    /// <exception cref="ArgumentOutOfRangeException">The step is out of range.</exception>
    public static GeoCoordinate NearestCrossing(GeoCoordinate spot, double stepDegrees)
    {
        RequireStep(stepDegrees);
        return Shifted(spot,
            Nudge(spot.LatitudeDegrees, stepDegrees),
            Nudge(spot.LongitudeDegrees, stepDegrees));
    }

    /// <summary>
    /// Where to move a piece's center so that it, or the middle of one of its edges, lies on a
    /// grid line: north–south and east–west are snapped separately, each by whichever of those
    /// points is nearest a line, so the piece moves as little as it can.
    /// </summary>
    /// <param name="center">Where the piece's center is now.</param>
    /// <param name="rotationDegrees">The piece's clockwise turn.</param>
    /// <param name="widthDegrees">The arc the piece spans, left to right.</param>
    /// <param name="boxAspectRatio">The piece's width divided by its height.</param>
    /// <param name="stepDegrees">The grid's spacing, more than 0 and at most 90.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A size, angle, or the step is invalid.
    /// </exception>
    public static GeoCoordinate SnapPiece(GeoCoordinate center, double rotationDegrees,
        double widthDegrees, double boxAspectRatio, double stepDegrees)
    {
        RequireStep(stepDegrees);
        List<GeoCoordinate> points =
            SnapPointsOf(center, rotationDegrees, widthDegrees, boxAspectRatio);
        int northPoint = Nearest(points.Select(p => Nudge(p.LatitudeDegrees, stepDegrees)));
        int eastPoint = Nearest(points.Select(p => Nudge(p.LongitudeDegrees, stepDegrees)));
        double northTarget = points[northPoint].LatitudeDegrees
            + Nudge(points[northPoint].LatitudeDegrees, stepDegrees);
        double eastTarget = points[eastPoint].LongitudeDegrees
            + Nudge(points[eastPoint].LongitudeDegrees, stepDegrees);

        // On a large or turned piece, moving the center doesn't move its edges by exactly as
        // much, so the move is refined until the chosen points sit on their lines.
        for (int pass = 0; pass < RefinePasses; pass++)
        {
            double north = northTarget - points[northPoint].LatitudeDegrees;
            double east = SphericalCoordinates.LongitudeDelta(
                points[eastPoint].LongitudeDegrees, eastTarget);
            if (Math.Abs(north) < 1e-9 && Math.Abs(east) < 1e-9)
            {
                break;
            }

            center = Shifted(center, north, east);
            points = SnapPointsOf(center, rotationDegrees, widthDegrees, boxAspectRatio);
        }

        return center;
    }

    private static List<GeoCoordinate> SnapPointsOf(GeoCoordinate center,
        double rotationDegrees, double widthDegrees, double boxAspectRatio)
    {
        var projection = new PieceProjection(
            center, rotationDegrees, widthDegrees, boxAspectRatio);
        return [.. _snapPoints.Select(point => projection.FromBoxPosition(point.U, point.V))];
    }

    // How far a value is from the nearest multiple of the step (signed, to reach it).
    private static double Nudge(double degrees, double step) =>
        Math.Round(degrees / step) * step - degrees;

    // Which of the nudges is smallest (the first, on a tie).
    private static int Nearest(IEnumerable<double> nudges) =>
        nudges.Select((nudge, index) => (Size: Math.Abs(nudge), index)).MinBy(n => n.Size).index;

    // GeoCoordinate keeps the latitude within the poles and wraps the longitude.
    private static GeoCoordinate Shifted(GeoCoordinate spot, double north, double east) =>
        new(spot.LatitudeDegrees + north, spot.LongitudeDegrees + east);

    private static void RequireStep(double stepDegrees)
    {
        if (!double.IsFinite(stepDegrees) || stepDegrees is <= 0 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(stepDegrees), stepDegrees,
                "A grid step must be more than 0° and at most 90°.");
        }
    }
}
