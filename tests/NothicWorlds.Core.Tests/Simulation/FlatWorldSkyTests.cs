using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class FlatWorldSkyTests
{
    private const byte Ocean = 1;

    [Fact]
    public void AFlatWorld_HasTwoSummersAndTwoWintersAYear()
    {
        (World world, Body planet) = Flat(tilt: 23.4);
        List<SeasonEvent> globe = Seasons.EventsBetween(
            world.Bodies, Globe(planet), 10, 375.25);

        List<SeasonEvent> flat = Seasons.EventsBetween(world.Bodies, planet, 10, 375.25);

        Assert.Equal(4, flat.Count);
        Assert.Equal(2, flat.Count(e => e.Kind == SeasonEventKind.Midsummer));
        Assert.Equal(2, flat.Count(e => e.Kind == SeasonEventKind.Midwinter));
        Assert.Equal(globe.Select(e => e.TimeDays), flat.Select(e => e.TimeDays));
        Assert.All(flat.Zip(globe), pair => Assert.Equal(
            pair.Second.Kind is SeasonEventKind.NorthernSpringEquinox
                or SeasonEventKind.NorthernAutumnEquinox,
            pair.First.Kind == SeasonEventKind.Midsummer));
    }

    [Fact]
    public void AfterMidsummer_ItsSummerEverywhere()
    {
        Assert.Equal((Season.Summer, Season.Summer),
            Seasons.SeasonsAfter(SeasonEventKind.Midsummer));
        Assert.Equal((Season.Winter, Season.Winter),
            Seasons.SeasonsAfter(SeasonEventKind.Midwinter));
    }

    [Fact]
    public void EveryPlace_SharesOneSky()
    {
        (World world, Body planet) = Flat(tilt: 23.4);

        ClimateYear north = ClimateYear.At(world.Bodies, planet, new(70, 0), 0)!;
        ClimateYear south = ClimateYear.At(world.Bodies, planet, new(-70, 120), 0)!;

        for (int i = 0; i < north.Days.Count; i += 30)
        {
            Assert.Equal(12, north.Days[i].DaylightHours, 9);  // Half of a 24-hour day
            Assert.Equal(north.Days[i].NoonSunDegrees, south.Days[i].NoonSunDegrees, 9);
            Assert.Equal(north.Days[i].MeanC, south.Days[i].MeanC, 9);
            Assert.Equal(north.Days[i].RainMm, south.Days[i].RainMm, 9);
        }
    }

    [Fact]
    public void TheNoonSun_IsHighestAtMidsummerAndLowestAtMidwinter()
    {
        (World world, Body planet) = Flat(tilt: 23.4);
        ClimateYear year = ClimateYear.At(world.Bodies, planet, new(0, 0), 0)!;

        Assert.Equal(90, year.Days.Max(d => d.NoonSunDegrees), 0);
        Assert.Equal(90 - 23.4, year.Days.Min(d => d.NoonSunDegrees), 0);
    }

    [Fact]
    public void TheYear_AveragesTheBodysTemperature_WithTwoWarmSpells()
    {
        (World world, Body planet) = Flat(tilt: 60);  // A big tilt, for strong seasons
        ClimateYear year = ClimateYear.At(world.Bodies, planet, new(30, 0), 0)!;

        Assert.Equal(planet.AverageTemperatureC, year.Days.Average(d => d.MeanC), 1);

        // Warm spells after each midsummer (half a year apart), cold ones between.
        double[] means = [.. year.Days.Select(d => d.MeanC)];
        int warmest = Array.IndexOf(means, means.Max());
        int halfYear = means.Length / 2;
        double opposite = means[(warmest + halfYear) % means.Length];
        double between = means[(warmest + halfYear / 2) % means.Length];
        Assert.True(opposite - between > 0.5 * (means.Max() - between));
    }

    [Fact]
    public void NearbyWater_IsMeasuredAcrossTheFace()
    {
        // A sea just east of a far-south spot: its edge is near on a globe (about 220 km), but
        // across the disc, where the south is stretched around the rim, over 1,000 km away.
        (World world, Body planet) = Flat(tilt: 23.4);
        var spot = new GeoCoordinate(-60, 0);
        planet.Surface.Terrain = planet.Surface.Terrain.Paint(
            SphericalPolygon.ToUnit(new GeoCoordinate(-60, 6)), 1, Ocean);

        TerrainSurroundings flat = TerrainSurroundings.At(planet, world.TerrainTypes, spot);
        TerrainSurroundings globe =
            TerrainSurroundings.At(Globe(planet), world.TerrainTypes, spot);

        Assert.True(globe.WaterShare > 0);
        Assert.Equal(0, flat.WaterShare);
    }

    private static (World World, Body Planet) Flat(double tilt)
    {
        World world = World.CreateNew();
        Body planet = world.Bodies.Single(b => b.Kind == BodyKind.Planet);
        planet.Shape = BodyShape.FlatDisc;
        planet.AxialTiltDegrees = tilt;
        return (world, planet);
    }

    private static Body Globe(Body flat)
    {
        Body globe = flat.Clone();
        globe.Shape = BodyShape.Sphere;
        return globe;
    }
}
