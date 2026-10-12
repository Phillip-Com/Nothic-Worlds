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

    private RoadProfile(Vector3D[] points, double[] along, double halfWidth, double shoulder,
        double[] road, double[] ground, List<RoadBridge> bridges, RoadSurface surface)
    {
        (Points, AlongMeters, HalfWidthMeters, ShoulderMeters, RoadMeters, GroundMeters) =
            (points, along, halfWidth, shoulder, road, ground);
        (Bridges, Surface) = (bridges, surface);
    }

    /// <summary>
    /// The way, as unit directions, first point first, at most <see cref="SampleMeters"/>
    /// apart.
    /// </summary>
    public IReadOnlyList<Vector3D> Points { get; }

    /// <summary>How far each point is from the first, in meters.</summary>
    public IReadOnlyList<double> AlongMeters { get; }

    /// <summary>Half the paving's (or worn ground's) width, in meters.</summary>
    public double HalfWidthMeters { get; }

    /// <summary>The level ground either side of it, in meters.</summary>
    public double ShoulderMeters { get; }

    /// <summary>What it's surfaced with.</summary>
    public RoadSurface Surface { get; }

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
    /// crossing <paramref name="rivers"/> (none: null). Null if it isn't a stone highway (dirt
    /// paths are laid by <see cref="DirtPath"/>; cart tracks and old roads come later), or its
    /// way is a single point.
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

        double[] along = AlongOf(points, radiusMeters);
        double[] ground = [.. points.Select(groundMeters)];

        double[] road = Smoothed(ground, along);
        LimitGrade(road, along);
        for (int i = 0; i < count; i++)
        {
            road[i] = Math.Clamp(road[i], ground[i] - MaxCutMeters, ground[i] + MaxCutMeters);
            double end = Math.Min(along[i], along[^1] - along[i]);
            road[i] = ground[i] + (road[i] - ground[i]) * Math.Min(1, end / EndMeters);
        }

        List<RoadBridge> bridges = rivers is null
            ? []
            : BridgesOver(points, along, rivers, BridgeMarginMeters);
        RaiseForBridges(road, along, bridges, MaxGrade, bridge => bridge.WaterMeters
            + StoneBridge.ClearanceMeters(bridge.LengthMeters));
        return new RoadProfile(points, along, course.WidthMeters / 2,
            RoadCarving.ShoulderMeters, road, ground, bridges, RoadSurface.Paving);
    }

    /// <summary>
    /// A road laid as given: along <paramref name="points"/> (unit directions, first first,
    /// <paramref name="along"/> meters from the first), its surface
    /// <paramref name="halfWidthMeters"/> to each side with <paramref name="shoulderMeters"/>
    /// level either side, at <paramref name="roadMeters"/> over ground at
    /// <paramref name="groundMeters"/>, crossing <paramref name="bridges"/>.
    /// </summary>
    internal static RoadProfile Laid(Vector3D[] points, double[] along, double halfWidthMeters,
        double shoulderMeters, double[] roadMeters, double[] groundMeters,
        List<RoadBridge> bridges, RoadSurface surface) =>
        new(points, along, halfWidthMeters, shoulderMeters, roadMeters, groundMeters, bridges,
            surface);

    /// <summary>
    /// How far each of <paramref name="points"/> (unit directions) is from the first along
    /// them, in meters, on a body <paramref name="radiusMeters"/> in radius.
    /// </summary>
    internal static double[] AlongOf(IReadOnlyList<Vector3D> points, double radiusMeters)
    {
        var along = new double[points.Count];
        for (int i = 1; i < points.Count; i++)
        {
            along[i] = along[i - 1] + radiusMeters
                * Math.Acos(Math.Clamp(points[i - 1].Dot(points[i]), -1, 1));
        }

        return along;
    }

    // The ground averaged over SmoothingMeters either side of each point (fewer at the ends).
    private static double[] Smoothed(double[] ground, double[] along) =>
        Averaged(ground, along, SmoothingMeters);

    /// <summary>
    /// <paramref name="values"/> at points <paramref name="along"/> meters along a line, each
    /// averaged with those within <paramref name="halfWindowMeters"/> either side of it (fewer
    /// at the ends).
    /// </summary>
    internal static double[] Averaged(IReadOnlyList<double> values, IReadOnlyList<double> along,
        double halfWindowMeters)
    {
        int count = values.Count;
        var sums = new double[count + 1];
        for (int i = 0; i < count; i++)
        {
            sums[i + 1] = sums[i] + values[i];
        }

        var smooth = new double[count];
        int from = 0, to = 0;
        for (int i = 0; i < count; i++)
        {
            while (along[i] - along[from] > halfWindowMeters)
            {
                from++;
            }

            while (to < count - 1 && along[to + 1] - along[i] <= halfWindowMeters)
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

    /// <summary>
    /// The bridges over the rivers: each run of points over a river's water (or within
    /// <paramref name="marginMeters"/> of it), from the dry point before it to the dry point
    /// after, with the highest water, the lowest bed, and the widest water under it. Runs whose
    /// ends meet are one bridge.
    /// </summary>
    internal static List<RoadBridge> BridgesOver(IReadOnlyList<Vector3D> points,
        IReadOnlyList<double> along, RiverCarving rivers, double marginMeters)
    {
        var bridges = new List<RoadBridge>();
        int count = points.Count;
        for (int i = 0; i < count; i++)
        {
            if (rivers.WaterOver(points[i], marginMeters) is not { } first)
            {
                continue;
            }

            (double water, double bed, double width) = first;
            int end = i;
            while (end + 1 < count
                && rivers.WaterOver(points[end + 1], marginMeters) is { } next)
            {
                end++;
                (water, bed) = (Math.Max(water, next.WaterMeters), Math.Min(bed, next.BedMeters));
                width = Math.Max(width, next.WidthMeters);
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
                    WaterWidthMeters = Math.Max(before.WaterWidthMeters, width),
                };
            }
            else if (last > start)
            {
                bridges.Add(new RoadBridge(start, last, water, bed) { WaterWidthMeters = width });
            }

            i = end;
        }

        return [.. bridges.Select(bridge => bridge with
        {
            LengthMeters = along[bridge.Last] - along[bridge.First],
        })];
    }

    /// <summary>
    /// Lifts the road at each bridge's ends to at least <paramref name="deckMeters"/> (high
    /// enough to carry it over the water), with banks climbing to it at
    /// <paramref name="grade"/> (as high as they need: a highway bridge's approach isn't held
    /// to MaxCutMeters); between its ends the bridge runs straight.
    /// </summary>
    internal static void RaiseForBridges(double[] road, IReadOnlyList<double> along,
        List<RoadBridge> bridges, double grade, Func<RoadBridge, double> deckMeters)
    {
        if (bridges.Count == 0)
        {
            return;
        }

        var least = new double[road.Length];
        Array.Fill(least, double.NegativeInfinity);
        foreach (RoadBridge bridge in bridges)
        {
            double deck = deckMeters(bridge);
            least[bridge.First] = Math.Max(least[bridge.First], deck);
            least[bridge.Last] = Math.Max(least[bridge.Last], deck);
        }

        for (int i = 1; i < road.Length; i++)
        {
            least[i] = Math.Max(least[i], least[i - 1] - grade * (along[i] - along[i - 1]));
        }

        for (int i = road.Length - 2; i >= 0; i--)
        {
            least[i] = Math.Max(least[i], least[i + 1] - grade * (along[i + 1] - along[i]));
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
