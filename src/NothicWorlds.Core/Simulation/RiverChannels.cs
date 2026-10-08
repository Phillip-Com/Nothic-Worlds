using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The rivers around a first-person eye (VISION.md BOD-11; owner's choice: banks carved up
/// close): the stretches of their courses in reach, in steps that shorten toward the eye, and
/// the channels they cut into the ground there. Up close a river runs in a bed with banks
/// sloping up to the ground; farther out, where the ground drawn around the eye is too coarse
/// for a channel, it lies on the ground. The channel and its banks are drawn finely by a strip
/// along the river (<see cref="RiverBanks"/>); the coarser ground around the eye is sunk a
/// little under it (<see cref="UnderStripMeters"/>), so it never shows through.
/// </summary>
public sealed class RiverChannels
{
    /// <summary>How steeply the banks rise: meters up for each meter out.</summary>
    public const double BankSlope = 0.5;

    /// <summary>The widest a bank reaches out from the water, in meters.</summary>
    public const double MaxBankMeters = 200;

    /// <summary>
    /// How far from the eye a river's channel is carved, for each meter of its half-width plus
    /// its banks, and the least and most.
    /// </summary>
    public const double CarveReachPerMeter = 12, MinCarveReachMeters = 200,
        MaxCarveReachMeters = 5000;

    // The skirt of the strip beyond each bank, where it blends into the coarser ground: this
    // share of its distance from the eye plus this, in meters (wider than a cell of that
    // ground there).
    private const double SkirtShare = 0.15, MinSkirtMeters = 2;

    // How far the coarser ground is sunk under the strip: this share of its distance from the
    // eye plus this, in meters (more than the coarser ground's flat cells can bulge by).
    private const double SinkShare = 0.03, MinSinkMeters = 0.5;

    // How long the steps along a river are: this share of their distance from the eye, and
    // the least and most, in meters.
    private const double StepShare = 0.01, MinStepMeters = 1, MaxStepMeters = 500;

    // The carving follows a river in segments at least this long, or this share of their
    // distance from the eye, in meters.
    private const double MinCarveStepMeters = 10, CarveStepShare = 0.02;

    // The carving looks up the stretches near a point in squares this wide, in meters.
    private const double CellMeters = 64;

    // How far a river lying on the ground is lifted, so the coarser ground drawn there
    // doesn't hide it: this share of its distance from the eye, and at least this, in meters.
    private const double LiftShare = 0.003, MinLiftMeters = 0.3;

    private readonly Vector3D _eye, _east, _north;
    private readonly double _radiusMeters;
    private readonly Dictionary<(int, int), List<Segment>> _cells = [];

    private RiverChannels(Vector3D eye, double radiusMeters, List<List<ChannelPoint>> stretches)
    {
        (_eye, _radiusMeters, Stretches) = (eye, radiusMeters, stretches);
        (_east, _north) = GlobeWalk.Tangents(eye);
        foreach (List<ChannelPoint> stretch in stretches)
        {
            // The carving needs far fewer, longer segments than the water's steps.
            ChannelPoint last = stretch[0];
            for (int i = 1; i < stretch.Count; i++)
            {
                ChannelPoint point = stretch[i];
                double fromEye = Project(last.Direction) is (double x, double y)
                    ? Math.Sqrt(x * x + y * y)
                    : 0;
                if (i < stretch.Count - 1 && point.AlongMeters - last.AlongMeters
                    < Math.Max(MinCarveStepMeters, CarveStepShare * fromEye))
                {
                    continue;
                }

                if (last.Carved > 0 || point.Carved > 0)
                {
                    Register(last, point);
                }

                last = point;
            }
        }
    }

    /// <summary>The stretches of river in reach, each in steps from upstream down.</summary>
    public IReadOnlyList<IReadOnlyList<ChannelPoint>> Stretches { get; }

    /// <summary>
    /// How far from the eye a river with this half-width is carved, in meters.
    /// </summary>
    public static double CarveReachMeters(double halfWidthMeters) => Math.Clamp(
        (halfWidthMeters + BankWidthMeters(halfWidthMeters)) * CarveReachPerMeter,
        MinCarveReachMeters, MaxCarveReachMeters);

    /// <summary>How far a river's banks may reach out from its water, in meters.</summary>
    public static double BankWidthMeters(double halfWidthMeters) =>
        Math.Min(MaxBankMeters, 20 + halfWidthMeters);

    /// <summary>
    /// How wide the strip's skirt beyond each bank is, <paramref name="fromEyeMeters"/> from
    /// the eye, in meters.
    /// </summary>
    public static double SkirtMeters(double fromEyeMeters) =>
        MinSkirtMeters + SkirtShare * fromEyeMeters;

    /// <summary>
    /// How far the coarser ground is sunk under the strip, <paramref name="fromEyeMeters"/>
    /// from the eye, in meters.
    /// </summary>
    public static double SinkMeters(double fromEyeMeters) =>
        MinSinkMeters + SinkShare * fromEyeMeters;

    /// <summary>
    /// The rivers within <paramref name="reachMeters"/> of <paramref name="eye"/> (a unit
    /// direction) on a body <paramref name="radiusKm"/> in radius, whose ground is
    /// <paramref name="groundMeters"/> high at each direction.
    /// </summary>
    public static RiverChannels Near(IReadOnlyList<RiverProfile> rivers, Vector3D eye,
        double radiusKm, double reachMeters, Func<Vector3D, double> groundMeters)
    {
        double radiusMeters = radiusKm * 1000;
        var stretches = new List<List<ChannelPoint>>();
        foreach (RiverProfile river in rivers)
        {
            List<ChannelPoint>? stretch = null;
            for (int i = 1; i < river.Points.Count; i++)
            {
                double away = DistanceToSegment(eye, river.Points[i - 1], river.Points[i])
                    * radiusMeters;
                if (away > reachMeters)
                {
                    stretch = null;
                    continue;
                }

                AddSteps(stretches, ref stretch, river, i, reachMeters, eye, radiusMeters,
                    groundMeters);
            }
        }

        stretches.RemoveAll(stretch => stretch.Count < 2);
        return new RiverChannels(eye, radiusMeters, stretches);
    }

    /// <summary>
    /// The ground's height at <paramref name="direction"/> with the rivers' channels cut into
    /// it, given its height there, <paramref name="groundMeters"/>: a bed as wide as the river,
    /// with banks rising at <see cref="BankSlope"/> to meet the ground.
    /// </summary>
    public double Carve(Vector3D direction, double groundMeters) =>
        Shape(direction, groundMeters, sink: false);

    /// <summary>
    /// The height of the coarser ground drawn around the eye at <paramref name="direction"/>,
    /// given the ground's height there: carved (<see cref="Carve"/>), and sunk out of sight
    /// under the strip that draws the channel finely (by <see cref="SinkMeters"/>, fading out
    /// across the strip's skirt).
    /// </summary>
    public double UnderStripMeters(Vector3D direction, double groundMeters) =>
        Shape(direction, groundMeters, sink: true);

    // The ground carved, and (with `sink`) sunk under the strip: one pass over the segments
    // near the point, as the coarser ground asks for thousands of points at a time.
    private double Shape(Vector3D direction, double groundMeters, bool sink)
    {
        if (_cells.Count == 0 || Project(direction) is not (double x, double y)
            || !_cells.TryGetValue(CellOf(x, y), out var segments))
        {
            return groundMeters;
        }

        double fromEye = Math.Sqrt(x * x + y * y);
        double skirt = SkirtMeters(fromEye);
        double carved = groundMeters, cover = 0;
        foreach (Segment segment in segments)
        {
            if (!segment.Reaches(x, y))
            {
                continue;
            }

            (double away, double t) = segment.Nearest(x, y);
            double halfWidth = Lerp(segment.HalfWidth, segment.ToHalfWidth, t);
            double share = Lerp(segment.Carved, segment.ToCarved, t);
            double beyond = away - halfWidth - BankWidthMeters(halfWidth);
            if (share <= 0)
            {
                continue;
            }

            if (sink)
            {
                cover = Math.Max(cover, share * Math.Clamp(1 - beyond / skirt, 0, 1));
            }

            if (beyond > 0)
            {
                continue;
            }

            double bank = Lerp(segment.Bed, segment.ToBed, t)
                + Math.Max(0, away - halfWidth) * BankSlope;
            if (bank < groundMeters)
            {
                carved = Math.Min(carved, Lerp(groundMeters, bank, share));
            }
        }

        return carved - cover * SinkMeters(fromEye);
    }

    // The steps in reach from point i - 1 (unless the stretch already ends there) to point
    // i of a river, each step's length set by its own distance from the eye. Steps out of
    // reach end the stretch (and are passed over quickly); the next one in reach starts
    // another.
    private static void AddSteps(List<List<ChannelPoint>> stretches,
        ref List<ChannelPoint>? stretch, RiverProfile river, int i, double reachMeters,
        Vector3D eye, double radiusMeters, Func<Vector3D, double> groundMeters)
    {
        double length = river.AlongMeters[i] - river.AlongMeters[i - 1];
        bool joined = stretch is not null;  // Point i - 1 ends the stretch already
        for (double at = 0; ;)
        {
            double t = length > 0 ? Math.Min(at / length, 1) : 1;
            Vector3D direction = river.Points[i - 1] * (1 - t) + river.Points[i] * t;
            direction *= 1 / direction.Length;
            double fromEye = radiusMeters * Math.Acos(Math.Clamp(direction.Dot(eye), -1, 1));
            if (at > 0 || !joined)
            {
                AddStep(stretches, ref stretch, river, i, t, direction, fromEye, reachMeters,
                    groundMeters);
            }

            if (t >= 1)
            {
                return;
            }

            at += fromEye > reachMeters
                ? Math.Max(fromEye - reachMeters, MinStepMeters)
                : Math.Clamp(fromEye * StepShare, MinStepMeters, MaxStepMeters);
        }
    }

    // A step `t` of the way from point i - 1 to point i of a river, if it's in reach.
    private static void AddStep(List<List<ChannelPoint>> stretches,
        ref List<ChannelPoint>? stretch, RiverProfile river, int i, double t,
        Vector3D direction, double fromEye, double reachMeters,
        Func<Vector3D, double> groundMeters)
    {
        if (fromEye > reachMeters)
        {
            stretch = null;
            return;
        }

        if (stretch is null)
        {
            stretch = [];
            stretches.Add(stretch);
        }

        double halfWidth = Lerp(river.HalfWidthMeters[i - 1], river.HalfWidthMeters[i], t);
        double reach = CarveReachMeters(halfWidth);
        double carved = 1 - SmoothStep(0.8 * reach, reach, fromEye);
        double water = Lerp(river.WaterMeters[i - 1], river.WaterMeters[i], t);
        double lying = carved < 1
            ? groundMeters(direction) + Math.Max(MinLiftMeters, fromEye * LiftShare)
            : water;
        stretch.Add(new ChannelPoint(
            direction,
            Lerp(river.AlongMeters[i - 1], river.AlongMeters[i], t),
            halfWidth,
            Lerp(lying, water, carved),
            Lerp(river.BedMeters[i - 1], river.BedMeters[i], t),
            carved,
            Lerp(river.FlowMetersPerSecond[i - 1], river.FlowMetersPerSecond[i], t),
            Lerp(river.Rapids[i - 1], river.Rapids[i], t)));
    }

    // Files a segment under every square its channel and banks reach into.
    private void Register(ChannelPoint from, ChannelPoint to)
    {
        if (Project(from.Direction) is not (double fromX, double fromY)
            || Project(to.Direction) is not (double toX, double toY))
        {
            return;
        }

        double halfWidth = Math.Max(from.HalfWidthMeters, to.HalfWidthMeters);
        double farther = Math.Max(Math.Sqrt(fromX * fromX + fromY * fromY),
            Math.Sqrt(toX * toX + toY * toY));
        double reach = halfWidth + BankWidthMeters(halfWidth)
            + SkirtMeters(farther + MaxBankMeters);
        var segment = new Segment(from, to, fromX, fromY, toX, toY, reach);
        (int left, int bottom) =
            CellOf(Math.Min(fromX, toX) - reach, Math.Min(fromY, toY) - reach);
        (int right, int top) =
            CellOf(Math.Max(fromX, toX) + reach, Math.Max(fromY, toY) + reach);
        for (int column = left; column <= right; column++)
        {
            for (int row = bottom; row <= top; row++)
            {
                if (!_cells.TryGetValue((column, row), out var list))
                {
                    list = [];
                    _cells[(column, row)] = list;
                }

                list.Add(segment);
            }
        }
    }

    // Where a direction falls on the plane touching the globe at the eye, in meters east and
    // north of it (straight lines on the globe stay straight on it); null if it's around the
    // far side.
    private (double X, double Y)? Project(Vector3D direction)
    {
        double toward = direction.Dot(_eye);
        return toward < 0.1
            ? null
            : (_radiusMeters * direction.Dot(_east) / toward,
                _radiusMeters * direction.Dot(_north) / toward);
    }

    private static (int, int) CellOf(double x, double y) =>
        ((int)Math.Floor(x / CellMeters), (int)Math.Floor(y / CellMeters));

    // The angle from a direction to the nearest point of the arc between two others, in
    // radians (close enough for the short stretches of a river's course).
    private static double DistanceToSegment(Vector3D point, Vector3D from, Vector3D to)
    {
        Vector3D along = to - from;
        double lengthSquared = along.Dot(along);
        double t = lengthSquared > 0
            ? Math.Clamp((point - from).Dot(along) / lengthSquared, 0, 1)
            : 0;
        Vector3D nearest = from + along * t;
        nearest *= 1 / nearest.Length;
        return Math.Acos(Math.Clamp(point.Dot(nearest), -1, 1));
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static double Square(double x) => x * x;

    private static double SmoothStep(double from, double to, double x)
    {
        double t = Math.Clamp((x - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }

    // A segment of a river's course for carving: where its ends fall on the plane at the eye,
    // and what's needed at each end (kept as plain numbers: millions of points are measured
    // against them).
    private sealed class Segment(ChannelPoint from, ChannelPoint to, double fromX,
        double fromY, double toX, double toY, double reach)
    {
        // The box the segment's channel, banks, and skirts reach into.
        private readonly double _left = Math.Min(fromX, toX) - reach,
            _right = Math.Max(fromX, toX) + reach, _bottom = Math.Min(fromY, toY) - reach,
            _top = Math.Max(fromY, toY) + reach;

        private readonly double _x = fromX, _y = fromY, _dx = toX - fromX, _dy = toY - fromY;
        private readonly double _inverseLengthSquared = Square(toX - fromX) + Square(toY - fromY)
            is double lengthSquared and > 0 ? 1 / lengthSquared : 0;

        public double HalfWidth { get; } = from.HalfWidthMeters;
        public double ToHalfWidth { get; } = to.HalfWidthMeters;
        public double Bed { get; } = from.BedMeters;
        public double ToBed { get; } = to.BedMeters;
        public double Carved { get; } = from.Carved;
        public double ToCarved { get; } = to.Carved;

        // Whether a point is in the box the segment reaches into (a quick test before
        // measuring).
        public bool Reaches(double x, double y) =>
            x >= _left && x <= _right && y >= _bottom && y <= _top;

        // How far a point is from the segment, in meters, and how far along it its nearest
        // point is (0 to 1).
        public (double Away, double T) Nearest(double x, double y)
        {
            double t = Math.Clamp(((x - _x) * _dx + (y - _y) * _dy) * _inverseLengthSquared, 0,
                1);
            return (Math.Sqrt(Square(x - _x - t * _dx) + Square(y - _y - t * _dy)), t);
        }
    }
}
