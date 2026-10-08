using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A river's water along its course, for drawing it up close (VISION.md BOD-11; owner's
/// choices: banks carved up close, depth from width, ripples and rapids). At each point of
/// its drawn course (<see cref="RiverLine"/>): how wide and deep it is, the height of its
/// water and of its bed, how fast it flows, and how white with rapids it is. The water never
/// runs uphill: where the ground rises ahead, it keeps its level and cuts down through it.
/// </summary>
public sealed class RiverProfile
{
    /// <summary>
    /// A river's depth for its width (a twentieth), and the shallowest and deepest, in meters.
    /// </summary>
    public const double DepthPerWidth = 0.05, MinDepthMeters = 1, MaxDepthMeters = 20;

    /// <summary>
    /// How far below the ground at its middle a river's water lies, as a share of its depth.
    /// </summary>
    public const double WaterBelowGround = 0.35;

    /// <summary>The slowest and fastest a river flows, in meters a second.</summary>
    public const double MinFlow = 0.3, MaxFlow = 6;

    /// <summary>The slopes where rapids start and where they're white all across.</summary>
    public const double RapidsStart = 0.004, RapidsFull = 0.03;

    // Slopes are measured over at least this far either side of a point, in meters.
    private const double SlopeSpanMeters = 400;

    private RiverProfile(Vector3D[] points, double[] along, double[] halfWidth,
        double[] water, double[] bed, double[] flow, double[] rapids)
    {
        (Points, AlongMeters, HalfWidthMeters, WaterMeters, BedMeters) =
            (points, along, halfWidth, water, bed);
        (FlowMetersPerSecond, Rapids) = (flow, rapids);
    }

    /// <summary>The course, as unit directions, source first.</summary>
    public IReadOnlyList<Vector3D> Points { get; }

    /// <summary>How far each point is from the source, in meters.</summary>
    public IReadOnlyList<double> AlongMeters { get; }

    /// <summary>Half the river's width at each point, in meters.</summary>
    public IReadOnlyList<double> HalfWidthMeters { get; }

    /// <summary>The height of the water's surface at each point, in meters.</summary>
    public IReadOnlyList<double> WaterMeters { get; }

    /// <summary>The height of the river's bed at each point, in meters.</summary>
    public IReadOnlyList<double> BedMeters { get; }

    /// <summary>How fast the water flows at each point, in meters a second.</summary>
    public IReadOnlyList<double> FlowMetersPerSecond { get; }

    /// <summary>How white with rapids the water is at each point, from 0 (calm) to 1.</summary>
    public IReadOnlyList<double> Rapids { get; }

    /// <summary>A river's depth where it's this wide, in meters.</summary>
    public static double DepthFor(double widthMeters) =>
        Math.Clamp(widthMeters * DepthPerWidth, MinDepthMeters, MaxDepthMeters);

    /// <summary>
    /// The profile of <paramref name="course"/> on a body <paramref name="radiusKm"/> in
    /// radius, whose ground is <paramref name="groundMeters"/> high at each direction. Null
    /// if its course is a single point.
    /// </summary>
    public static RiverProfile? For(RiverCourseShown course, double radiusKm,
        Func<Vector3D, double> groundMeters)
    {
        Vector3D[] points = [.. RiverLine.PathOf(course)];
        int count = points.Length;
        if (count < 2)
        {
            return null;
        }

        double radiusMeters = radiusKm * 1000;
        var along = new double[count];
        for (int i = 1; i < count; i++)
        {
            along[i] = along[i - 1] + radiusMeters
                * Math.Acos(Math.Clamp(points[i - 1].Dot(points[i]), -1, 1));
        }

        double length = Math.Max(along[^1], 1e-9);
        var halfWidth = new double[count];
        var water = new double[count];
        var bed = new double[count];
        for (int i = 0; i < count; i++)
        {
            // From a fifth of its width at the source to all of it at the mouth.
            double width = course.WidthKm * 1000 * (0.2 + 0.8 * along[i] / length);
            double depth = DepthFor(width);
            halfWidth[i] = width / 2;
            water[i] = groundMeters(points[i]) - WaterBelowGround * depth;
            if (i > 0)
            {
                water[i] = Math.Min(water[i], water[i - 1]);
            }

            bed[i] = water[i] - (1 - WaterBelowGround) * depth;
        }

        var flow = new double[count];
        var rapids = new double[count];
        for (int i = 0; i < count; i++)
        {
            double slope = SlopeAt(i, along, water);
            flow[i] = Math.Clamp(MinFlow + 25 * Math.Sqrt(slope), MinFlow, MaxFlow);
            rapids[i] = SmoothStep(RapidsStart, RapidsFull, slope);
        }

        return new RiverProfile(points, along, halfWidth, water, bed, flow, rapids);
    }

    // How steeply the water falls around a point: the drop over the points at least
    // SlopeSpanMeters either side (or the ends), per meter.
    private static double SlopeAt(int i, double[] along, double[] water)
    {
        int before = i, after = i;
        while (before > 0 && along[i] - along[before] < SlopeSpanMeters)
        {
            before--;
        }

        while (after < along.Length - 1 && along[after] - along[i] < SlopeSpanMeters)
        {
            after++;
        }

        double span = along[after] - along[before];
        return span > 0 ? (water[before] - water[after]) / span : 0;
    }

    private static double SmoothStep(double from, double to, double x)
    {
        double t = Math.Clamp((x - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
