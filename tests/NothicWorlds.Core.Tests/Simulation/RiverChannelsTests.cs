using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class RiverChannelsTests
{
    private const double RadiusKm = 6371;
    private const double MetersPerDegree = RadiusKm * 1000 * Math.PI / 180;

    // A river 100 m wide at its mouth running east along the equator over level ground 100 m
    // up, and an eye beside it.
    private static readonly RiverProfile _river = RiverProfile.For(
        RiverProfileTests.Drawn(0.1, WaterGround.At(0, -1), WaterGround.At(0, 1)), RadiusKm,
        _ => 100)!;

    private static RiverChannels Around(Vector3D eye, double reachMeters = 20_000) =>
        RiverChannels.Near([_river], eye, RadiusKm, reachMeters);

    // A spot `north` meters north of the river, at longitude `east` degrees.
    private static Vector3D Beside(double north, double east = 0.01) =>
        WaterGround.At(north / MetersPerDegree, east);

    [Fact]
    public void OnlyTheStretchesInReach_AreKept_InStepsShorterNearTheEye()
    {
        Vector3D eye = Beside(10);
        RiverChannels channels = Around(eye, reachMeters: 5_000);
        ChannelPoint[] points = [.. Assert.Single(channels.Stretches)];
        double span = points[^1].AlongMeters - points[0].AlongMeters;
        Assert.InRange(span, 9_000, 10_100);  // 5 km either side
        double Step(int i) => points[i + 1].AlongMeters - points[i].AlongMeters;
        int nearest = Array.IndexOf(points,
            points.MinBy(p => Math.Acos(Math.Clamp(p.Direction.Dot(eye), -1, 1))));
        Assert.True(Step(nearest) < Step(0));
    }

    [Fact]
    public void FarFromTheEye_TheWaterIsStillInItsChannel()
    {
        // No lying on the ground, lifted, far off: the water's where the profile puts it.
        RiverChannels channels = Around(Beside(10), reachMeters: 100_000);
        Vector3D far = Beside(0, 0.5);  // About 55 km east
        ChannelPoint there = channels.Stretches.SelectMany(s => s)
            .MinBy(p => Math.Acos(Math.Clamp(p.Direction.Dot(far), -1, 1)));
        Assert.True(there.SurfaceMeters < 100);
        Assert.True(there.BedMeters < there.SurfaceMeters);
    }

    [Fact]
    public void ARiverOutOfReach_HasNoStretches()
    {
        Assert.Empty(Around(WaterGround.At(5, 0)).Stretches);
    }
}
