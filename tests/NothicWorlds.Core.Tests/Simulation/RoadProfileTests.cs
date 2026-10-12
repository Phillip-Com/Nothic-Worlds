using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class RoadProfileTests
{
    internal const double RadiusKm = 6371;
    internal const double MetersPerDegree = RadiusKm * 1000 * Math.PI / 180;

    // A river 50 m wide running north across the equator at longitude 0, over level ground
    // 100 m up.
    internal static readonly RiverCarving Stream = RiverCarving.For([RiverProfile.For(
        RiverProfileTests.Drawn(0.05, WaterGround.At(-0.05, 0), WaterGround.At(0.05, 0)),
        RadiusKm, _ => 100)!], RadiusKm);

    // A stone highway 8 m wide drawn east along the equator, `degrees` either side of
    // longitude 0.
    internal static RoadCourseShown Highway(double degrees = 0.03,
        RoadStyle style = RoadStyle.StoneHighway) =>
        new(Guid.NewGuid(), style, RoadKind.Drawn, 8,
            RiverLine.Densified([WaterGround.At(0, -degrees), WaterGround.At(0, degrees)]),
            null);

    // How far east of longitude 0 a direction is, in meters.
    internal static double East(Vector3D direction) =>
        RiverProfileTests.Longitude(direction) * MetersPerDegree;

    [Fact]
    public void OnlyStoneHighways_AreLaidUpCloseSoFar()
    {
        Assert.Null(RoadProfile.For(Highway(style: RoadStyle.DirtPaths), RadiusKm, _ => 100,
            null));
        Assert.NotNull(RoadProfile.For(Highway(), RadiusKm, _ => 100, null));
    }

    [Fact]
    public void OnLevelGround_TheRoadLiesOnIt_InShortSteps()
    {
        RoadProfile road = RoadProfile.For(Highway(), RadiusKm, _ => 100, null)!;

        Assert.All(road.RoadMeters, meters => Assert.Equal(100, meters, 9));
        Assert.Empty(road.Bridges);
        Assert.Equal(4, road.HalfWidthMeters);
        Assert.All(road.AlongMeters.Zip(road.AlongMeters.Skip(1)), pair =>
            Assert.InRange(pair.Second - pair.First, 0, RoadProfile.SampleMeters + 1e-6));
    }

    [Fact]
    public void OverASteepHill_TheRoadCutsThrough_NeverSteeperThanItsGrade()
    {
        // A hill 10 m high, rising as steeply as 1 in 8.
        static double Hill(Vector3D direction) =>
            100 + 10 * Math.Exp(-Math.Pow(East(direction) / 50, 2));
        RoadProfile road = RoadProfile.For(Highway(), RadiusKm, Hill, null)!;

        for (int i = 1; i < road.Points.Count; i++)
        {
            double grade = Math.Abs(road.RoadMeters[i] - road.RoadMeters[i - 1])
                / (road.AlongMeters[i] - road.AlongMeters[i - 1]);
            Assert.True(grade <= RoadProfile.MaxGrade + 1e-9, $"{grade} at {i}");
        }

        int top = Enumerable.Range(0, road.Points.Count)
            .MaxBy(i => road.GroundMeters[i]);
        Assert.True(road.RoadMeters[top] < road.GroundMeters[top] - 2);  // A cutting
    }

    [Fact]
    public void AtACliff_TheRoadBanksNoMoreThanItsLimit_AndTunnelsIntoIt_OnItsGrade()
    {
        // A cliff 100 m high at longitude 0.
        static double Cliff(Vector3D direction) => East(direction) < 0 ? 100 : 200;
        RoadProfile road = RoadProfile.For(Highway(), RadiusKm, Cliff, null)!;

        RoadTunnel tunnel = Assert.Single(road.Tunnels);
        Assert.InRange(East(road.Points[tunnel.First]), 0, RoadProfile.SampleMeters);
        Assert.True(tunnel.LengthMeters >= RoadProfile.MinTunnelMeters);
        for (int i = 0; i < road.Points.Count; i++)
        {
            double over = road.RoadMeters[i] - road.GroundMeters[i];
            Assert.True(over <= RoadProfile.MaxCutMeters + 1e-9, $"{over} at {i}");
            if (i < tunnel.First || i > tunnel.Last)
            {
                Assert.True(over >= -RoadProfile.MaxCutMeters - 1e-9, $"{over} at {i}");
            }
            else
            {
                Assert.True(over < -RoadProfile.MaxCutMeters, $"{over} at {i}");
            }
        }

        for (int i = tunnel.First + 1; i <= tunnel.Last; i++)
        {
            Assert.True(road.InTunnel(i - 1));
            double grade = Math.Abs(road.RoadMeters[i] - road.RoadMeters[i - 1])
                / (road.AlongMeters[i] - road.AlongMeters[i - 1]);
            Assert.True(grade <= RoadProfile.MaxGrade + 1e-9, $"{grade} at {i}");
        }

        Assert.False(road.InTunnel(tunnel.Last));
    }

    // A mesa 200 m high and 800 m across, centered on longitude 0, with a notch `notchMeters`
    // high (over the ground around the mesa) 16 m across in its middle (none: null).
    internal static Func<Vector3D, double> Mesa(double? notchMeters = null) => direction =>
    {
        double east = Math.Abs(East(direction));
        return east >= 400 ? 100 : east < 8 && notchMeters is double notch ? 100 + notch : 300;
    };

    [Fact]
    public void UnderAMesa_OneTunnelRunsThrough()
    {
        RoadProfile road = RoadProfile.For(Highway(), RadiusKm, Mesa(), null)!;

        RoadTunnel tunnel = Assert.Single(road.Tunnels);
        Assert.InRange(East(road.Points[tunnel.First]), -400, -390);
        Assert.InRange(East(road.Points[tunnel.Last]), 390, 400);
        Assert.Equal(road.AlongMeters[tunnel.Last] - road.AlongMeters[tunnel.First],
            tunnel.LengthMeters, 9);
    }

    [Fact]
    public void ANotchInTheHill_SplitsTheTunnel_OnlyWhereItLeavesTooLittleOverTheRoad()
    {
        RoadProfile whole = RoadProfile.For(Highway(), RadiusKm, Mesa(), null)!;
        int middle = Enumerable.Range(0, whole.Points.Count)
            .MinBy(i => Math.Abs(East(whole.Points[i])));
        double roadUp = whole.RoadMeters[middle] - 100;
        double cover = StoneTunnel.CoverMeters(4 + RoadCarving.ShoulderMeters);

        // Covered (yet shallower than a tunnel): one tunnel; too shallow to cover it: two.
        RoadProfile covered = RoadProfile.For(Highway(), RadiusKm, Mesa(roadUp + cover + 1),
            null)!;
        RoadProfile open = RoadProfile.For(Highway(), RadiusKm, Mesa(roadUp + cover - 3),
            null)!;

        Assert.Single(covered.Tunnels);
        Assert.Equal(2, open.Tunnels.Count);
        Assert.False(open.InTunnel(middle));
    }

    [Fact]
    public void AShortStretchTooDeepForACutting_IsCutDeeper_NotTunneled()
    {
        // A ridge 60 m high and only about 25 m across at its deepest cutting.
        static double Ridge(Vector3D direction) =>
            100 + 60 * Math.Exp(-Math.Pow(East(direction) / 12, 2));
        RoadProfile road = RoadProfile.For(Highway(), RadiusKm, Ridge, null)!;

        Assert.Empty(road.Tunnels);
        Assert.Contains(Enumerable.Range(0, road.Points.Count), i =>
            road.GroundMeters[i] - road.RoadMeters[i] > RoadProfile.MaxCutMeters);
    }

    [Fact]
    public void AtItsEnds_TheRoadComesDownToTheGround()
    {
        // A slope steeper than the grade, the whole way.
        static double Slope(Vector3D direction) => 1_000 + East(direction) / 8;
        RoadProfile road = RoadProfile.For(Highway(), RadiusKm, Slope, null)!;

        Assert.Equal(road.GroundMeters[0], road.RoadMeters[0], 9);
        Assert.Equal(road.GroundMeters[^1], road.RoadMeters[^1], 9);
        int middle = road.Points.Count / 2;
        Assert.True(Math.Abs(road.RoadMeters[middle] - road.GroundMeters[middle]) > 1);
    }

    [Fact]
    public void OverARiver_ItCrossesOnABridgeHighEnoughForItsArches_WithBanksUpToIt()
    {
        RoadProfile road = RoadProfile.For(Highway(), RadiusKm, _ => 100, Stream)!;

        RoadBridge bridge = Assert.Single(road.Bridges);
        double east = East(road.Points[bridge.First]), west = East(road.Points[bridge.Last]);
        Assert.True(east < -15 && west > 15, $"{east} to {west}");  // Past the water's edges
        Assert.Equal(west - east, bridge.LengthMeters, 0.01);
        double deck = bridge.WaterMeters + StoneBridge.ClearanceMeters(bridge.LengthMeters);
        Assert.True(road.RoadMeters[bridge.First] >= deck - 1e-9);
        Assert.True(road.RoadMeters[bridge.Last] >= deck - 1e-9);
        Assert.True(road.OnBridge(bridge.First) && !road.OnBridge(bridge.Last));
        Assert.True(road.OnBridge((bridge.First + bridge.Last) / 2));

        // The banks up to it climb no more steeply than the grade, and far off it's on the
        // ground again.
        for (int i = 1; i < road.Points.Count; i++)
        {
            double grade = Math.Abs(road.RoadMeters[i] - road.RoadMeters[i - 1])
                / (road.AlongMeters[i] - road.AlongMeters[i - 1]);
            Assert.True(grade <= RoadProfile.MaxGrade + 1e-9, $"{grade} at {i}");
        }

        Assert.Equal(100, road.RoadMeters[0], 9);
        Assert.Equal(100, road.RoadMeters[^1], 9);
    }

    [Fact]
    public void TheSameRoadAndGround_GiveTheSameProfile()
    {
        static double Hill(Vector3D direction) => 100 + 30 * Math.Sin(East(direction) / 300);
        RoadCourseShown course = Highway();
        RoadProfile first = RoadProfile.For(course, RadiusKm, Hill, Stream)!;
        RoadProfile second = RoadProfile.For(course, RadiusKm, Hill, Stream)!;

        Assert.Equal(first.RoadMeters, second.RoadMeters);
        Assert.Equal(first.Bridges, second.Bridges);
        RoadProfile mesa = RoadProfile.For(course, RadiusKm, Mesa(), null)!;
        Assert.Equal(mesa.Tunnels, RoadProfile.For(course, RadiusKm, Mesa(), null)!.Tunnels);
    }
}
