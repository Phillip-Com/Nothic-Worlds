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

    [Theory]
    [InlineData(0, 10)]
    [InlineData(45, 10)]
    [InlineData(-30, 190)]
    public void TheSolarTime_IsNoonWhenTheSunIsHighest(double latitude, double day)
    {
        (World world, Body planet) = EarthLike();

        SkyView noon = DayOfSky(world, planet, new GeoCoordinate(latitude, 0), day)
            .MaxBy(s => s.Star!.AltitudeDegrees)!;

        Assert.InRange(noon.SolarTimeHours!.Value, 11.9, 12.1);
    }

    [Fact]
    public void TheSolarTime_IsAboutSixAtSunrise_AndEighteenAtSunset()
    {
        (World world, Body planet) = EarthLike();
        List<SkyView> skies = DayOfSky(world, planet, new GeoCoordinate(0, 0), 80);

        int rise = Enumerable.Range(1, skies.Count - 1).First(
            i => skies[i - 1].Star!.AltitudeDegrees <= 0 && skies[i].Star!.AltitudeDegrees > 0);
        int set = Enumerable.Range(1, skies.Count - 1).First(
            i => skies[i - 1].Star!.AltitudeDegrees > 0 && skies[i].Star!.AltitudeDegrees <= 0);

        Assert.InRange(skies[rise].SolarTimeHours!.Value, 5.75, 6.25);
        Assert.InRange(skies[set].SolarTimeHours!.Value, 17.75, 18.25);
    }

    [Fact]
    public void ThereIsNoSolarTime_AtAPole()
    {
        (World world, Body planet) = EarthLike();

        SkyView sky = SkyView.From(world.Bodies, planet, new GeoCoordinate(90, 0), 0, 10)!;

        Assert.Null(sky.SolarTimeHours);
    }

    [Fact]
    public void TheBodyAtADirection_IsTheOneWhoseDiscIsThere()
    {
        (World world, Body planet) = EarthLike();
        SkyView sky = SkyView.From(world.Bodies, planet, new GeoCoordinate(0, 0), 0, 10.3)!;
        SkyBody sun = sky.Star!;

        Assert.Equal(sun.BodyId, sky.BodyAt(sun.East, sun.North, sun.Up, 0, 0.1)?.BodyId);
        Assert.Null(sky.BodyAt(-sun.East, -sun.North, -sun.Up, 0, 0.1));

        // Two degrees off: missed at true size, caught when drawn at least three degrees wide.
        (double e, double n, double u) = Tilted(sun, 2);
        Assert.Null(sky.BodyAt(e, n, u, 0, 0.1));
        Assert.Equal(sun.BodyId, sky.BodyAt(e, n, u, 1.5, 0.6)?.BodyId);
    }

    [Fact]
    public void WhereDiscsOverlap_TheNearestBodyIsSeen()
    {
        (World world, Body planet) = EarthLike();
        Body moon = NewBodies.Moon(world.Bodies, planet);
        world.Bodies.Add(moon);

        // The moment in a month when the moon passes closest to the sun.
        SkyView sky = Enumerable.Range(0, 400)
            .Select(i => SkyView.From(world.Bodies, planet, new GeoCoordinate(0, 0), 0,
                i * moon.Orbit!.PeriodDays / 400)!)
            .MinBy(s => Apart(s.Bodies.Single(b => b.BodyId == moon.Id), s.Star!))!;
        SkyBody sun = sky.Star!;

        Assert.Equal(moon.Id, sky.BodyAt(sun.East, sun.North, sun.Up, 10, 0)?.BodyId);
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

    // A direction turned `degrees` away from a body's, toward the zenith (or, for a body
    // overhead, toward the north).
    private static (double East, double North, double Up) Tilted(SkyBody body, double degrees)
    {
        var at = new Vector3D(body.East, body.North, body.Up);
        Vector3D toward = Math.Abs(body.Up) < 0.99 ? new Vector3D(0, 0, 1) : new Vector3D(0, 1, 0);
        Vector3D side = toward - at * toward.Dot(at);
        side *= 1 / side.Length;
        double angle = double.DegreesToRadians(degrees);
        Vector3D turned = at * Math.Cos(angle) + side * Math.Sin(angle);
        return (turned.X, turned.Y, turned.Z);
    }

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
