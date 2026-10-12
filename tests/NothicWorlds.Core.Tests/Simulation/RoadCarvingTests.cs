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

    [Fact]
    public void OverATunnel_TheGroundIsLeftAlone_AndWalkersInsideWalkOnItsRoad()
    {
        RoadProfile road = RoadProfile.For(RoadProfileTests.Highway(), RadiusKm,
            RoadProfileTests.Mesa(), null)!;
        RoadCarving carving = RoadCarving.For([road], RadiusKm);
        int middle = Enumerable.Range(0, road.Points.Count)
            .MinBy(i => Math.Abs(RoadProfileTests.East(road.Points[i])));
        Vector3D over = road.Points[middle];

        Assert.Equal(300, carving.Shape(over, 300), 9);
        Assert.Equal(300, carving.Shape(Beside(20, 0), 300), 9);

        // Just inside each portal, the cutting runs on (behind the portal's front).
        RoadTunnel tunnel = Assert.Single(road.Tunnels);
        int first = tunnel.First, last = tunnel.Last;
        Vector3D inside = road.Points[first] * 0.8 + road.Points[first + 1] * 0.2;
        inside *= 1 / inside.Length;
        double floor = road.RoadMeters[first] * 0.8 + road.RoadMeters[first + 1] * 0.2;
        Assert.Equal(floor, carving.Shape(inside, 300), 6);
        Vector3D deeper = road.Points[last - 1];
        Assert.Equal(300, carving.Shape(deeper, 300), 9);

        // Coarser ground at a portal is sunk under its road (out of sight under the cutting's
        // strips, the portal's front, and its block); deeper in, it's the hill.
        Func<Vector3D, double, (double Shaped, double Sink)> parts =
            carving.PartsNear(road.Points[first], 1e-4, 2)!;
        (double shaped, double sink) = parts(inside, 300);
        Assert.True(shaped - sink <= floor - StoneTunnel.HiddenBelowMeters + 1e-6);
        Vector3D past = road.Points[first + 2];
        (shaped, sink) = parts(past, 300);
        Assert.Equal(300, shaped - sink, 9);
        Assert.Equal(road.RoadMeters[middle], carving.TunnelFloorMeters(over)!.Value, 6);
        Assert.Null(carving.TunnelFloorMeters(Beside(Shoulders + 1, 0)));
        Assert.Null(_level.TunnelFloorMeters(Beside(0)));

        // Plants grow over it (there's no road there), but rough terrain fades toward it.
        Assert.Equal(50, carving.BeyondSidesMeters(over, 50, tunnels: false), 9);
        Assert.Equal(0, carving.BeyondSidesMeters(over, 50), 9);
    }
}
