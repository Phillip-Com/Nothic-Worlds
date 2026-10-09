using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The rivers around a first-person eye (VISION.md BOD-11): the stretches of their courses in
/// reach, in steps that shorten toward the eye, each with its water in its channel (cut by
/// <see cref="RiverCarving"/>, at every distance). The water's surface and the strip that
/// draws the channels' beds and banks (<see cref="RiverBanks"/>) are built along them.
/// </summary>
public sealed class RiverChannels
{
    // How long the steps along a river are: this share of their distance from the eye, and
    // the least and most, in meters.
    private const double StepShare = 0.01, MinStepMeters = 1, MaxStepMeters = 500;

    private RiverChannels(List<List<ChannelPoint>> stretches) => Stretches = stretches;

    /// <summary>The stretches of river in reach, each in steps from upstream down.</summary>
    public IReadOnlyList<IReadOnlyList<ChannelPoint>> Stretches { get; }

    /// <summary>
    /// The rivers within <paramref name="reachMeters"/> of <paramref name="eye"/> (a unit
    /// direction) on a body <paramref name="radiusKm"/> in radius.
    /// </summary>
    public static RiverChannels Near(IReadOnlyList<RiverProfile> rivers, Vector3D eye,
        double radiusKm, double reachMeters)
    {
        double radiusMeters = radiusKm * 1000;
        var stretches = new List<List<ChannelPoint>>();
        foreach (RiverProfile river in rivers)
        {
            List<ChannelPoint>? stretch = null;
            for (int i = 1; i < river.Points.Count; i++)
            {
                // Most of a river is far out of reach: passed over without measuring closely.
                double length = river.AlongMeters[i] - river.AlongMeters[i - 1];
                if ((eye - river.Points[i - 1]).Length * radiusMeters - length > reachMeters)
                {
                    stretch = null;
                    continue;
                }

                double away = DistanceToSegment(eye, river.Points[i - 1], river.Points[i])
                    * radiusMeters;
                if (away > reachMeters)
                {
                    stretch = null;
                    continue;
                }

                AddSteps(stretches, ref stretch, river, i, reachMeters, eye, radiusMeters);
            }
        }

        stretches.RemoveAll(stretch => stretch.Count < 2);
        return new RiverChannels(stretches);
    }

    // The steps in reach from point i - 1 (unless the stretch already ends there) to point
    // i of a river, each step's length set by its own distance from the eye. Steps out of
    // reach end the stretch (and are passed over quickly); the next one in reach starts
    // another.
    private static void AddSteps(List<List<ChannelPoint>> stretches,
        ref List<ChannelPoint>? stretch, RiverProfile river, int i, double reachMeters,
        Vector3D eye, double radiusMeters)
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
                AddStep(stretches, ref stretch, river, i, t, direction, fromEye, reachMeters);
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
        Vector3D direction, double fromEye, double reachMeters)
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

        stretch.Add(new ChannelPoint(
            direction,
            Lerp(river.AlongMeters[i - 1], river.AlongMeters[i], t),
            Lerp(river.HalfWidthMeters[i - 1], river.HalfWidthMeters[i], t),
            Lerp(river.WaterMeters[i - 1], river.WaterMeters[i], t),
            Lerp(river.BedMeters[i - 1], river.BedMeters[i], t),
            Lerp(river.FlowMetersPerSecond[i - 1], river.FlowMetersPerSecond[i], t),
            Lerp(river.Rapids[i - 1], river.Rapids[i], t)));
    }

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
}
