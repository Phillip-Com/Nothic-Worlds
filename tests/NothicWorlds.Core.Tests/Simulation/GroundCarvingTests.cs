using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class GroundCarvingTests
{
    private const double RadiusKm = RoadProfileTests.RadiusKm;
    private const double MetersPerDegree = RoadProfileTests.MetersPerDegree;

    // The stream, and a highway crossing it on a bridge.
    private static readonly RoadProfile _road = RoadProfile.For(RoadProfileTests.Highway(),
        RadiusKm, _ => 100, RoadProfileTests.Stream)!;

    private static readonly GroundCarving _carving = GroundCarving.Of(RoadProfileTests.Stream,
        RoadCarving.For([_road], RadiusKm));

    private static Vector3D At(double northMeters, double eastMeters) =>
        WaterGround.At(northMeters / MetersPerDegree, eastMeters / MetersPerDegree);

    [Fact]
    public void TheRiversCut_ThenTheRoadsShape_ButNotUnderABridge()
    {
        // Under the bridge, the river's bed.
        double bed = RoadProfileTests.Stream.Carve(At(0, 0), 100);
        Assert.Equal(bed, _carving.Carve(At(0, 0), 100), 9);

        // On the road's bank up to the bridge, the road (above the ground).
        RoadBridge bridge = Assert.Single(_road.Bridges);
        int approach = bridge.Last + 3;
        Vector3D onRoad = _road.Points[approach];
        Assert.Equal(_road.RoadMeters[approach], _carving.Carve(onRoad, 100), 6);
        Assert.True(_road.RoadMeters[approach] > 100);

        // Far from both, the ground.
        Assert.Equal(100, _carving.Carve(At(2_000, 2_000), 100), 9);
        Assert.NotNull(_carving.DeckMeters(At(0, 0)));
    }

    [Fact]
    public void BeyondBanks_IsTheNearerOfTheRiversAndTheRoads()
    {
        Assert.Equal(0, _carving.BeyondBanksMeters(At(0, 1_000), 500));      // On the road
        Assert.Equal(0, _carving.BeyondBanksMeters(At(1_000, 0), 500));      // In the river
        Assert.Equal(500, _carving.BeyondBanksMeters(At(2_000, 2_000), 500));
    }

    [Fact]
    public void WithNeither_NothingIsCarved()
    {
        GroundCarving none = GroundCarving.Of(null, null);

        Assert.True(none.IsEmpty);
        Assert.Equal(42, none.Carve(At(0, 0), 42));
        Assert.Null(none.UnderStripNear(At(0, 0), 1e-4, 10));
        Assert.Null(none.DeckMeters(At(0, 0)));
    }

    [Fact]
    public void CoarserGround_IsSunkUnderBothStrips()
    {
        Func<Vector3D, double, double> under = _carving.UnderStripNear(At(0, 500), 1e-4, 20)!;

        Assert.True(under(At(0, 500), 100) < _road.RoadMeters[^1] - 0.4 * 20);
        Assert.True(under(At(1_000, 0), 100) < RoadProfileTests.Stream.Carve(At(1_000, 0), 100));
        Assert.Equal(100, under(At(3_000, 3_000), 100), 9);
    }
}
