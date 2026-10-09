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
        RiverChannels.Near([_river], eye, RadiusKm, reachMeters, _ => 100);

    // A spot `north` meters north of the river, at longitude `east` degrees.
    private static Vector3D Beside(double north, double east = 0.01) =>
        WaterGround.At(north / MetersPerDegree, east);

    [Fact]
    public void UpClose_TheRiverRunsInABed_WithBanksRisingToTheGround()
    {
        RiverChannels channels = Around(Beside(10));
        int middle = _river.Points.Count / 2;
        double halfWidth = _river.HalfWidthMeters[middle];

        double bed = channels.Carve(Beside(0), 100);
        Assert.Equal(_river.BedMeters[middle], bed, 0);
        double bank = channels.Carve(Beside(halfWidth + 4), 100);
        // Its half-width here is within a few tenths of a meter of the middle's.
        Assert.InRange(bank - bed, 4 * RiverChannels.BankSlope - 0.25,
            4 * RiverChannels.BankSlope + 0.25);
        Assert.Equal(100, channels.Carve(Beside(halfWidth + 500), 100));
    }

    [Fact]
    public void UnderTheStrip_TheCoarserGroundSinks_AndBeyondItDoesnt()
    {
        RiverChannels channels = Around(Beside(10));
        double bed = channels.Carve(Beside(0), 100);

        Assert.True(channels.UnderStripMeters(Beside(0), 100) < bed - 0.4);
        Assert.Equal(100, channels.UnderStripMeters(Beside(1000), 100));
    }

    [Fact]
    public void BeyondMaxShapedMeters_TheGroundIsNeitherCarvedNorSunk()
    {
        RiverChannels channels = Around(Beside(10), reachMeters: 100_000);
        double Along(double meters) => 0.01 + meters / MetersPerDegree;

        Assert.NotEqual(100, channels.UnderStripMeters(Beside(0, Along(1000)), 100));
        for (double meters = RiverChannels.MaxShapedMeters; meters < 30_000; meters += 250)
        {
            Assert.Equal(100, channels.UnderStripMeters(Beside(0, Along(meters)), 100));
            Assert.Equal(100, channels.UnderStripMeters(Beside(150, Along(meters)), 100));
        }
    }

    [Fact]
    public void FarFromTheEye_TheRiverLiesOnTheGround_Uncarved()
    {
        RiverChannels channels = Around(Beside(10), reachMeters: 100_000);
        Vector3D far = Beside(0, 0.5);  // About 55 km east

        Assert.Equal(100, channels.Carve(far, 100));
        ChannelPoint there = channels.Stretches.SelectMany(s => s)
            .MinBy(p => WaterGround.Degrees(p.Direction, WaterCells.IndexAt(far)));
        Assert.Equal(0, there.Carved);
        Assert.True(there.SurfaceMeters > 100);
    }

    [Fact]
    public void OnlyTheStretchesInReach_AreKept_InStepsShorterNearTheEye()
    {
        Vector3D eye = Beside(10);
        RiverChannels channels = Around(eye, reachMeters: 5_000);

        ChannelPoint[] points = [.. Assert.Single(channels.Stretches)];
        double span = points[^1].AlongMeters - points[0].AlongMeters;
        Assert.InRange(span, 9_000, 10_100);  // 5 km either side
        double Step(int i) => points[i + 1].AlongMeters - points[i].AlongMeters;
        int nearest = Array.FindIndex(points, p => p.Carved == 1);
        Assert.True(Step(nearest) < Step(0));
    }

    [Fact]
    public void ARiverOutOfReach_LeavesTheGroundAlone()
    {
        RiverChannels channels = Around(WaterGround.At(5, 0));

        Assert.Empty(channels.Stretches);
        Assert.Equal(100, channels.Carve(WaterGround.At(5, 0), 100));
    }
}
