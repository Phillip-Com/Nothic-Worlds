using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The channels rivers cut into the ground (VISION.md BOD-11; owner's choices: carved at every
/// distance, following the ground): a bed as wide as each river, with banks rising to its
/// water's edges (more steeply where its bed is set deeper) and on at <see cref="BankSlope"/>
/// to meet the ground. It depends only on the rivers, not on where an
/// eye is, so ground built with it stays right however the eye moves. The channels are drawn
/// finely by a strip along each river (<see cref="RiverBanks"/>); the coarser ground under it
/// is also sunk out of sight (<see cref="UnderStripNear"/>), by more where its cells are wider,
/// unless its cells are so wide that the river would be lost in one.
/// </summary>
public sealed class RiverCarving
{
    /// <summary>How steeply the banks rise: meters up for each meter out.</summary>
    public const double BankSlope = 0.5;

    /// <summary>The widest a bank reaches out from the water, in meters.</summary>
    public const double MaxBankMeters = 200;

    // How far coarser ground is sunk under the strip: this, plus this share of the width of
    // its cells, in meters (more than its cells' flat faces can bulge by, and more than a
    // river narrower than a cell is deep).
    private const double MinSinkMeters = 0.5, SinkPerCell = 0.4;

    // Coarser ground is carved only for rivers whose channels and banks reach out at least
    // this share of the width of its cells (farther off, they're too narrow to see).
    private const double LeastShareOfCell = 0.02;

    // How many segments (or groups) each group of the search tree holds.
    private const int GroupSize = 8;

    // The segments near a point, gathered afresh for each (one list for each thread).
    [ThreadStatic]
    private static List<(int River, int Segment)>? _near;

    private readonly double _radiusMeters;
    private readonly IReadOnlyList<RiverProfile> _rivers;
    private readonly Level[][] _trees;  // Each river's search tree, its segments first

    private RiverCarving(IReadOnlyList<RiverProfile> rivers, double radiusMeters)
    {
        (_rivers, _radiusMeters) = (rivers, radiusMeters);
        _trees = [.. rivers.Select(Tree)];
    }

    /// <summary>Whether there are no rivers to carve.</summary>
    public bool IsEmpty => _rivers.Count == 0;

    /// <summary>The channels of <paramref name="rivers"/> on a body this big.</summary>
    public static RiverCarving For(IReadOnlyList<RiverProfile> rivers, double radiusKm) =>
        new(rivers, radiusKm * 1000);

    /// <summary>How far a river's banks may reach out from its water, in meters.</summary>
    public static double BankWidthMeters(double halfWidthMeters) =>
        Math.Min(MaxBankMeters, 20 + halfWidthMeters);

    /// <summary>
    /// How far past a river's banks coarser ground with cells this wide (in meters) is sunk
    /// (fading out), in meters: the strip over it must reach at least this far.
    /// </summary>
    public static double SinkReachMeters(double cellMeters) => cellMeters;

    /// <summary>
    /// The ground's height at <paramref name="direction"/> (a unit direction) with the rivers'
    /// channels cut into it, given its height there, <paramref name="groundMeters"/>.
    /// </summary>
    public double Carve(Vector3D direction, double groundMeters)
    {
        List<(int River, int Segment)> near = Gather(direction, 0, 0);
        return Shape(near, direction, groundMeters, cellMeters: null);
    }

    /// <summary>
    /// For coarser ground with cells <paramref name="cellMeters"/> wide within
    /// <paramref name="radius"/> (in radii) of <paramref name="center"/>: its height at a unit
    /// direction, given the ground's height there, carved (<see cref="Carve"/>) and sunk out
    /// of sight under the strip that draws the channels finely, fading out over
    /// <see cref="SinkReachMeters"/> past the banks. Null if no river comes near, or none is
    /// wide enough to show on cells that wide. Safe to call from several threads at once.
    /// </summary>
    public Func<Vector3D, double, double>? UnderStripNear(Vector3D center, double radius,
        double cellMeters)
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

    // The ground carved by the segments given, and sunk (with cells `cellMeters` wide) under
    // the strip: by more under banks steeper than BankSlope below the water, which its cells'
    // flat faces would otherwise poke through.
    private double Shape(List<(int River, int Segment)> near, Vector3D direction,
        double groundMeters, double? cellMeters)
    {
        double fade = cellMeters is double cell ? SinkReachMeters(cell) : 0;
        double carved = groundMeters, cover = 0, steeper = 0;
        foreach ((int r, int s) in near)
        {
            RiverProfile river = _rivers[r];
            (double away, double t) = Nearest(direction, river.Points[s], river.Points[s + 1]);
            double halfWidth = Lerp(river.HalfWidthMeters[s], river.HalfWidthMeters[s + 1], t);
            double beyond = away - halfWidth - BankWidthMeters(halfWidth);
            if (fade > 0)
            {
                double covered = Math.Clamp(1 - beyond / fade, 0, 1);
                cover = Math.Max(cover, covered);
                steeper = Math.Max(steeper, covered * DeeperThanSlope(river, s, t, halfWidth));
            }

            if (beyond > 0)
            {
                continue;
            }

            carved = Math.Min(carved, Across(river, s, t, away, halfWidth));
        }

        return cellMeters is double width
            ? carved - cover * (MinSinkMeters + SinkPerCell * width) - steeper
            : carved;
    }

    // How much deeper a river's bed is, `t` of the way along segment s, than banks rising at
    // BankSlope from it to its water's edges would make it, in meters (0 for Auto's depth).
    private static double DeeperThanSlope(RiverProfile river, int s, double t, double halfWidth)
    {
        double bed = Lerp(river.BedMeters[s], river.BedMeters[s + 1], t);
        double water = Lerp(river.WaterMeters[s], river.WaterMeters[s + 1], t);
        double edge = Lerp(river.WaterHalfWidthMeters[s], river.WaterHalfWidthMeters[s + 1], t);
        return Math.Max(0, water - bed - (edge - halfWidth) * BankSlope);
    }

    // The height of a river's channel `away` meters from its middle, `t` of the way along
    // segment s: its bed, then a bank rising to its water's edge and on at BankSlope.
    private static double Across(RiverProfile river, int s, double t, double away,
        double halfWidth)
    {
        double bed = Lerp(river.BedMeters[s], river.BedMeters[s + 1], t);
        if (away <= halfWidth)
        {
            return bed;
        }

        double water = Lerp(river.WaterMeters[s], river.WaterMeters[s + 1], t);
        double edge = Lerp(river.WaterHalfWidthMeters[s], river.WaterHalfWidthMeters[s + 1], t);
        return away >= edge || edge <= halfWidth
            ? water + (away - edge) * BankSlope
            : bed + (water - bed) * (away - halfWidth) / (edge - halfWidth);
    }

    // How far a unit direction is from the segment between two others, in meters, and how far
    // along it its nearest point is (0 to 1). The segments are short enough (RiverProfile's
    // SampleMeters) for the straight line between their ends to stand for the globe's curve.
    private (double Away, double T) Nearest(Vector3D point, Vector3D from, Vector3D to)
    {
        Vector3D along = to - from;
        double lengthSquared = along.Dot(along);
        double t = lengthSquared > 0
            ? Math.Clamp((point - from).Dot(along) / lengthSquared, 0, 1)
            : 0;
        return ((point - (from + along * t)).Length * _radiusMeters, t);
    }

    // The segments whose channels and banks reach within `margin` radii of a unit direction,
    // of those reaching out at least `leastReach` meters: down each river's tree, skipping
    // groups that can't. The list is reused by the next gathering on the same thread.
    private List<(int River, int Segment)> Gather(Vector3D point, double margin,
        double leastReach)
    {
        List<(int River, int Segment)> near = _near ??= [];
        near.Clear();
        for (int r = 0; r < _trees.Length; r++)
        {
            Level[] tree = _trees[r];
            Visit(tree, tree.Length - 1, 0, r, point, margin, leastReach, near);
        }

        return near;
    }

    private void Visit(Level[] tree, int level, int index, int river, Vector3D point,
        double margin, double leastReach, List<(int River, int Segment)> near)
    {
        Level nodes = tree[level];
        if (nodes.ReachMeters[index] < leastReach || (point - nodes.Centers[index]).Length
            - nodes.Radii[index] > margin + nodes.ReachMeters[index] / _radiusMeters)
        {
            return;
        }

        if (level == 0)
        {
            near.Add((river, index));
            return;
        }

        int end = Math.Min((index + 1) * GroupSize, tree[level - 1].Centers.Length);
        for (int child = index * GroupSize; child < end; child++)
        {
            Visit(tree, level - 1, child, river, point, margin, leastReach, near);
        }
    }

    // A river's search tree: balls around its segments (with how far their channels and banks
    // reach out), then around groups of them, up to one around the whole river.
    private static Level[] Tree(RiverProfile river)
    {
        int segments = river.Points.Count - 1;
        var centers = new Vector3D[segments];
        var radii = new double[segments];
        var reach = new double[segments];
        for (int s = 0; s < segments; s++)
        {
            Vector3D from = river.Points[s], to = river.Points[s + 1];
            centers[s] = (from + to) * 0.5;
            radii[s] = (to - from).Length / 2;
            double halfWidth = Math.Max(river.HalfWidthMeters[s], river.HalfWidthMeters[s + 1]);
            reach[s] = halfWidth + BankWidthMeters(halfWidth);
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

    // One level of a river's search tree: each ball's middle and radius (in radii), and how
    // far the channels and banks inside it reach out past it (in meters).
    private sealed record Level(Vector3D[] Centers, double[] Radii, double[] ReachMeters);
}
