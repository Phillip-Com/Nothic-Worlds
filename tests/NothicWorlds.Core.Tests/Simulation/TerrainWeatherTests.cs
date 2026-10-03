using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class TerrainWeatherTests
{
    private const byte Ocean = 1;
    private const byte Forest = 5;
    private const byte Mountains = 8;
    private const byte Desert = 9;
    private const byte Ice = 12;

    private static readonly GeoCoordinate _spot = new(45, 0);

    [Fact]
    public void AnUnpaintedPlanet_HasNoWaterAndNoGround()
    {
        (World world, Body planet) = EarthLike();

        TerrainSurroundings around = TerrainSurroundings.At(planet, world.TerrainTypes, _spot);

        Assert.Null(around.Here);
        Assert.Equal(0, around.WaterShare);
        Assert.Equal(500, around.RadiusKm);
    }

    [Fact]
    public void OpenSea_IsAllWater()
    {
        (World world, Body planet) = EarthLike();
        Paint(planet, _spot, 10, Ocean);

        TerrainSurroundings around = TerrainSurroundings.At(planet, world.TerrainTypes, _spot);

        Assert.Equal(ClimateKind.Water, around.Here);
        Assert.Equal(1, around.WaterShare);
        Assert.Equal(1, around.Maritime);
    }

    [Fact]
    public void ACoast_IsPartlyWater()
    {
        // A wide band of sea just east of the spot, which stands in a forest.
        (World world, Body planet) = EarthLike();
        planet.Surface.Terrain = planet.Surface.Terrain
            .PaintStroke(Dir(30, 5), Dir(60, 5), 3, Ocean)
            .Paint(Dir(45, 0), 0.3, Forest);

        TerrainSurroundings around = TerrainSurroundings.At(planet, world.TerrainTypes, _spot);

        Assert.Equal(ClimateKind.Forest, around.Here);
        Assert.InRange(around.WaterShare, 0.3, 0.7);
    }

    [Fact]
    public void FarFromTheSea_IsNoWater()
    {
        (World world, Body planet) = EarthLike();
        Paint(planet, new GeoCoordinate(45, 20), 2, Ocean);  // About 1,600 km away

        TerrainSurroundings around = TerrainSurroundings.At(planet, world.TerrainTypes, _spot);

        Assert.Equal(0, around.WaterShare);
    }

    [Fact]
    public void OnASmallMoon_TheAreaShrinks()
    {
        var moon = new Body { Kind = BodyKind.Moon, RadiusKm = 500 };

        TerrainSurroundings around = TerrainSurroundings.At(moon, TerrainType.Defaults, _spot);

        Assert.Equal(150, around.RadiusKm);
    }

    [Fact]
    public void TerrainOfAnUnknownType_CountsAsUnpainted()
    {
        (World world, Body planet) = EarthLike();
        Paint(planet, _spot, 10, 200);  // No type has code 200

        Assert.Null(TerrainSurroundings.At(planet, world.TerrainTypes, _spot).Here);
    }

    [Fact]
    public void WithoutTerrain_TheWeatherIsUnchanged()
    {
        (World world, Body planet) = EarthLike();
        TerrainSurroundings unpainted = TerrainSurroundings.At(planet, world.TerrainTypes, _spot);

        Assert.Equal(Year(world, planet).Days, Year(world, planet, unpainted).Days);
    }

    [Fact]
    public void TheSea_SoftensAndDelaysTheSeasons_AndTheDayNightSwing()
    {
        (World world, Body planet) = EarthLike();
        ClimateYear inland = Year(world, planet);
        Paint(planet, _spot, 10, Ocean);
        ClimateYear sea = Year(world, planet,
            TerrainSurroundings.At(planet, world.TerrainTypes, _spot));

        Assert.True(SeasonalRange(sea) < 0.6 * SeasonalRange(inland));
        Assert.True(Swing(sea) < 0.5 * Swing(inland));
        Assert.True(WarmestDay(sea) > WarmestDay(inland) + 10);
        Assert.Equal(Mean(inland), Mean(sea), 0);  // The yearly average stays.
    }

    [Theory]
    [InlineData(Desert, 2.0, 2.0)]
    [InlineData(Forest, 0.8, 0.0)]
    [InlineData(Mountains, 1.2, -6.0)]
    [InlineData(Ice, 1.0, -8.0)]
    public void TheGroundAtTheSpot_ChangesTheSwingAndTheMean(
        byte code, double swingFactor, double meanChange)
    {
        (World world, Body planet) = EarthLike();
        ClimateYear plain = Year(world, planet);
        Paint(planet, _spot, 1, code);
        ClimateYear year = Year(world, planet,
            TerrainSurroundings.At(planet, world.TerrainTypes, _spot));

        Assert.Equal(Swing(plain) * swingFactor, Swing(year), 6);
        Assert.Equal(Mean(plain) + meanChange, Mean(year), 6);
        Assert.Equal(ClimateKindOf(code), year.Terrain!.Here);
    }

    [Fact]
    public void TheWeather_RemembersTheTerrainItAllowedFor()
    {
        (World world, Body planet) = EarthLike();
        TerrainSurroundings around = TerrainSurroundings.At(planet, world.TerrainTypes, _spot);

        Assert.Same(around, Year(world, planet, around).Terrain);
        Assert.Null(Year(world, planet).Terrain);
    }

    private static ClimateKind ClimateKindOf(byte code) =>
        TerrainType.Defaults.Single(t => t.Code == code).Climate;

    private static ClimateYear Year(
        World world, Body planet, TerrainSurroundings? terrain = null) =>
        ClimateYear.At(world.Bodies, planet, _spot, 0, terrain)!;

    private static double SeasonalRange(ClimateYear year) =>
        year.Days.Max(d => d.MeanC) - year.Days.Min(d => d.MeanC);

    private static double Swing(ClimateYear year) => year.Days[0].HighC - year.Days[0].LowC;

    private static double Mean(ClimateYear year) => year.Days.Average(d => d.MeanC);

    private static int WarmestDay(ClimateYear year) =>
        year.Days.ToList().IndexOf(year.Days.MaxBy(d => d.MeanC)!);

    private static void Paint(Body planet, GeoCoordinate centre, double radius, byte code)
    {
        planet.Surface.Terrain = planet.Surface.Terrain.Paint(
            SphericalPolygon.ToUnit(centre), radius, code);
    }

    private static Vector3D Dir(double latitude, double longitude) =>
        SphericalPolygon.ToUnit(new GeoCoordinate(latitude, longitude));

    private static (World World, Body Planet) EarthLike()
    {
        World world = World.CreateNew();
        Body planet = world.Bodies[0];
        planet.AxialTiltDirectionDegrees = 180;  // Leaning toward the sun at time 0
        return (world, planet);
    }
}
