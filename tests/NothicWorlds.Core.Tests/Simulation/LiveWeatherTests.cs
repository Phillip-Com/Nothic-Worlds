using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class LiveWeatherTests
{
    private const byte Desert = 9;

    // ----- Which bodies have weather -----

    [Fact]
    public void OnlyPlanetsAndMoonsWithAirAndAStar_HaveWeather()
    {
        (World world, Body planet) = EarthLike();
        Body sun = world.Bodies.Single(b => b.Kind == BodyKind.Star);
        Body moon = NewBodies.Moon(world.Bodies, planet);
        world.Bodies.Add(moon);

        Assert.NotNull(LiveWeather.For(world.Bodies, planet, world.TerrainTypes));
        Assert.Null(LiveWeather.For(world.Bodies, moon, world.TerrainTypes));  // No air
        Assert.Null(LiveWeather.For(world.Bodies, sun, world.TerrainTypes));
        moon.HasAtmosphere = true;
        Assert.NotNull(LiveWeather.For(world.Bodies, moon, world.TerrainTypes));
        planet.HasAtmosphere = false;
        Assert.Null(LiveWeather.For(world.Bodies, planet, world.TerrainTypes));
    }

    [Fact]
    public void APlanetWithoutAStar_HasNoWeather()
    {
        (World world, Body planet) = EarthLike();
        world.Bodies.RemoveAll(b => b.Kind == BodyKind.Star);
        planet.Orbit = null;

        Assert.Null(LiveWeather.For(world.Bodies, planet, world.TerrainTypes));
    }

    // ----- Always the same -----

    [Fact]
    public void TheSameWorldAndTime_AlwaysGiveTheSameWeather()
    {
        (World world, Body planet) = EarthLike();
        LiveWeather first = LiveWeather.For(world.Bodies, planet, world.TerrainTypes)!;
        LiveWeather second = LiveWeather.For(world.Bodies, planet, world.TerrainTypes)!;

        foreach (double time in new[] { 0.0, 12.3, 200.75, -40.5, 5000.1 })
        {
            WeatherMoment a = first.At(time), b = second.At(time);
            Assert.Equal(a.Storms, b.Storms);
            foreach (GeoCoordinate spot in Spots())
            {
                Assert.Equal(a.SampleAt(spot), b.SampleAt(spot));
            }
        }
    }

    [Fact]
    public void Weather_ChangesSmoothlyFromMomentToMoment()
    {
        (World world, Body planet) = EarthLike();
        LiveWeather weather = LiveWeather.For(world.Bodies, planet, world.TerrainTypes)!;
        WeatherMoment now = weather.At(100), soon = weather.At(100.02);  // Half an hour on

        double change = Spots().Average(
            spot => Math.Abs(now.SampleAt(spot).CloudCover - soon.SampleAt(spot).CloudCover));

        Assert.True(change < 0.05, $"Clouds changed by {change:F3} on average");
    }

    [Fact]
    public void EverySpot_HasSensibleWeather_EvenAtThePoles()
    {
        (World world, Body planet) = EarthLike();
        WeatherMoment moment =
            LiveWeather.For(world.Bodies, planet, world.TerrainTypes)!.At(77.7);

        foreach (GeoCoordinate spot in Spots().Append(new(90, 0)).Append(new(-90, 0)))
        {
            WeatherSample sample = moment.SampleAt(spot);
            Assert.InRange(sample.CloudCover, 0, 1);
            Assert.True(sample.PrecipitationMmPerHour >= 0);
            Assert.True(double.IsFinite(sample.WindSpeedMs) && sample.WindSpeedMs < 100);
            Assert.InRange(sample.WindFromDegrees, 0, 360);
        }
    }

    // ----- The climate it stands on -----

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(45)]
    [InlineData(-45)]
    [InlineData(60)]
    public void AYearOfLiveRain_ComesToAboutTheClimatesRain(double latitude)
    {
        (World world, Body planet) = EarthLike();
        LiveWeather weather = LiveWeather.For(world.Bodies, planet, world.TerrainTypes)!;
        ClimateYear climate =
            ClimateYear.At(world.Bodies, planet, new GeoCoordinate(latitude, 0), 0)!;

        double live = YearlyRain(weather, latitude);
        double expected = climate.Days.Average(d => d.RainMm) * climate.YearDays;

        Assert.InRange(live / expected, 0.6, 1.5);
    }

    [Fact]
    public void TheDryBelt_IsDrierAndClearerThanTheEquator()
    {
        (World world, Body planet) = EarthLike();
        LiveWeather weather = LiveWeather.For(world.Bodies, planet, world.TerrainTypes)!;

        Assert.True(YearlyRain(weather, 30) < 0.3 * YearlyRain(weather, 0));
        Assert.True(YearlyCloud(weather, 30) < YearlyCloud(weather, 0) - 0.2);
    }

    [Fact]
    public void ADesertWorld_GetsFarLessRain()
    {
        (World world, Body planet) = EarthLike();
        double unpainted = YearlyRain(
            LiveWeather.For(world.Bodies, planet, world.TerrainTypes)!, 0);
        planet.Surface.Terrain = planet.Surface.Terrain  // Two half-globes cover it all
            .Paint(SphericalPolygon.ToUnit(new GeoCoordinate(90, 0)), 90, Desert)
            .Paint(SphericalPolygon.ToUnit(new GeoCoordinate(-90, 0)), 90, Desert);

        double desert = YearlyRain(
            LiveWeather.For(world.Bodies, planet, world.TerrainTypes)!, 0);

        Assert.True(desert < 0.4 * unpainted, $"{desert:F0} mm against {unpainted:F0} mm");
    }

    [Fact]
    public void AFrozenWorld_GetsSnowNotRain()
    {
        (World world, Body planet) = EarthLike();
        planet.AverageTemperatureC = -30;
        LiveWeather weather = LiveWeather.For(world.Bodies, planet, world.TerrainTypes)!;

        List<WeatherSample> samples = [.. Times(weather, 1.0)
            .SelectMany(time => Spots().Select(spot => weather.At(time).SampleAt(spot)))];

        Assert.Contains(samples, s => s.Precipitation == PrecipitationKind.Snow);
        Assert.DoesNotContain(samples, s => s.Precipitation == PrecipitationKind.Rain);
    }

    // ----- Winds -----

    [Theory]
    [InlineData(15, -1)]  // Trade winds blow toward the west
    [InlineData(-15, -1)]
    [InlineData(45, 1)]   // Westerlies toward the east
    [InlineData(-45, 1)]
    [InlineData(75, -1)]  // Polar easterlies toward the west
    public void ThePrevailingWinds_BlowInBelts(double latitude, int east)
    {
        (World world, Body planet) = EarthLike();
        LiveWeather weather = LiveWeather.For(world.Bodies, planet, world.TerrainTypes)!;

        double average = Times(weather, 5.0).Average(time => Longitudes()
            .Average(longitude => weather.At(time)
                .SampleAt(new GeoCoordinate(latitude, longitude)).WindEastMs));

        Assert.Equal(east, Math.Sign(average));
    }

    // ----- Storms -----

    [Fact]
    public void Cyclones_FormInTheStormBelts_AndMoveEastAndTowardThePole()
    {
        (World world, Body planet) = EarthLike();
        LiveWeather weather = LiveWeather.For(world.Bodies, planet, world.TerrainTypes)!;
        WeatherMoment now = weather.At(50);
        List<Storm> cyclones = [.. now.Storms.Where(s => s.Kind == StormKind.Cyclone)];

        Assert.InRange(cyclones.Count, 4, 16);
        Assert.All(cyclones, storm => Assert.InRange(
            Math.Abs(storm.Center.LatitudeDegrees), 25, 80));

        Storm storm = cyclones.First(s => 50 - s.BornDays < s.LifeDays - 1);
        Storm later = weather.At(51).Storms.Single(s => s.BornDays == storm.BornDays
            && s.Kind == StormKind.Cyclone);
        double east = (later.Center.LongitudeDegrees - storm.Center.LongitudeDegrees + 540)
            % 360 - 180;
        Assert.InRange(east, 3, 40);
        Assert.True(Math.Abs(later.Center.LatitudeDegrees)
            > Math.Abs(storm.Center.LatitudeDegrees));
    }

    [Theory]
    [InlineData(1)]   // Counterclockwise in the north: east of the middle, the wind blows north
    [InlineData(-1)]  // Clockwise in the south: it blows south there
    public void Storms_TurnTheRightWay(int hemisphere)
    {
        (World world, Body planet) = EarthLike();
        LiveWeather weather = LiveWeather.For(world.Bodies, planet, world.TerrainTypes)!;
        (WeatherMoment moment, Storm storm) = Times(weather, 0.5)
            .Select(time => weather.At(time))
            .SelectMany(m => m.Storms.Select(s => (m, s)))
            .First(pair => pair.s.Kind == StormKind.Cyclone && pair.s.Strength > 0.6
                && Math.Sign(pair.s.Center.LatitudeDegrees) == hemisphere);

        double latitude = storm.Center.LatitudeDegrees;
        double eastDegrees = double.RadiansToDegrees(0.45 * storm.RadiusKm
            / (planet.RadiusKm * Math.Cos(double.DegreesToRadians(latitude))));
        WeatherSample east = moment.SampleAt(
            new GeoCoordinate(latitude, storm.Center.LongitudeDegrees + eastDegrees));

        Assert.True(hemisphere * east.WindNorthMs > 3, $"{east.WindNorthMs:F1} m/s north");
    }

    [Fact]
    public void TropicalStorms_ComeInAWarmYear_AndNeverOnACoolWorld()
    {
        (World world, Body planet) = EarthLike();
        LiveWeather warm = LiveWeather.For(world.Bodies, planet, world.TerrainTypes)!;
        planet.AverageTemperatureC = -5;
        LiveWeather cool = LiveWeather.For(world.Bodies, planet, world.TerrainTypes)!;

        Assert.Contains(Times(warm, 1.0),
            time => warm.At(time).Storms.Any(s => s.Kind == StormKind.Tropical));
        Assert.DoesNotContain(Times(cool, 1.0),
            time => cool.At(time).Storms.Any(s => s.Kind == StormKind.Tropical));
    }

    [Fact]
    public void ACyclonesMiddle_IsCloudy_AndKnowsItsInAStorm()
    {
        (WeatherMoment moment, Storm storm, _) = StrongStorm(StormKind.Cyclone);

        WeatherSample middle = moment.SampleAt(storm.Center);

        Assert.True(middle.CloudCover > 0.6);
        Assert.Equal(StormKind.Cyclone, middle.Storm);
    }

    [Fact]
    public void ATropicalStorm_HasAClearEyeInsideACloudyWall()
    {
        (WeatherMoment moment, Storm storm, Body planet) = StrongStorm(StormKind.Tropical);
        double latitude = storm.Center.LatitudeDegrees;
        double wallDegrees = double.RadiansToDegrees(0.2 * storm.RadiusKm
            / (planet.RadiusKm * Math.Cos(double.DegreesToRadians(latitude))));

        WeatherSample eye = moment.SampleAt(storm.Center);
        WeatherSample wall = moment.SampleAt(
            new GeoCoordinate(latitude, storm.Center.LongitudeDegrees + wallDegrees));

        Assert.True(wall.CloudCover > 0.6, $"wall {wall.CloudCover:F2}");
        Assert.Equal(StormKind.Tropical, wall.Storm);
        Assert.True(wall.PrecipitationMmPerHour > eye.PrecipitationMmPerHour);
        Assert.True(wall.WindSpeedMs > 15);
    }

    // ----- Flat worlds -----

    [Fact]
    public void AFlatWorld_HasWeatherWithoutBelts()
    {
        (World world, Body planet) = EarthLike();
        planet.Shape = BodyShape.FlatDisc;
        WeatherMoment moment =
            LiveWeather.For(world.Bodies, planet, world.TerrainTypes)!.At(30);

        Assert.All(Spots(), spot => Assert.InRange(moment.SampleAt(spot).CloudCover, 0, 1));
        Assert.DoesNotContain(moment.Storms, s => s.Kind == StormKind.Tropical);
    }

    // The first storm of a kind at more than 0.7 strength, through a year.
    private static (WeatherMoment Moment, Storm Storm, Body Planet) StrongStorm(StormKind kind)
    {
        (World world, Body planet) = EarthLike();
        LiveWeather weather = LiveWeather.For(world.Bodies, planet, world.TerrainTypes)!;
        (WeatherMoment moment, Storm storm) = Times(weather, 0.5)
            .Select(time => weather.At(time))
            .SelectMany(m => m.Storms.Select(s => (m, s)))
            .First(pair => pair.s.Kind == kind && pair.s.Strength > 0.7);
        return (moment, storm, planet);
    }

    private static double YearlyRain(LiveWeather weather, double latitude) =>
        Times(weather, 0.25).Average(time => Longitudes().Average(longitude => weather.At(time)
            .SampleAt(new GeoCoordinate(latitude, longitude)).PrecipitationMmPerHour * 24))
        * weather.YearDays;

    private static double YearlyCloud(LiveWeather weather, double latitude) =>
        Times(weather, 1.0).Average(time => Longitudes().Average(longitude => weather.At(time)
            .SampleAt(new GeoCoordinate(latitude, longitude)).CloudCover));

    private static IEnumerable<double> Times(LiveWeather weather, double stepDays)
    {
        for (double time = 0; time < weather.YearDays; time += stepDays)
        {
            yield return time;
        }
    }

    private static IEnumerable<double> Longitudes() =>
        Enumerable.Range(0, 12).Select(i => -180.0 + i * 30);

    private static IEnumerable<GeoCoordinate> Spots() =>
        from latitude in new[] { -75.0, -45, -15, 0, 15, 45, 75 }
        from longitude in Longitudes()
        select new GeoCoordinate(latitude, longitude);

    // A new world's planet, with a fixed ID: storms are made up from it, so every run of the
    // tests sees the same ones.
    private static (World World, Body Planet) EarthLike()
    {
        World world = World.CreateNew();
        Body made = world.Bodies[0];
        var planet = new Body
        {
            Id = Guid.Parse("0e0e0e0e-1111-2222-3333-444444444444"),
            Name = made.Name,
            Kind = made.Kind,
            RadiusKm = made.RadiusKm,
            DayLengthHours = made.DayLengthHours,
            AxialTiltDegrees = made.AxialTiltDegrees,
            AxialTiltDirectionDegrees = 180,  // Leaning toward the sun at time 0
            Orbit = made.Orbit,
        };
        world.Bodies[0] = planet;
        return (world, planet);
    }
}
