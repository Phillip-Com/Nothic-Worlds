using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public class RoughGroundTests
{
    private const double EarthKm = 6_371;
    private const int Seed = 12_345;
    private const byte Crags = 1, Meadow = 2;
    private static readonly Vector3D _faceMiddle = new(0, 0, 1);

    [Fact]
    public void NoRoughness_OrNoVariation_GivesNoRelief()
    {
        Assert.Equal(0, Relief(roughness: 0, variation: 1_500));
        Assert.Equal(0, Relief(roughness: 0.7, variation: 0));
    }

    [Fact]
    public void TheSamePlace_AlwaysGivesTheSameRelief()
    {
        Assert.Equal(Relief(0.7, 1_500), Relief(0.7, 1_500));
    }

    [Fact]
    public void RougherGround_RisesAndFallsMore()
    {
        Assert.True(Spread(0.9) > Spread(0.3));
    }

    [Fact]
    public void MountainsGetCrags_TensOfMetersAcrossAHundredMeters()
    {
        // Across 100 m of craggy mountain ground, the fine relief alone climbs and drops by
        // meters, not centimeters (and not kilometers).
        double most = 0;
        for (int i = 0; i < 200; i++)
        {
            Vector3D here = Place(i * 0.37);
            Vector3D along = Unit(here + new Vector3D(100 / (EarthKm * 1000), 0, 0));
            most = Math.Max(most, Math.Abs(Relief(0.7, 1_500, here) - Relief(0.7, 1_500, along)));
        }

        Assert.InRange(most, 3, 200);
    }

    [Fact]
    public void GroundDrawnCoarsely_LeavesOutOnlyTheFinestFeatures()
    {
        Vector3D here = Place(1.3);
        double fine = Relief(0.7, 1_500, here, smallestMeters: 20);
        double coarse = Relief(0.7, 1_500, here, smallestMeters: 160);

        Assert.NotEqual(fine, coarse);
        Assert.InRange(Math.Abs(fine - coarse), 0, 0.2 * Spread(0.7) + 5);
    }

    [Fact]
    public void For_IsNullWhenNothingPaintedIsRough()
    {
        TerrainGrid terrain = TerrainGrid.Empty.Paint(_faceMiddle, 5, Meadow);

        Assert.Null(RoughGround.For(terrain, Types(), EarthKm, Seed));
        Assert.Null(RoughGround.For(TerrainGrid.Empty, Types(), EarthKm, Seed));
        Assert.NotNull(RoughGround.For(terrain.Paint(_faceMiddle, 1, Crags), Types(), EarthKm,
            Seed));
    }

    [Fact]
    public void Relief_CarriesOnAcrossTheLineBetweenTwoTypes()
    {
        // Crags on one side of the face's middle, smooth meadow on the other: stepping across
        // the line in 5 m steps, the relief never jumps.
        TerrainGrid terrain = TerrainGrid.Empty.Paint(_faceMiddle, 10, Meadow)
            .Paint(Unit(new Vector3D(0.2, 0, 1)), 10, Crags);
        RoughGround ground = RoughGround.For(terrain, Types(), EarthKm, Seed)!;
        double step = 5 / (EarthKm * 1000), biggest = 0;
        double before = ground.OffsetAt(_faceMiddle, 20);
        for (int i = 1; i < 4_000; i++)
        {
            double now = ground.OffsetAt(Unit(new Vector3D(i * step, 0, 1)), 20);
            biggest = Math.Max(biggest, Math.Abs(now - before));
            before = now;
        }

        Assert.InRange(biggest, 0, 3);
    }

    [Fact]
    public void OneTypeEverywhere_GivesThatTypesOwnRelief()
    {
        TerrainGrid terrain = TerrainGrid.Empty.Paint(_faceMiddle, 10, Crags);
        RoughGround ground = RoughGround.For(terrain, Types(), EarthKm, Seed)!;
        double cellKm = EarthKm * Math.PI / 2 / TerrainGrid.FaceSize;

        Assert.Equal(TerrainRoughness.Offset(_faceMiddle, EarthKm, 1_500, 40, 3 * cellKm, 0.7,
            20, Seed + Crags * 7_919), ground.OffsetAt(_faceMiddle, 20), 9);
    }

    private static TerrainType[] Types() =>
    [
        new(Crags, "Crags", new RgbColor(0x7D, 0x6E, 0x62), ClimateKind.Mountains, 2_500, 0.5,
            1_500, 40, 0.7),
        new(Meadow, "Meadow", new RgbColor(0xA8, 0xC6, 0x6C), ClimateKind.OpenLand, 150, 0,
            40, 80, 0),
    ];

    private static Vector3D Unit(Vector3D v) => v * (1 / v.Length);

    private static Vector3D Place(double t) =>
        Unit(new Vector3D(Math.Sin(t) * 0.3, Math.Cos(t * 1.7) * 0.3, 1));

    private static double Relief(double roughness, int variation, Vector3D? place = null,
        double smallestMeters = 20)
    {
        double cellKm = EarthKm * Math.PI / 2 / TerrainGrid.FaceSize;
        return TerrainRoughness.Offset(place ?? Place(0.5), EarthKm, variation, 40, 3 * cellKm,
            roughness, smallestMeters, Seed);
    }

    // How far the relief strays from level, on average over many places.
    private static double Spread(double roughness) =>
        Enumerable.Range(0, 400).Average(i => Math.Abs(Relief(roughness, 1_500, Place(i))));
}
