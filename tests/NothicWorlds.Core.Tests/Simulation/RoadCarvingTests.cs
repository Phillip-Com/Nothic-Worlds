using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class RoadCarvingTests
{
    private const double RadiusKm = RoadProfileTests.RadiusKm;
    private const double MetersPerDegree = RoadProfileTests.MetersPerDegree;

    // A highway laid over level ground 100 m up, and one crossing the stream on a bridge.
    private static readonly RoadCarving _level = RoadCarving.For(
        [RoadProfile.For(RoadProfileTests.Highway(), RadiusKm, _ => 100, null)!], RadiusKm);

    private static readonly RoadProfile _bridged = RoadProfile.For(RoadProfileTests.Highway(),
        RadiusKm, _ => 100, RoadProfileTests.Stream)!;

    // A spot `north` meters north of the road, `east` meters east of longitude 0.
    private static Vector3D Beside(double north, double east = 500) =>
        WaterGround.At(north / MetersPerDegree, east / MetersPerDegree);

    // How far from the road's middle its shoulders end, in meters.
    private const double Shoulders = 4 + RoadCarving.ShoulderMeters;

    [Fact]
    public void AcrossThePavingAndShoulders_TheGroundIsTheRoad()
    {
        Assert.Equal(100, _level.Shape(Beside(0), 130), 9);
        Assert.Equal(100, _level.Shape(Beside(-Shoulders + 0.1), 70), 9);
    }

    [Fact]
    public void HigherGround_IsCutBack_AndLowerGroundBankedUp_AtTheirSlopes()
    {
        Assert.Equal(100 + 3 * RoadCarving.CutSlope, _level.Shape(Beside(Shoulders + 3), 120),
            6);
        Assert.Equal(100 - 3 * RoadCarving.BankSlope, _level.Shape(Beside(Shoulders + 3), 80),
            6);

        // Where the slopes have met the ground, and past the sides' reach, it's left alone.
        Assert.Equal(101, _level.Shape(Beside(Shoulders + 3), 101), 9);
        Assert.Equal(130, _level.Shape(Beside(200), 130), 9);
    }

    [Fact]
    public void PastTheRoadsEnds_NothingIsShaped()
    {
        // The road ends 0.03 degrees west of longitude 0.
        double end = -0.03 * MetersPerDegree;
        Assert.Equal(100, _level.Shape(Beside(0, end + 1), 130), 9);
        Assert.Equal(130, _level.Shape(Beside(0, end - 1), 130), 9);
        Assert.Equal(0, _level.PartsNear(Beside(0, end), 1e-5, 20)!(Beside(0, end - 3), 120)
            .Sink);
    }

    [Fact]
    public void UnderABridge_TheGroundIsLeftAsTheRiverCutIt_AndWalkersCrossOnItsRoad()
    {
        RoadCarving carving = RoadCarving.For([_bridged], RadiusKm);
        RoadBridge bridge = Assert.Single(_bridged.Bridges);
        Vector3D middle = Beside(0, 0);

        Assert.Equal(90, carving.Shape(middle, 90), 9);
        double deck = carving.DeckMeters(middle)!.Value;
        Assert.InRange(deck, bridge.WaterMeters + StoneBridge.ClearanceMeters(
            bridge.LengthMeters) - 1e-6, 200);
        Assert.Null(carving.DeckMeters(Beside(0, 500)));       // Off the bridge
        Assert.Null(carving.DeckMeters(Beside(6, 0)));         // Over its wall
        Assert.Null(_level.DeckMeters(Beside(0, 0)));          // No bridge here
    }

    [Fact]
    public void CoarserGround_IsSunkUnderTheStrip_FadingOutPastItsSides()
    {
        Vector3D on = Beside(0);
        Func<Vector3D, double, (double Shaped, double Sink)> parts =
            _level.PartsNear(on, 1e-5, 20)!;

        (double shaped, double sink) = parts(on, 120);
        Assert.Equal(100, shaped, 9);
        Assert.True(sink > 0.4 * 20);
        Assert.Equal(0, parts(Beside(500), 120).Sink);

        // Far from any road, or on cells so wide the road would be lost in one, nothing.
        Assert.Null(_level.PartsNear(Beside(50_000), 1e-5, 20));
        Assert.Null(_level.PartsNear(on, 1e-3, 5_000));
    }

    [Fact]
    public void BeyondSides_MeasuresFromTheSidesOut()
    {
        double reach = RoadCarving.ReachMeters(4, 0);

        Assert.Equal(0, _level.BeyondSidesMeters(Beside(0), 1_000));
        Assert.Equal(100, _level.BeyondSidesMeters(Beside(reach + 100), 1_000), 3);
        Assert.Equal(50, _level.BeyondSidesMeters(Beside(reach + 100), 50));
    }
}
