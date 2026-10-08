using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The rivers around a first-person eye (VISION.md BOD-11; owner's choice: banks carved up
/// close): the stretches of their courses in reach, in steps that shorten toward the eye, and
/// the channels they cut into the ground there. Up close a river runs in a bed with banks
/// sloping up to the ground; farther out, where the ground drawn around the eye is too coarse
/// for a channel, it lies on the ground.
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

    // How long the steps along a river are: this share of their distance from the eye, and
    // the least and most, in meters.
    private const double StepShare = 0.01, MinStepMeters = 1, MaxStepMeters = 500;

    // The carving looks up the stretches near a point in squares this wide, in meters.
    private const double CellMeters = 250;

    // How far a river lying on the ground is lifted, so the coarser ground drawn there
    // doesn't hide it: this share of its distance from the eye, and at least this, in meters.
    private const double LiftShare = 0.003, MinLiftMeters = 0.3;

    private readonly Vector3D _eye, _east, _north;
    private readonly double _radiusMeters;
    private readonly Dictionary<(int, int), List<(ChannelPoint From, ChannelPoint To)>> _cells
        = [];

    private RiverChannels(Vector3D eye, double radiusMeters, List<List<ChannelPoint>> stretches)
    {
        (_eye, _radiusMeters, Stretches) = (eye, radiusMeters, stretches);
        (_east, _north) = GlobeWalk.Tangents(eye);
        foreach (List<ChannelPoint> stretch in stretches)
        {
            for (int i = 1; i < stretch.Count; i++)
            {
                if (stretch[i - 1].Carved > 0 || stretch[i].Carved > 0)
                {
                    Register(stretch[i - 1], stretch[i]);
                }
            }
        }
    }

    /// <summary>The stretches of river in reach, each in steps from upstream down.</summary>
    public IReadOnlyList<IReadOnlyList<ChannelPoint>> Stretches { get; }

    /// <summary>
    /// How far from the eye a river with this half-width is carved, in meters.
    /// </summary>
    public static double CarveReachMeters(double halfWidthMeters) => Math.Clamp(
        (halfWidthMeters + BankWidth(halfWidthMeters)) * CarveReachPerMeter,
        MinCarveReachMeters, MaxCarveReachMeters);

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

                AddSteps(stretches, ref stretch, river, i, away, reachMeters, eye,
                    radiusMeters, groundMeters);
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
    public double Carve(Vector3D direction, double groundMeters)
    {
        if (_cells.Count == 0 || Project(direction) is not (double x, double y)
            || !_cells.TryGetValue(CellOf(x, y), out var segments))
        {
            return groundMeters;
        }

        double carved = groundMeters;
        foreach ((ChannelPoint from, ChannelPoint to) in segments)
        {
            (double fromX, double fromY) = Project(from.Direction)!.Value;
            (double toX, double toY) = Project(to.Direction)!.Value;
            double dx = toX - fromX, dy = toY - fromY;
            double lengthSquared = dx * dx + dy * dy;
            double t = lengthSquared > 0
                ? Math.Clamp(((x - fromX) * dx + (y - fromY) * dy) / lengthSquared, 0, 1)
                : 0;
            double away = Math.Sqrt(Square(x - fromX - t * dx) + Square(y - fromY - t * dy));
            double halfWidth = Lerp(from.HalfWidthMeters, to.HalfWidthMeters, t);
            double share = Lerp(from.Carved, to.Carved, t);
            if (share <= 0 || away > halfWidth + BankWidth(halfWidth))
            {
                continue;
            }

            double bank = Lerp(from.BedMeters, to.BedMeters, t)
                + Math.Max(0, away - halfWidth) * BankSlope;
            if (bank < groundMeters)
            {
                carved = Math.Min(carved, Lerp(groundMeters, bank, share));
            }
        }

        return carved;
    }

    // How far a river's banks may reach out from its water: wider rivers, wider banks.
    private static double BankWidth(double halfWidthMeters) =>
        Math.Min(MaxBankMeters, 20 + halfWidthMeters);

    // The steps in reach from point i - 1 (unless the stretch already ends there) to point
    // i of a river, the segment between them `away` meters from the eye at its nearest. Steps
    // out of reach end the stretch; the next one in reach starts another.
    private static void AddSteps(List<List<ChannelPoint>> stretches,
        ref List<ChannelPoint>? stretch, RiverProfile river, int i, double away,
        double reachMeters, Vector3D eye, double radiusMeters,
        Func<Vector3D, double> groundMeters)
    {
        double length = river.AlongMeters[i] - river.AlongMeters[i - 1];
        double step = Math.Clamp(away * StepShare, MinStepMeters, MaxStepMeters);
        int steps = Math.Max(1, (int)Math.Ceiling(length / step));
        for (int s = stretch is null ? 0 : 1; s <= steps; s++)
        {
            double t = (double)s / steps;
            Vector3D direction = river.Points[i - 1] * (1 - t) + river.Points[i] * t;
            direction *= 1 / direction.Length;
            double fromEye = radiusMeters * Math.Acos(Math.Clamp(direction.Dot(eye), -1, 1));
            if (fromEye > reachMeters)
            {
                stretch = null;
                continue;
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
        double reach = halfWidth + BankWidth(halfWidth);
        (int left, int bottom) = CellOf(Math.Min(fromX, toX) - reach, Math.Min(fromY, toY) - reach);
        (int right, int top) = CellOf(Math.Max(fromX, toX) + reach, Math.Max(fromY, toY) + reach);
        for (int column = left; column <= right; column++)
        {
            for (int row = bottom; row <= top; row++)
            {
                if (!_cells.TryGetValue((column, row), out var list))
                {
                    list = [];
                    _cells[(column, row)] = list;
                }

                list.Add((from, to));
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
}
