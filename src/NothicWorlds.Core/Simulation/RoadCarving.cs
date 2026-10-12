using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The ground shaped to the stone highways (VISION.md BOD-13): level with each road across its
/// paving and a shoulder either side, then cut back at <see cref="CutSlope"/> where the road
/// runs below the ground (a cutting) or banked down at <see cref="BankSlope"/> where it runs
/// above (a bank), until the slopes meet the ground. Under a bridge the ground is left as the
/// river cut it. As <see cref="RiverCarving"/>, it depends only on the roads, not on where an
/// eye is; the roads are drawn finely by a strip along each (<see cref="RoadStrips"/>), and
/// the coarser ground under it is sunk out of sight. Where roads meet, the nearest one shapes
/// the ground; past a road's ends it shapes nothing, as its strip ends square there.
/// </summary>
public sealed class RoadCarving
{
    /// <summary>How steeply a cutting's sides rise: meters up for each meter out.</summary>
    public const double CutSlope = 1;

    /// <summary>How steeply a bank's sides fall: meters down for each meter out.</summary>
    public const double BankSlope = 0.67;

    /// <summary>The level ground either side of the paving, in meters.</summary>
    public const double ShoulderMeters = 1;

    // How much farther than its height needs a road's sides are let reach, in meters (the
    // ground beside it is rougher than its profile's smooth ground).
    private const double SlackMeters = 5;

    // How far coarser ground is sunk under the strip, as RiverCarving's.
    private const double MinSinkMeters = 0.5, SinkPerCell = 0.4;

    // Coarser ground is shaped only for roads whose sides reach out at least this share of the
    // width of its cells (farther off, they're too narrow to see).
    private const double LeastShareOfCell = 0.02;

    // How many segments (or groups) each group of the search tree holds.
    private const int GroupSize = 8;

    // The segments near a point, gathered afresh for each (one list for each thread).
    [ThreadStatic]
    private static List<(int Road, int Segment)>? _near;

    // Each road's nearest of them (NearestEach), likewise.
    [ThreadStatic]
    private static List<Closeness>? _nearest;

    private readonly double _radiusMeters;
    private readonly IReadOnlyList<RoadProfile> _roads;
    private readonly bool[][] _bridged;  // Whether each road's segments are on a bridge
    private readonly Level[][] _trees;   // Each road's search tree, its segments first

    private RoadCarving(IReadOnlyList<RoadProfile> roads, double radiusMeters)
    {
        (_roads, _radiusMeters) = (roads, radiusMeters);
        _bridged = [.. roads.Select(Bridged)];
        _trees = [.. roads.Select(Tree)];
    }

    /// <summary>Whether there are no roads to shape the ground.</summary>
    public bool IsEmpty => _roads.Count == 0;

    /// <summary>The roads shaped by.</summary>
    public IReadOnlyList<RoadProfile> Roads => _roads;

    /// <summary>The ground shaped to <paramref name="roads"/> on a body this big.</summary>
    public static RoadCarving For(IReadOnlyList<RoadProfile> roads, double radiusKm) =>
        new(roads, radiusKm * 1000);

    /// <summary>
    /// How far out a road's sides reach from its middle where it runs
    /// <paramref name="heightMeters"/> above or below the ground, in meters, for paving
    /// <paramref name="halfWidthMeters"/> to each side.
    /// </summary>
    public static double ReachMeters(double halfWidthMeters, double heightMeters) =>
        halfWidthMeters + ShoulderMeters + Math.Abs(heightMeters) / BankSlope + SlackMeters;

    /// <summary>
    /// How far past a road's sides coarser ground with cells this wide (in meters) is sunk
    /// (fading out), in meters: the strip over it must reach at least this far.
    /// </summary>
    public static double SinkReachMeters(double cellMeters) => cellMeters;

    /// <summary>
    /// The ground's height at <paramref name="direction"/> (a unit direction) shaped to the
    /// roads, given its height there, <paramref name="groundMeters"/>.
    /// </summary>
    public double Shape(Vector3D direction, double groundMeters) =>
        Shape(Gather(direction, 0, 0), direction, groundMeters, cellMeters: null).Shaped;

    /// <summary>
    /// For coarser ground with cells <paramref name="cellMeters"/> wide within
    /// <paramref name="radius"/> (in radii) of <paramref name="center"/>: at a unit direction,
    /// given the ground's height there, its height shaped (<see cref="Shape"/>) and how far
    /// it's sunk out of sight under the strip that draws the roads finely, fading out over
    /// <see cref="SinkReachMeters"/> past their sides. Null if no road comes near, or none is
    /// wide enough to show on cells that wide. Safe to call from several threads at once.
    /// </summary>
    public Func<Vector3D, double, (double Shaped, double Sink)>? PartsNear(Vector3D center,
        double radius, double cellMeters)
    {
        double margin = SinkReachMeters(cellMeters) / _radiusMeters;
        double leastReach = LeastShareOfCell * cellMeters;
        if (Gather(center * (1 / center.Length), radius + margin, leastReach).Count == 0)
        {
            return null;
        }

        return (direction, groundMeters) => Shape(Gather(direction, margin, leastReach),
            direction, groundMeters, cellMeters);
    }

