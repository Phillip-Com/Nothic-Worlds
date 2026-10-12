using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class RoadStripsTests
{
    private const double RadiusKm = RoadProfileTests.RadiusKm;
    private const double MetersPerDegree = RoadProfileTests.MetersPerDegree;

    // The highway crossing the stream on its bridge, the ground carved by both, and an eye
    // standing on the road 300 m east of the bridge.
    private static readonly RoadProfile _road = RoadProfile.For(RoadProfileTests.Highway(),
        RadiusKm, _ => 100, RoadProfileTests.Stream)!;

    private static readonly RoadCarving _roads = RoadCarving.For([_road], RadiusKm);

    private static readonly GroundCarving _carving = GroundCarving.Of(RoadProfileTests.Stream,
        _roads);

    private static readonly Vector3D _eye = WaterGround.At(0, 300 / MetersPerDegree);

    private static (List<List<BankPoint[]>> Strips, List<BridgeShown> Bridges) Near(
        double reachMeters)
    {
        (List<List<BankPoint[]>> strips, List<BridgeShown> bridges, _) = RoadStrips.Near(
            _roads, _carving.Carve, _eye, RadiusKm, reachMeters, _ => 100, _ => 99, _ => 5);
        return (strips, bridges);
    }

    [Fact]
    public void EachStretch_HasItsPavingBetweenItsSides_EndingAtTheBridge()
    {
        (List<List<BankPoint[]>> strips, List<BridgeShown> bridges) = Near(1_000);

        // Two stretches (either side of the bridge), each a side, its paving, and a side.
        Assert.Equal(6, strips.Count);
        Assert.Single(bridges);
        List<BankPoint[]> paving = strips[4];
        Assert.All(paving, row => Assert.All(row, point =>
            Assert.Equal(StripSurface.Paving, point.Surface)));
        Assert.All(strips[3].Concat(strips[5]), row =>
            Assert.All(row, point => Assert.Equal(StripSurface.Ground, point.Surface)));

        // No row runs over the bridge, and every row runs the same way across.
        RoadBridge bridge = Assert.Single(_road.Bridges);
        double bridgeEast = RoadProfileTests.East(_road.Points[bridge.Last]);
        Assert.All(paving, row => Assert.True(
            RoadProfileTests.East(row[2].Direction) >= bridgeEast - 1e-3));
    }

    [Fact]
    public void ThePaving_LiesJustOverTheRoad_AndTheSkirtsEndOverTheCoarserGround()
    {
        (List<List<BankPoint[]>> strips, _) = Near(1_000);
        BankPoint[] row = strips[4].MinBy(r => (r[2].Direction - _eye).Length)!;

        Assert.All(row, point => Assert.InRange(point.Meters, 100, 100.05));
        BankPoint[] side = strips[5].MinBy(r => (r[0].Direction - _eye).Length)!;
        Assert.Equal(row[^1].Meters, side[0].Meters, 9);  // The side meets the paving
        Assert.Equal(1, side[0].Shaped);
        Assert.Equal(0, side[^1].Shaped);
        Assert.InRange(side[^1].Meters, 99, 99.1);
    }

    [Fact]
    public void RoadsOutOfReach_GiveNothing()
    {
        Vector3D far = WaterGround.At(1, 0);
        (List<List<BankPoint[]>> strips, List<BridgeShown> bridges, _) = RoadStrips.Near(
            _roads, _carving.Carve, far, RadiusKm, 1_000, _ => 100, _ => 99, _ => 5);

        Assert.Empty(strips);
        Assert.Empty(bridges);
    }

    [Fact]
    public void ABridge_IsPlacedAlongTheRoad_AtItsRoadsHeight()
    {
        BridgeShown bridge = Assert.Single(Near(1_000).Bridges);
        RoadBridge span = Assert.Single(_road.Bridges);

        BridgePoint start = bridge.Points.First(p =>
            p.Material == BridgeMaterial.Paving && p.Position.X == 0);
        (Vector3D direction, double meters) = bridge.Place(start with
        {
            Position = start.Position with { Y = 0 },
        });
        Assert.True((direction - _road.Points[span.First]).Length * RadiusKm * 1000 < 1e-3);
        Assert.Equal(_road.RoadMeters[span.First], meters, 9);
    }

    [Fact]
    public void NearATunnel_ItsVaultAndPortalsAreLaid_AndNoStripRunsThroughIt()
    {
        RoadProfile road = RoadProfile.For(RoadProfileTests.Highway(), RadiusKm,
            RoadProfileTests.Mesa(), null)!;
        RoadCarving roads = RoadCarving.For([road], RadiusKm);
        RoadTunnel tunnel = Assert.Single(road.Tunnels);
        Vector3D eye = WaterGround.At(0, 0);

        (List<List<BankPoint[]>> strips, List<BridgeShown> pieces, List<TunnelPortal> portals) =
            RoadStrips.Near(roads, roads.Shape, eye, RadiusKm, 1_000, _ => 100, _ => 99,
                _ => 5);

        Assert.Equal(2, portals.Count);
        Assert.Equal(tunnel.Last - tunnel.First + 2, pieces.Count);  // And no bridges
        double from = RoadProfileTests.East(road.Points[tunnel.First]);
        double to = RoadProfileTests.East(road.Points[tunnel.Last]);
        Assert.All(strips.SelectMany(strip => strip).SelectMany(row => row), point =>
        {
            double east = RoadProfileTests.East(point.Direction);
            Assert.True(east <= from + 1e-3 || east >= to - 1e-3, $"{east}");
        });

        // The coarser ground is sunk from in front of each portal to under its block.
        TunnelPortal entrance = portals.MinBy(p => RoadProfileTests.East(p.Shown.Start))!;
        double door = RoadProfileTests.East(entrance.Shown.Start);
        Assert.True(entrance.Covers(WaterGround.At(0, (door - 10) / MetersPerDegree)));
        Assert.True(entrance.Covers(WaterGround.At(0, (door + 3) / MetersPerDegree)));
        Assert.False(entrance.Covers(WaterGround.At(0, (door - 30) / MetersPerDegree)));
        Assert.False(entrance.Covers(WaterGround.At(0.01, (door - 10) / MetersPerDegree)));
    }
}
