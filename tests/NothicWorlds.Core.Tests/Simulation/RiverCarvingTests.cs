using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class RiverCarvingTests
{
    private const double RadiusKm = 6371;
    private const double MetersPerDegree = RadiusKm * 1000 * Math.PI / 180;

    // A river 100 m wide at its mouth running east along the equator over level ground 100 m
    // up.
    private static readonly RiverProfile _river = RiverProfile.For(
        RiverProfileTests.Drawn(0.1, WaterGround.At(0, -1), WaterGround.At(0, 1)), RadiusKm,
        _ => 100)!;

    private static readonly RiverCarving _carving = RiverCarving.For([_river], RadiusKm);

    // The same river, set 40 m deep at its mouth.
    private static RiverProfile DeepRiver()
    {
        RiverCourseShown course = RiverProfileTests.Drawn(0.1, WaterGround.At(0, -1),
            WaterGround.At(0, 1));
        var depth = new RiverDepth(MouthMeters: 40);
        return RiverProfile.For(course with { Depth = depth }, RadiusKm, _ => 100)!;
    }

    // A spot `north` meters north of the river, at longitude `east` degrees.
    private static Vector3D Beside(double north, double east = 0.01) =>
        WaterGround.At(north / MetersPerDegree, east);

    [Fact]
    public void TheRiverRunsInABed_WithBanksRisingToTheGround()
    {
        int middle = _river.Points.Count / 2;
        double halfWidth = _river.HalfWidthMeters[middle];
        double bed = _carving.Carve(Beside(0), 100);
        Assert.Equal(_river.BedMeters[middle], bed, 0);
        double bank = _carving.Carve(Beside(halfWidth + 4), 100);

        // Its half-width here is within a few tenths of a meter of the middle's.
        Assert.InRange(bank - bed, 4 * RiverCarving.BankSlope - 0.25,
            4 * RiverCarving.BankSlope + 0.25);
        Assert.Equal(100, _carving.Carve(Beside(halfWidth + 500), 100));
    }

    [Fact]
    public void BeyondBanks_MeasuresFromTheBanksOut()
    {
        int middle = _river.Points.Count / 2;
        double halfWidth = _river.HalfWidthMeters[middle];
        double reach = halfWidth + RiverCarving.BankWidthMeters(halfWidth);

        Assert.Equal(0, _carving.BeyondBanksMeters(Beside(0), 10_000));
        Assert.Equal(0, _carving.BeyondBanksMeters(Beside(reach - 1), 10_000));
        Assert.Equal(500, _carving.BeyondBanksMeters(Beside(reach + 500), 10_000), 0);
        Assert.Equal(2_000, _carving.BeyondBanksMeters(Beside(reach + 5_000), 2_000));
    }

    [Fact]
    public void ADeeperBed_KeepsTheBanksAboveTheWater_SteeperBelowIt()
    {
        RiverProfile deep = DeepRiver();
        RiverCarving carving = RiverCarving.For([deep], RadiusKm);
        int middle = deep.Points.Count / 2;
        double halfWidth = deep.HalfWidthMeters[middle];
        double edge = deep.WaterHalfWidthMeters[middle];
        double east = RiverProfileTests.Longitude(deep.Points[middle]);
        double Carve(RiverCarving each, double north) => each.Carve(Beside(north, east), 100);

        Assert.Equal(deep.BedMeters[middle], Carve(carving, 0), 0);
        Assert.Equal(deep.WaterMeters[middle], Carve(carving, edge), 0);
        // Above the water it's carved just as at Auto's depth.
        Assert.Equal(Carve(_carving, edge + 3), Carve(carving, edge + 3), 1);
        // Below it the bank drops more steeply than BankSlope.
        double underwater = Carve(carving, (halfWidth + edge) / 2);
        Assert.True(underwater < deep.WaterMeters[middle]
            - (edge - halfWidth) / 2 * RiverCarving.BankSlope - 5);
    }

    [Fact]
    public void UnderTheStrip_CoarserGroundSinksFurther_UnderSteeperBanks()
    {
        // Its squares' flat faces would otherwise poke through the steep banks under the water.
        RiverProfile deep = DeepRiver();
        RiverCarving carving = RiverCarving.For([deep], RadiusKm);
        int middle = deep.Points.Count / 2;
        Vector3D bed = Beside(0, RiverProfileTests.Longitude(deep.Points[middle]));
        double steeper = deep.WaterMeters[middle] - deep.BedMeters[middle]
            - (deep.WaterHalfWidthMeters[middle] - deep.HalfWidthMeters[middle])
            * RiverCarving.BankSlope;

        double sunk = carving.UnderStripNear(bed, 1e-5, 2)!(bed, 100);

        Assert.True(steeper > 10);
        Assert.Equal(carving.Carve(bed, 100) - 0.5 - 0.4 * 2 - steeper, sunk, 6);
    }

    [Fact]
    public void ItsCarvedAtEveryDistance_TheSameWhereverItsAskedFrom()
    {
        // The river's whole length, far beyond the few kilometers once carved.
        foreach (double east in new[] { -0.9, -0.3, 0.2, 0.8 })
        {
            Assert.True(_carving.Carve(Beside(0, east), 100) < 99);
        }

        // A tile's ground is carved just as the ground at each point is.
        Func<Vector3D, double, double> tile = _carving.UnderStripNear(Beside(0, 0.5), 1e-4,
            cellMeters: 0.01)!;
        Vector3D bed = Beside(0, 0.5);
        Assert.Equal(_carving.Carve(bed, 100), tile(bed, 100) + 0.5 + 0.004, 6);
    }

    [Fact]
    public void UnderTheStrip_CoarserGroundSinksMore_ForWiderSquares()
    {
        Vector3D bed = Beside(0);
        double carved = _carving.Carve(bed, 100);
        double fine = _carving.UnderStripNear(bed, 1e-5, 2)!(bed, 100);
        double coarse = _carving.UnderStripNear(bed, 1e-3, 200)!(bed, 100);
        Assert.Equal(carved - 0.5 - 0.4 * 2, fine, 6);
        Assert.Equal(carved - 0.5 - 0.4 * 200, coarse, 6);

        // Past the banks, it fades out over a square's width.
        double halfWidth = _river.HalfWidthMeters[_river.Points.Count / 2];
        double banks = halfWidth + RiverCarving.BankWidthMeters(halfWidth);
        Assert.Equal(100, _carving.UnderStripNear(bed, 1e-3, 200)!(
            Beside(banks + 250), 100));
        Assert.True(_carving.UnderStripNear(bed, 1e-3, 200)!(Beside(banks + 100), 100) < 100);
    }

    [Fact]
    public void OnSquaresFarWiderThanItsChannel_TheRiverIsntCarved()
    {
        // Its channel and banks reach out 80 m here: lost in squares 10 km wide.
        Assert.NotNull(_carving.UnderStripNear(Beside(0), 0.01, 4_000));
        Assert.Null(_carving.UnderStripNear(Beside(0), 0.01, 10_000));
    }

    [Fact]
    public void ATileNoRiverComesNear_IsLeftAlone()
    {
        Assert.Null(_carving.UnderStripNear(WaterGround.At(1, 0), 1e-4, 10));
        Assert.Equal(100, _carving.Carve(WaterGround.At(1, 0), 100));
        Assert.True(RiverCarving.For([], RadiusKm).IsEmpty);
    }
}
