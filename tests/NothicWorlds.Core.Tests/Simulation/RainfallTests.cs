using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class RainfallTests
{
    [Theory]
    [InlineData(0, 1400, 2200)]    // Rainy all year near the equator
    [InlineData(30, 150, 450)]     // The dry belt
    [InlineData(-30, 150, 450)]
    [InlineData(50, 700, 1300)]    // Storms in the middle latitudes
    [InlineData(85, 0, 80)]        // Polar desert
    public void AnEarthLikePlanet_GetsEarthLikeRainfall(double latitude, double least, double most)
    {
        (World world, Body planet) = EarthLike();

        Assert.InRange(YearlyRain(Year(world, planet, latitude)), least, most);
    }

    [Fact]
    public void NearTheTropics_TheWetSeasonFollowsTheSun()
    {
        // Leaning toward the sun at time 0: northern summer comes first, southern half a year on.
        (World world, Body planet) = EarthLike();
        ClimateYear north = Year(world, planet, 15);
        double half = north.YearDays / 2;

        double northernSummer = MonthlyRain(north, 15, 45);
        double northernWinter = MonthlyRain(north, half + 15, half + 45);

        Assert.True(northernSummer > 5 * northernWinter);
        ClimateYear south = Year(world, planet, -15);
        Assert.True(MonthlyRain(south, half + 15, half + 45) > 5 * MonthlyRain(south, 15, 45));
    }

    [Fact]
    public void WithoutTerrain_TheMoistureIsAverage()
    {
        Assert.Equal(1.0, ClimateYear.Moisture(null));
        Assert.Equal(1.0, ClimateYear.Moisture(new TerrainSurroundings(null, 0, 500)));
    }

    [Fact]
    public void SeaAir_IsWetter_AndInland_Drier()
    {
        double coast =
            ClimateYear.Moisture(new TerrainSurroundings(ClimateKind.OpenLand, 0.8, 500));
        double inland =
            ClimateYear.Moisture(new TerrainSurroundings(ClimateKind.OpenLand, 0.0, 500));

        Assert.True(coast > 1.0);
        Assert.True(inland < 0.5);
    }

    [Theory]
    [InlineData(ClimateKind.Desert, 0.15)]
    [InlineData(ClimateKind.Ice, 0.5)]
    [InlineData(ClimateKind.Forest, 1.2)]
    [InlineData(ClimateKind.Wetland, 1.3)]
    [InlineData(ClimateKind.Mountains, 1.3)]
    public void TheGround_ScalesTheRain(ClimateKind ground, double factor)
    {
        double open = ClimateYear.Moisture(new TerrainSurroundings(ClimateKind.OpenLand, 0.5, 500));

        Assert.Equal(open * factor,
            ClimateYear.Moisture(new TerrainSurroundings(ground, 0.5, 500)), 9);
    }

    [Fact]
    public void ADesert_IsFarDrierThanTheSameSpotUnpainted()
    {
        (World world, Body planet) = EarthLike();
        var spot = new GeoCoordinate(0, 0);
        double plain = YearlyRain(Year(world, planet, 0));
        planet.Surface.Terrain = planet.Surface.Terrain
            .Paint(SphericalPolygon.ToUnit(spot), 10, 9);  // Desert

        ClimateYear desert = ClimateYear.At(world.Bodies, planet, spot, 0,
            TerrainSurroundings.At(planet, world.TerrainTypes, spot))!;

        Assert.True(YearlyRain(desert) < 0.1 * plain);
    }

    [Fact]
    public void TheMonthlyAverage_IncludesRain()
    {
        (World world, Body planet) = EarthLike();
        ClimateYear year = Year(world, planet, 50);

        ClimateDay month = year.Average(0, 30);

        Assert.InRange(month.RainMm, 0.5, 5);
        Assert.Equal(year.Days.Take(30).Average(d => d.RainMm), month.RainMm, 1);
    }

    [Fact]
    public void Rain_IsDeterministic()
    {
        (World world, Body planet) = EarthLike();

        Assert.Equal(Year(world, planet, 20).Days, Year(world, planet, 20).Days);
    }

    private static ClimateYear Year(World world, Body planet, double latitude) =>
        ClimateYear.At(world.Bodies, planet, new GeoCoordinate(latitude, 0), 0)!;

    private static double YearlyRain(ClimateYear year) =>
        year.Days.Average(d => d.RainMm) * year.YearDays;

    private static double MonthlyRain(ClimateYear year, double from, double to) =>
        year.Average(from, to).RainMm * (to - from);

    private static (World World, Body Planet) EarthLike()
    {
        World world = World.CreateNew();
        Body planet = world.Bodies[0];
        planet.AxialTiltDirectionDegrees = 180;  // Leaning toward the sun at time 0
        return (world, planet);
    }
}