    /// <summary>
    /// How far a unit direction is past the nearest road's sides, in meters (0 on them or the
    /// road), looking no farther than <paramref name="withinMeters"/> (which it gives if no
    /// road is nearer). Safe to call from several threads at once.
    /// </summary>
    public double BeyondSidesMeters(Vector3D direction, double withinMeters)
    {
        double nearest = withinMeters;
        foreach (Closeness near in NearestEach(Gather(direction, withinMeters / _radiusMeters,
            0), direction))
        {
            nearest = Math.Min(nearest,
                Math.Max(0, near.Away - _trees[near.Road][0].ReachMeters[near.Segment]));
        }

        return nearest;
    }

    /// <summary>
    /// The height of the road on a bridge at a unit direction, in meters, if it's on one
    /// (between its walls); else null. Walkers cross on it (VISION.md BOD-13).
    /// </summary>
    public double? DeckMeters(Vector3D direction)
    {
        if (Closest(NearestEach(Gather(direction, 0, 0), direction)) is not { } closest)
        {
            return null;
        }

        (int r, int s, double away, double t) = closest;
        if (!_bridged[r][s] || away > _roads[r].HalfWidthMeters)
        {
            return null;
        }

        return Lerp(_roads[r].RoadMeters[s], _roads[r].RoadMeters[s + 1], t);
    }

    // Whether segment s of road r is on a bridge.
    internal bool IsBridged(int r, int s) => _bridged[r][s];

    // How far the sides of segment s of road r reach out from its middle, in meters.
    internal double SegmentReachMeters(int r, int s) => _trees[r][0].ReachMeters[s];

    // The ground shaped by the nearest of the segments given, and how far it's sunk (with
    // cells `cellMeters` wide) under the strip.
    private (double Shaped, double Sink) Shape(List<(int Road, int Segment)> segments,
        Vector3D direction, double groundMeters, double? cellMeters)
    {
        List<Closeness> each = NearestEach(segments, direction);
        double sink = 0;
        if (cellMeters is double cell)
        {
            double fade = SinkReachMeters(cell), cover = 0;
            foreach (Closeness near in each)
            {
                if (!_bridged[near.Road][near.Segment])
                {
                    double beyond = near.Away - _trees[near.Road][0].ReachMeters[near.Segment];
                    cover = Math.Max(cover, Math.Clamp(1 - beyond / fade, 0, 1));
                }
            }

            sink = cover * (MinSinkMeters + SinkPerCell * cell);
        }

        if (Closest(each) is not { } closest
            || _bridged[closest.Road][closest.Segment])
        {
            return (groundMeters, sink);
        }

        (int road, int segment, double distance, double along) = closest;

        RoadProfile profile = _roads[road];
        double height = Lerp(profile.RoadMeters[segment], profile.RoadMeters[segment + 1], along);
        double past = distance - profile.HalfWidthMeters - ShoulderMeters;
        if (past <= 0)
        {
            return (height, sink);
        }

        double cut = height + past * CutSlope, bank = height - past * BankSlope;
        return (Math.Clamp(groundMeters, bank, cut), sink);
    }

    // The nearest of the roads' nearest segments (NearestEach) whose sides reach the
    // direction they were measured from; null if none.
    private (int Road, int Segment, double Away, double T)? Closest(List<Closeness> each)
    {
        (int, int, double, double)? best = null;
        double nearest = double.PositiveInfinity;
        foreach (Closeness near in each)
        {
            if (near.Away < nearest
                && near.Away <= _trees[near.Road][0].ReachMeters[near.Segment])
            {
                (nearest, best) = (near.Away, (near.Road, near.Segment, near.Away, near.T));
            }
        }

        return best;
    }

    // Each road's nearest of the segments given (gathered road by road) to a unit direction;
    // none for a road the direction is past an end of (its nearest point is that end, and it
    // lies beyond it), where the road shapes nothing, as its strip ends square there. The list
    // is reused by the next call on the same thread.
    private List<Closeness> NearestEach(List<(int Road, int Segment)> segments, Vector3D point)
    {
        List<Closeness> each = _nearest ??= [];
        each.Clear();
        Closeness? best = null;
        double bestRaw = 0;
        foreach ((int r, int s) in segments)
        {
            Vector3D from = _roads[r].Points[s], along = _roads[r].Points[s + 1] - from;
            double lengthSquared = along.Dot(along);
            double raw = lengthSquared > 0 ? (point - from).Dot(along) / lengthSquared : 0.5;
            double t = Math.Clamp(raw, 0, 1);
            var near = new Closeness(r, s, (point - (from + along * t)).Length * _radiusMeters,
                t);
            if (best is not { } known || known.Road != r)
            {
                AddUnlessPastEnd(each, best, bestRaw);
                (best, bestRaw) = (near, raw);
            }
            else if (near.Away < known.Away)
            {
                (best, bestRaw) = (near, raw);
            }
        }

        AddUnlessPastEnd(each, best, bestRaw);
        return each;
    }

