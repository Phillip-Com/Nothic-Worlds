using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class SkyViewTests
{
    // Minutes between looks at the sky when following it through a day.
    private const double StepMinutes = 2;

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(23.4, 180, 12.3)]
    [InlineData(97, 45, 101.7)]
    public void TheBodysNorth_TurnsToItsNorthPole(double tilt, double direction, double time)
    {
        var body = new Body { AxialTiltDegrees = tilt, AxialTiltDirectionDegrees = direction };

        Vector3D north = BodyOrientation.ToSystem(body, time, new Vector3D(0, 1, 0));
        Vector3D pole = BodyOrientation.NorthPole(body);

        Assert.True((north - pole).Length < 1e-9, $"{north} against {pole}");
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(45, 10)]
    [InlineData(-30, 10)]
    [InlineData(45, 190)]   // Half a year on: the other season
    public void TheSunsHighestInADay_IsTheClimatesNoonHeight(double latitude, double day)
    {
        (World world, Body planet) = EarthLike();
        var spot = new GeoCoordinate(latitude, 0);
        ClimateYear climate = ClimateYear.At(world.Bodies, planet, spot, 0)!;

        double highest = DayOfSky(world, planet, spot, day).Max(s => s.Star!.AltitudeDegrees);

        Assert.InRange(highest, climate.DayAt(day + 0.5).NoonSunDegrees - 0.6,
            climate.DayAt(day + 0.5).NoonSunDegrees + 0.6);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(50, 10)]
    [InlineData(50, 190)]
    public void TheSunIsUpForTheClimatesDaylight(double latitude, double day)
    {
        (World world, Body planet) = EarthLike();
        var spot = new GeoCoordinate(latitude, 0);
        ClimateYear climate = ClimateYear.At(world.Bodies, planet, spot, 0)!;

        double hoursUp = DayOfSky(world, planet, spot, day)
            .Count(s => s.Star!.AltitudeDegrees > 0) * StepMinutes / 60;

        Assert.InRange(hoursUp, climate.DayAt(day + 0.5).DaylightHours - 0.25,
            climate.DayAt(day + 0.5).DaylightHours + 0.25);
    }

    [Fact]
    public void TheSunRisesInTheEast_AndSetsInTheWest()
    {
        (World world, Body planet) = EarthLike();
        List<SkyView> skies = DayOfSky(world, planet, new GeoCoordinate(0, 0), 80);

        int rise = Enumerable.Range(1, skies.Count - 1).First(
            i => skies[i - 1].Star!.AltitudeDegrees <= 0 && skies[i].Star!.AltitudeDegrees > 0);
        int set = Enumerable.Range(1, skies.Count - 1).First(
            i => skies[i - 1].Star!.AltitudeDegrees > 0 && skies[i].Star!.AltitudeDegrees <= 0);

        Assert.InRange(skies[rise].Star!.AzimuthDegrees, 45, 135);
        Assert.InRange(skies[set].Star!.AzimuthDegrees, 225, 315);
    }

    [Fact]
    public void TheSun_LooksAboutHalfADegreeAcross()
    {
        (World world, Body planet) = EarthLike();

        SkyView sky = SkyView.From(world.Bodies, planet, new GeoCoordinate(0, 0), 0, 10)!;

        Assert.InRange(sky.Star!.AngularDiameterDegrees, 0.5, 0.56);
        Assert.Equal(1, sky.Star.LitFraction);
        Assert.DoesNotContain(sky.Bodies, b => b.BodyId == planet.Id);
    }

    [Fact]
    public void AMoon_GoesThroughItsPhases_AndIsFullOppositeTheSun()
    {
        (World world, Body planet) = EarthLike();
        Body moon = NewBodies.Moon(world.Bodies, planet);
        world.Bodies.Add(moon);

        List<(SkyBody Moon, SkyBody Sun)> month = [.. Enumerable.Range(0, 60)
            .Select(i => SkyView.From(world.Bodies, planet, new GeoCoordinate(0, 0), 0,
                i * moon.Orbit!.PeriodDays / 60)!)
            .Select(sky => (sky.Bodies.Single(b => b.BodyId == moon.Id), sky.Star!))];

        Assert.True(month.Min(m => m.Moon.LitFraction) < 0.05);
        Assert.True(month.Max(m => m.Moon.LitFraction) > 0.95);
        (SkyBody Moon, SkyBody Sun) opposite = month.MaxBy(m => Apart(m.Moon, m.Sun));
        Assert.True(opposite.Moon.LitFraction > 0.95, $"{opposite.Moon.LitFraction:F2}");
        Assert.InRange(month[0].Moon.AngularDiameterDegrees, 0.45, 0.6);
    }

    [Fact]
    public void OnlyGlobesHaveASkyWorkedOut()
    {
        (World world, Body planet) = EarthLike();
        Body sun = world.Bodies.Single(b => b.Kind == BodyKind.Star);

        Assert.Null(SkyView.From(world.Bodies, sun, new GeoCoordinate(0, 0), 0, 0));
        planet.Shape = BodyShape.FlatDisc;
        Assert.Null(SkyView.From(world.Bodies, planet, new GeoCoordinate(0, 0), 0, 0));
    }

    [Fact]
    public void TheSameMoment_AlwaysGivesTheSameSky()
    {
        (World world, Body planet) = EarthLike();
        var spot = new GeoCoordinate(33.3, -71.2);

        SkyView first = SkyView.From(world.Bodies, planet, spot, 2, 123.45)!;
        SkyView second = SkyView.From(world.Bodies, planet, spot, 2, 123.45)!;

        Assert.Equal(first.Bodies, second.Bodies);
    }

    // The sky every StepMinutes through one standard day from `day`.
    private static List<SkyView> DayOfSky(World world, Body planet, GeoCoordinate spot,
        double day) =>
        [.. Enumerable.Range(0, (int)(24 * 60 / StepMinutes))
            .Select(i => SkyView.From(world.Bodies, planet, spot, 0,
                day + i * StepMinutes / (24 * 60))!)];

    // The angle between two bodies in the sky, in degrees.
    private static double Apart(SkyBody a, SkyBody b) => double.RadiansToDegrees(Math.Acos(
        Math.Clamp(a.East * b.East + a.North * b.North + a.Up * b.Up, -1, 1)));

    private static (World World, Body Planet) EarthLike()
    {
        World world = World.CreateNew();
        Body planet = world.Bodies[0];
        planet.AxialTiltDirectionDegrees = 180;  // Leaning toward the sun at time 0
        return (world, planet);
    }
}
