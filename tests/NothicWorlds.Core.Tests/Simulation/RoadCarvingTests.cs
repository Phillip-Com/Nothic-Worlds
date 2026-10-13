using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
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

    [Fact]
    public void OnAnOldRoad_PlantsGrowOnItsPavingAndSides_ButNotOnAHighways()
    {
        RoadCarving old = RoadCarving.For([RoadProfile.For(
            RoadProfileTests.Highway(style: RoadStyle.OldRoad), RadiusKm, _ => 100,
            null)!], RadiusKm);

        Assert.True(old.OnOldPaving(Beside(0)));
        Assert.True(old.OnOldPaving(Beside(3.9)));
        Assert.False(old.OnOldPaving(Beside(4.5)));
        Assert.False(_level.OnOldPaving(Beside(0)));

        // Plants grow there (but rough terrain still fades toward it).
        Assert.Equal(0, old.BeyondSidesMeters(Beside(0), 1_000));
        Assert.Equal(1_000, old.BeyondSidesMeters(Beside(0), 1_000, oldRoads: false));
        Assert.Equal(0, _level.BeyondSidesMeters(Beside(0), 1_000, oldRoads: false));
    }

    [Fact]
    public void BeyondSides_IsTheNearestRoadsAsMeasuredOnEverySegment()
    {
        // Rough terrain asks this for every height, out to tens of km: measuring every road
        // segment that far out took most of each frame beside a road on the owner's world.
        RoadProfile[] roads =
        [
            RoadProfile.For(RoadProfileTests.Highway(), RadiusKm, _ => 100, null)!,
            RoadProfile.For(RoadProfileTests.Highway(), RadiusKm, RoadProfileTests.Mesa(),
                null)!,
            _bridged,
            RoadProfile.For(RoadProfileTests.Highway(style: RoadStyle.OldRoad), RadiusKm,
                _ => 100, null)!,
        ];
        var random = new Random(3);
        foreach (RoadProfile[] some in (RoadProfile[][])[[roads[0]], [roads[1]], roads])
        {
            RoadCarving carving = RoadCarving.For(some, RadiusKm);
            for (int i = 0; i < 300; i++)
            {
                Vector3D point = Beside((random.NextDouble() - 0.5) * 3_000,
                    (random.NextDouble() - 0.2) * 3_000);
                foreach (double within in (double[])[30, 1_000, 50_000])
                {
                    foreach ((bool tunnels, bool oldRoads) in (ReadOnlySpan<(bool, bool)>)
                        [(true, true), (false, true), (true, false)])
                    {
                        Assert.Equal(EverySegment(some, point, within, tunnels, oldRoads),
                            carving.BeyondSidesMeters(point, within, tunnels, oldRoads), 6);
                    }
                }
            }
        }
    }

    // BeyondSidesMeters measured on every segment of every road: past the sides of each
    // road's nearest segment (by its middle line), unless the point is past the road's ends,
    // or that segment is in a tunnel (without `tunnels`) or the road old (without `oldRoads`).
    private static double EverySegment(IReadOnlyList<RoadProfile> roads, Vector3D point,
        double within, bool tunnels, bool oldRoads)
    {
        double radiusMeters = RadiusKm * 1000, nearest = within;
        foreach (RoadProfile road in roads)
        {
            (double Away, double Raw, int Segment) best = (double.MaxValue, 0, -1);
            for (int s = 0; s < road.Points.Count - 1; s++)
            {
                Vector3D from = road.Points[s], along = road.Points[s + 1] - from;
                double raw = (point - from).Dot(along) / along.Dot(along);
                double away = (point - (from + along * Math.Clamp(raw, 0, 1))).Length
                    * radiusMeters;
                if (away < best.Away)
                {
                    best = (away, raw, s);
                }
            }

            int last = road.Points.Count - 2;
            if ((best.Segment == 0 && best.Raw < 0) || (best.Segment == last && best.Raw > 1)
                || (!tunnels && road.InTunnel(best.Segment))
                || (!oldRoads && road.Surface == RoadSurface.OldPaving))
            {
                continue;
            }

            int seg = best.Segment;
            double height = Math.Max(Math.Abs(road.RoadMeters[seg] - road.GroundMeters[seg]),
                Math.Abs(road.RoadMeters[seg + 1] - road.GroundMeters[seg + 1]));
            nearest = Math.Min(nearest, Math.Max(0,
                best.Away - RoadCarving.ReachMeters(road.HalfWidthMeters, height)));
        }

        return nearest;
    }
}