    // Adds a road's nearest segment, unless the point it was measured from is past the road's
    // ends (`raw`: how far along the segment its nearest point would be, unclamped).
    private void AddUnlessPastEnd(List<Closeness> each, Closeness? nearest, double raw)
    {
        if (nearest is { } near && !(near.Segment == 0 && raw < 0)
            && !(near.Segment == _roads[near.Road].Points.Count - 2 && raw > 1))
        {
            each.Add(near);
        }
    }

    // The segments whose sides reach within `margin` radii of a unit direction, of those
    // reaching out at least `leastReach` meters: down each road's tree, skipping groups that
    // can't. The list is reused by the next gathering on the same thread.
    private List<(int Road, int Segment)> Gather(Vector3D point, double margin,
        double leastReach)
    {
        List<(int Road, int Segment)> near = _near ??= [];
        near.Clear();
        for (int r = 0; r < _trees.Length; r++)
        {
            Level[] tree = _trees[r];
            Visit(tree, tree.Length - 1, 0, r, point, margin, leastReach, near);
        }

        return near;
    }

    private void Visit(Level[] tree, int level, int index, int road, Vector3D point,
        double margin, double leastReach, List<(int Road, int Segment)> near)
    {
        Level nodes = tree[level];
        if (nodes.ReachMeters[index] < leastReach || (point - nodes.Centers[index]).Length
            - nodes.Radii[index] > margin + nodes.ReachMeters[index] / _radiusMeters)
        {
            return;
        }

        if (level == 0)
        {
            near.Add((road, index));
            return;
        }

        int end = Math.Min((index + 1) * GroupSize, tree[level - 1].Centers.Length);
        for (int child = index * GroupSize; child < end; child++)
        {
            Visit(tree, level - 1, child, road, point, margin, leastReach, near);
        }
    }

    // Whether each of a road's segments is on one of its bridges.
    private static bool[] Bridged(RoadProfile road)
    {
        var bridged = new bool[road.Points.Count - 1];
        foreach (RoadBridge bridge in road.Bridges)
        {
            for (int s = bridge.First; s < bridge.Last; s++)
            {
                bridged[s] = true;
            }
        }

        return bridged;
    }

    // A road's search tree: balls around its segments (with how far their sides reach out),
    // then around groups of them, up to one around the whole road.
    private static Level[] Tree(RoadProfile road)
    {
        int segments = road.Points.Count - 1;
        var centers = new Vector3D[segments];
        var radii = new double[segments];
        var reach = new double[segments];
        for (int s = 0; s < segments; s++)
        {
            Vector3D from = road.Points[s], to = road.Points[s + 1];
            centers[s] = (from + to) * 0.5;
            radii[s] = (to - from).Length / 2;
            double height = Math.Max(Math.Abs(road.RoadMeters[s] - road.GroundMeters[s]),
                Math.Abs(road.RoadMeters[s + 1] - road.GroundMeters[s + 1]));
            reach[s] = ReachMeters(road.HalfWidthMeters, height);
        }

        List<Level> levels = [new Level(centers, radii, reach)];
        while (levels[^1].Centers.Length > 1)
        {
            levels.Add(Grouped(levels[^1]));
        }

        return [.. levels];
    }

    // The level above: a ball around each group of the level's balls.
    private static Level Grouped(Level below)
    {
        int count = (below.Centers.Length + GroupSize - 1) / GroupSize;
        var centers = new Vector3D[count];
        var radii = new double[count];
        var reach = new double[count];
        for (int g = 0; g < count; g++)
        {
            int start = g * GroupSize, end = Math.Min(start + GroupSize, below.Centers.Length);
            Vector3D sum = Vector3D.Zero;
            for (int i = start; i < end; i++)
            {
                sum += below.Centers[i];
            }

            centers[g] = sum * (1.0 / (end - start));
            for (int i = start; i < end; i++)
            {
                radii[g] = Math.Max(radii[g],
                    (below.Centers[i] - centers[g]).Length + below.Radii[i]);
                reach[g] = Math.Max(reach[g], below.ReachMeters[i]);
            }
        }

        return new Level(centers, radii, reach);
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    // How near a point is to a segment of a road: which road and segment, how far away it is
    // in meters, and how far along it its nearest point is (0 to 1).
    private readonly record struct Closeness(int Road, int Segment, double Away, double T);

    // One level of a road's search tree: each ball's middle and radius (in radii), and how far
    // the road's sides inside it reach out past it (in meters).
    private sealed record Level(Vector3D[] Centers, double[] Radii, double[] ReachMeters);
}
