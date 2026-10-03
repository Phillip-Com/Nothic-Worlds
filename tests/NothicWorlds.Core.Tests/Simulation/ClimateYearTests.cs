using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

// An Earth-like planet whose northern summer solstice falls at time 0 (its axis leans toward
// the sun then), so northern winter's solstice is half a year later.
public class ClimateYearTests
{
    [Fact]
    public void Daylight_MatchesEarthAtMidLatitudes()
    {
        (World world, Body planet) = EarthLike();

        ClimateYear year = YearAt(world, planet, 45);

        // Earth at 45°: about 15.4 hours at midsummer, 8.6 at midwinter (no refraction).
        Assert.Equal(15.4, year.Days.Max(d => d.DaylightHours), 0.15);
        Assert.Equal(8.6, year.Days.Min(d => d.DaylightHours), 0.15);
    }

    [Fact]
    public void BeyondThePolarCircle_TheSunStaysUpOrDown()
    {
        (World world, Body planet) = EarthLike();

        ClimateYear year = YearAt(world, planet, 80);

        Assert.Equal(24, year.Days.Max(d => d.DaylightHours), 1e-9);
        Assert.Equal(0, year.Days.Min(d => d.DaylightHours), 1e-9);
        Assert.True(year.Days.Min(d => d.NoonSunDegrees) < 0);  // Polar night: never rises
    }

    [Fact]
    public void TheNoonSun_ClimbsByTheTilt()
    {
        (World world, Body planet) = EarthLike();

        ClimateYear year = YearAt(world, planet, 45);

        Assert.Equal(90 - 45 + 23.4, year.Days.Max(d => d.NoonSunDegrees), 0.2);
        Assert.Equal(90 - 45 - 23.4, year.Days.Min(d => d.NoonSunDegrees), 0.2);
    }

    [Fact]
    public void WithoutTilt_EveryDayIsTwelveHours()
    {
        (World world, Body planet) = EarthLike();
        planet.AxialTiltDegrees = 0;

        ClimateYear year = YearAt(world, planet, 60);

        Assert.All(year.Days, d => Assert.Equal(12, d.DaylightHours, 1e-6));
    }

    [Fact]
    public void Temperatures_AreEarthLike_AndFallTowardThePoles()
    {
        (World world, Body planet) = EarthLike();

        double equator = YearAt(world, planet, 0).Days.Average(d => d.MeanC);
        double middle = YearAt(world, planet, 45).Days.Average(d => d.MeanC);
        double pole = YearAt(world, planet, 90).Days.Average(d => d.MeanC);

        Assert.InRange(equator, 25, 31);
        Assert.InRange(middle, 6, 13);
        Assert.InRange(pole, -25, -5);
    }

    [Fact]
    public void TheWarmestDay_LagsTheSolstice_ByAboutAMonth()
    {
        (World world, Body planet) = EarthLike();

        ClimateYear year = YearAt(world, planet, 45);
        ClimateDay warmest = year.Days.MaxBy(d => d.MeanC);
        ClimateDay coldest = year.Days.MinBy(d => d.MeanC);

        Assert.InRange(warmest.TimeDays, 15, 45);  // Midsummer is at day 0
        Assert.InRange(coldest.TimeDays, 182.6 + 15, 182.6 + 45);
        Assert.InRange(warmest.MeanC - coldest.MeanC, 15, 30);  // A mid-latitude year's swing
    }

    [Fact]
    public void TheHemispheres_HaveOppositeSeasons()
    {
        (World world, Body planet) = EarthLike();

        ClimateYear north = YearAt(world, planet, 45);
        ClimateYear south = YearAt(world, planet, -45);

        Assert.Equal(north.Days.Average(d => d.MeanC), south.Days.Average(d => d.MeanC), 1e-3);
        Assert.True(north.DayAt(30).MeanC > south.DayAt(30).MeanC);
        Assert.True(north.DayAt(210).MeanC < south.DayAt(210).MeanC);
    }

    [Fact]
    public void TheAverageTemperature_ShiftsEverything()
    {
        (World world, Body planet) = EarthLike();
        ClimateDay before = YearAt(world, planet, 30).DayAt(100);

        planet.AverageTemperatureC = 25;
        ClimateDay after = YearAt(world, planet, 30).DayAt(100);

        Assert.Equal(before.MeanC + 10, after.MeanC, 1e-9);
        Assert.Equal(before.LowC + 10, after.LowC, 1e-9);
    }

    [Fact]
    public void AnElongatedOrbit_BrightensTheClosestApproach()
    {
        // No tilt, so only the distance changes the light: (1+e)²/(1-e)² brighter at closest.
        (World world, Body planet) = EarthLike();
        planet.AxialTiltDegrees = 0;
        planet.Orbit = planet.Orbit! with { Eccentricity = 0.3 };

        ClimateYear year = YearAt(world, planet, 0);
        double ratio = year.Days.Max(d => d.Sunlight) / year.Days.Min(d => d.Sunlight);

        Assert.Equal(Math.Pow(1.3 / 0.7, 2), ratio, 0.05);
    }

    [Fact]
    public void LongerDays_SwingMoreBetweenDayAndNight()
    {
        (World world, Body planet) = EarthLike();
        ClimateDay day = YearAt(world, planet, 20).DayAt(50);
        planet.DayLengthHours = 96;
        ClimateDay longDay = YearAt(world, planet, 20).DayAt(50);

        Assert.Equal(10, day.HighC - day.LowC, 1e-9);
        Assert.Equal(20, longDay.HighC - longDay.LowC, 1e-9);
    }

    [Fact]
    public void TheYearRepeats_AndAveragesOverSpans()
    {
        (World world, Body planet) = EarthLike();
        ClimateYear year = YearAt(world, planet, 45);

        Assert.Equal(year.DayAt(40).MeanC, year.DayAt(40 + 365.25 * 3).MeanC, 1e-9);
        Assert.Equal(year.DayAt(40).MeanC, year.DayAt(40 - 365.25).MeanC, 1e-9);
        ClimateDay july = year.Average(15, 45);
        Assert.InRange(july.MeanC, year.DayAt(15).MeanC - 3, year.DayAt(45).MeanC + 3);
    }

    [Fact]
    public void StarsAndStarlessBodies_HaveNoWeather()
    {
        (World world, Body planet) = EarthLike();
        var spot = new GeoCoordinate(10, 10);

        Assert.Null(ClimateYear.At(world.Bodies, world.Bodies[1], spot, 0));
        var lonePlanet = new Body();
        Assert.Null(ClimateYear.At([lonePlanet], lonePlanet, spot, 0));
    }

    [Fact]
    public void SameSpotAndYear_AlwaysGivesTheSameWeather()
    {
        (World world, Body planet) = EarthLike();

        Assert.Equal(YearAt(world, planet, 33).Days, YearAt(world, planet, 33).Days);
    }

    private static ClimateYear YearAt(World world, Body planet, double latitude) =>
        ClimateYear.At(world.Bodies, planet, new GeoCoordinate(latitude, 0), 0)!;

    private static (World World, Body Planet) EarthLike()
    {
        World world = World.CreateNew();
        Body planet = world.Bodies[0];
        planet.AxialTiltDirectionDegrees = 180;  // Leaning toward the sun at time 0
        return (world, planet);
    }
}
