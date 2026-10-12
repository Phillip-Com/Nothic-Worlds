using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A stone highway along its way, for drawing it up close (VISION.md BOD-13; owner's choices,
/// 2026-10-12): laid on an even grade no steeper than <see cref="MaxGrade"/>, cut into the
/// ground where it rises and banked up where it dips, by at most <see cref="MaxCutMeters"/>
/// (past that it follows the ground more steeply, until tunnels come), coming down to the
/// ground at its ends (over <see cref="EndMeters"/>), and carried over each
/// river on a stone bridge (<see cref="StoneBridge"/>) high enough for its arches, with banks
/// rising to it. In steps of <see cref="SampleMeters"/> along its way: its height, the
/// ground's under it, and where it's on a bridge. It depends only on the road, the ground,
/// and the rivers, never on where an eye is.
/// </summary>
public sealed class RoadProfile
{
    /// <summary>The step between the profile's points, in meters.</summary>
    public const double SampleMeters = 10;

    /// <summary>The steepest a highway climbs or falls: meters up for each meter along.</summary>
    public const double MaxGrade = 0.08;

    /// <summary>
    /// The deepest a highway cuts into the ground, or banks up over it, in meters.
    /// </summary>
    public const double MaxCutMeters = 15;

    /// <summary>
    /// How far from each end a road comes down to the ground, in meters (where its cutting or
    /// bank would be deepest, at about its grade again).
    /// </summary>
    public const double EndMeters = MaxCutMeters / MaxGrade;

    /// <summary>How far a bridge reaches past a river's water at each end, in meters.</summary>
    public const double BridgeMarginMeters = 2;

    // The ground is averaged over this far either side of each point before the grade is
    // laid, in meters, so the road doesn't follow every bump.
    private const double SmoothingMeters = 100;

    private RoadProfile(Vector3D[] points, double[] along, double halfWidth, double[] road,
        double[] ground, List<RoadBridge> bridges)
    {
        (Points, AlongMeters, HalfWidthMeters, RoadMeters, GroundMeters, Bridges) =
            (points, along, halfWidth, road, ground, bridges);
    }

    /// <summary>
    /// The way, as unit directions, first point first, at most <see cref="SampleMeters"/>
    /// apart.
    /// </summary>
    public IReadOnlyList<Vector3D> Points { get; }

    /// <summary>How far each point is from the first, in meters.</summary>
    public IReadOnlyList<double> AlongMeters { get; }

    /// <summary>Half the paving's width, in meters.</summary>
    public double HalfWidthMeters { get; }

    /// <summary>The height of the road's surface at each point, in meters.</summary>
    public IReadOnlyList<double> RoadMeters { get; }

    /// <summary>The (smooth) ground's height under each point, in meters.</summary>
    public IReadOnlyList<double> GroundMeters { get; }

    /// <summary>The bridges along it, first first.</summary>
    public IReadOnlyList<RoadBridge> Bridges { get; }

    /// <summary>
    /// Whether the segment from point <paramref name="segment"/> to the next is on a bridge.
    /// </summary>
    public bool OnBridge(int segment) =>
        Bridges.Any(bridge => segment >= bridge.First && segment < bridge.Last);

    /// <summary>
    /// The profile of <paramref name="course"/> on a body <paramref name="radiusKm"/> in
    /// radius, whose smooth ground is <paramref name="groundMeters"/> high at each direction,
    /// crossing <paramref name="rivers"/> (none: null). Null if it isn't a stone highway (the
    /// other styles come later), or its way is a single point.
    /// </summary>
    public static RoadProfile? For(RoadCourseShown course, double radiusKm,
        Func<Vector3D, double> groundMeters, RiverCarving? rivers)
    {
        if (course.Style != RoadStyle.StoneHighway || course.Path.Count < 2)
        {
            return null;
        }

        double radiusMeters = radiusKm * 1000;
        Vector3D[] points = RiverProfile.Sampled([.. course.Path], radiusMeters, SampleMeters);
        int count = points.Length;
        if (count < 2)
        {
            return null;
        }

        var along = new double[count];
        var ground = new double[count];
        for (int i = 0; i < count; i++)
        {
            along[i] = i == 0 ? 0 : along[i - 1] + radiusMeters
                * Math.Acos(Math.Clamp(points[i - 1].Dot(points[i]), -1, 1));
            ground[i] = groundMeters(points[i]);
        }

        double[] road = Smoothed(ground, along);
        LimitGrade(road, along);
        for (int i = 0; i < count; i++)
        {
            road[i] = Math.Clamp(road[i], ground[i] - MaxCutMeters, ground[i] + MaxCutMeters);
            double end = Math.Min(along[i], along[^1] - along[i]);
            road[i] = ground[i] + (road[i] - ground[i]) * Math.Min(1, end / EndMeters);
        }

        List<RoadBridge> bridges = rivers is null ? [] : BridgesOver(points, along, rivers);
        RaiseForBridges(road, along, bridges);
        return new RoadProfile(points, along, course.WidthMeters / 2, road, ground, bridges);
    }

    // The ground averaged over SmoothingMeters either side of each point (fewer at the ends).
    private static double[] Smoothed(double[] ground, double[] along)
    {
        int count = ground.Length;
        var sums = new double[count + 1];
        for (int i = 0; i < count; i++)
        {
            sums[i + 1] = sums[i] + ground[i];
        }

        var smooth = new double[count];
        int from = 0, to = 0;
        for (int i = 0; i < count; i++)
        {
            while (along[i] - along[from] > SmoothingMeters)
            {
                from++;
            }

            while (to < count - 1 && along[to + 1] - along[i] <= SmoothingMeters)
            {
                to++;
            }

            smooth[i] = (sums[to + 1] - sums[from]) / (to + 1 - from);
        }

        return smooth;
    }

    // Holds the road to MaxGrade: forward, then back, each point kept within the grade of the
    // one before it (the pass back leaves every pair within it).
    private static void LimitGrade(double[] road, double[] along)
    {
        for (int i = 1; i < road.Length; i++)
        {
            double most = MaxGrade * (along[i] - along[i - 1]);
            road[i] = Math.Clamp(road[i], road[i - 1] - most, road[i - 1] + most);
        }

        for (int i = road.Length - 2; i >= 0; i--)
        {
            double most = MaxGrade * (along[i + 1] - along[i]);
            road[i] = Math.Clamp(road[i], road[i + 1] - most, road[i + 1] + most);
        }
    }

    // The bridges over the rivers: each run of points over a river's water (or within
    // BridgeMarginMeters of it), from the dry point before it to the dry point after, with
    // the highest water and the lowest bed under it. Runs whose ends meet are one bridge.
    private static List<RoadBridge> BridgesOver(Vector3D[] points, double[] along,
        RiverCarving rivers)
    {
        var bridges = new List<RoadBridge>();
        int count = points.Length;
        for (int i = 0; i < count; i++)
        {
            if (rivers.WaterOver(points[i], BridgeMarginMeters) is not { } first)
            {
                continue;
            }

            (double water, double bed) = first;
            int end = i;
            while (end + 1 < count
                && rivers.WaterOver(points[end + 1], BridgeMarginMeters) is { } next)
            {
                end++;
                (water, bed) = (Math.Max(water, next.WaterMeters), Math.Min(bed, next.BedMeters));
            }

            int start = Math.Max(i - 1, 0), last = Math.Min(end + 1, count - 1);
            if (bridges.Count > 0 && bridges[^1].Last >= start)
            {
                RoadBridge before = bridges[^1];
                bridges[^1] = before with
                {
                    Last = last,
                    WaterMeters = Math.Max(before.WaterMeters, water),
                    BedMeters = Math.Min(before.BedMeters, bed),
                };
            }
            else if (last > start)
            {
                bridges.Add(new RoadBridge(start, last, water, bed));
            }

            i = end;
        }

        return [.. bridges.Select(bridge => bridge with
        {
            LengthMeters = along[bridge.Last] - along[bridge.First],
        })];
    }

    // Lifts the road at each bridge's ends to carry its arches over the water, with banks
    // climbing to it at MaxGrade (as high as they need: a bridge's approach isn't held to
    // MaxCutMeters); between its ends the bridge runs straight.
    private static void RaiseForBridges(double[] road, double[] along, List<RoadBridge> bridges)
    {
        if (bridges.Count == 0)
        {
            return;
        }

        var least = new double[road.Length];
        Array.Fill(least, double.NegativeInfinity);
        foreach (RoadBridge bridge in bridges)
        {
            double deck = bridge.WaterMeters + StoneBridge.ClearanceMeters(bridge.LengthMeters);
            least[bridge.First] = Math.Max(least[bridge.First], deck);
            least[bridge.Last] = Math.Max(least[bridge.Last], deck);
        }

        for (int i = 1; i < road.Length; i++)
        {
            least[i] = Math.Max(least[i], least[i - 1] - MaxGrade * (along[i] - along[i - 1]));
        }

        for (int i = road.Length - 2; i >= 0; i--)
        {
            least[i] = Math.Max(least[i], least[i + 1] - MaxGrade * (along[i + 1] - along[i]));
        }

        for (int i = 0; i < road.Length; i++)
        {
            road[i] = Math.Max(road[i], least[i]);
        }

        foreach (RoadBridge bridge in bridges)
        {
            double start = road[bridge.First], end = road[bridge.Last];
            for (int i = bridge.First + 1; i < bridge.Last; i++)
            {
                double t = (along[i] - along[bridge.First])
                    / Math.Max(bridge.LengthMeters, 1e-9);
                road[i] = start + (end - start) * t;
            }
        }
    }
}
