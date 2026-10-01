using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public class SeasonsTests
{
    [Fact]
    public void NorthPole_LeansByTheTilt_TowardTheDirection()
    {
        var body = new Body { AxialTiltDegrees = 30, AxialTiltDirectionDegrees = 90 };

        Vector3D pole = BodyOrientation.NorthPole(body);

        // Direction 90° is -Z (counterclockwise from +X, seen from the north).
        Assert.Equal(0, pole.X, 1e-12);
        Assert.Equal(Math.Cos(Math.PI / 6), pole.Y, 1e-12);
        Assert.Equal(-Math.Sin(Math.PI / 6), pole.Z, 1e-12);
    }

    [Fact]
    public void EarthLikePlanet_HasFourEvents_AQuarterYearApart()
    {
        (List<Body> bodies, Body planet) = EarthLike();

        List<SeasonEvent> events = Seasons.EventsBetween(bodies, planet, -1, 364);

        Assert.Equal(
            [SeasonEventKind.NorthernWinterSolstice, SeasonEventKind.NorthernSpringEquinox,
                SeasonEventKind.NorthernSummerSolstice, SeasonEventKind.NorthernAutumnEquinox],
            events.Select(e => e.Kind));
        for (int i = 0; i < events.Count; i++)
        {
            Assert.Equal(365.25 / 4 * i, events[i].TimeDays, 0.05);
        }
    }

    [Fact]
    public void Solstices_PutTheStarAsFarFromTheEquatorAsTheTilt()
    {
        (List<Body> bodies, Body planet) = EarthLike();
        Body sun = bodies[0];

        SeasonEvent summer = Seasons.EventsBetween(bodies, planet, 1, 365)
            .First(e => e.Kind == SeasonEventKind.NorthernSummerSolstice);
        SeasonEvent spring = Seasons.EventsBetween(bodies, planet, 1, 365)
            .First(e => e.Kind == SeasonEventKind.NorthernSpringEquinox);

        Assert.Equal(23.4,
            Seasons.StarDeclinationDegrees(bodies, planet, sun, summer.TimeDays), 1e-6);
        Assert.Equal(0,
            Seasons.StarDeclinationDegrees(bodies, planet, sun, spring.TimeDays), 1e-6);
    }

    [Fact]
    public void Seasons_AreOppositeInTheSouth()
    {
        (List<Body> bodies, Body planet) = EarthLike();

        Assert.Equal((Season.Summer, Season.Winter), Seasons.SeasonAt(bodies, planet, 200));
        Assert.Equal((Season.Winter, Season.Summer), Seasons.SeasonAt(bodies, planet, 30));
    }

    [Fact]
    public void NextEvent_IsTheComingOne()
    {
        (List<Body> bodies, Body planet) = EarthLike();

        SeasonEvent next = Seasons.NextEvent(bodies, planet, 100)!.Value;

        Assert.Equal(SeasonEventKind.NorthernSummerSolstice, next.Kind);
        Assert.Equal(365.25 / 2, next.TimeDays, 0.05);
    }

    [Fact]
    public void ElongatedOrbit_MakesSeasonsUneven()
    {
        // Kepler: the planet moves faster near the sun, so the seasons around its closest
        // approach are shorter.
        (List<Body> bodies, Body planet) = EarthLike();
        planet.Orbit = planet.Orbit! with { Eccentricity = 0.3 };

        List<SeasonEvent> events = Seasons.EventsBetween(bodies, planet, 1, 366);
        double[] lengths = [.. events.Zip(events.Skip(1), (a, b) => b.TimeDays - a.TimeDays)];

        Assert.Equal(4, events.Count);
        Assert.True(lengths.Max() - lengths.Min() > 30);
    }

    [Fact]
    public void NoTilt_OrNoStar_MeansNoSeasons()
    {
        (List<Body> bodies, Body planet) = EarthLike();
        planet.AxialTiltDegrees = 0;
        Assert.Empty(Seasons.EventsBetween(bodies, planet, 0, 400));
        Assert.Null(Seasons.SeasonAt(bodies, planet, 100));

        var lonePlanet = new Body { AxialTiltDegrees = 23.4 };
        Assert.Null(Seasons.SeasonAt([lonePlanet], lonePlanet, 100));
    }

    [Fact]
    public void PlanetCenteredSystem_HasTheSameSeasons()
    {
        (List<Body> bodies, Body planet) = EarthLike();
        List<SeasonEvent> before = Seasons.EventsBetween(bodies, planet, 1, 366);

        foreach ((Guid id, Orbit? orbit) in SystemHierarchy.MakeCenter(bodies, planet.Id))
        {
            bodies.First(b => b.Id == id).Orbit = orbit;
        }

        List<SeasonEvent> after = Seasons.EventsBetween(bodies, planet, 1, 366);
        Assert.Equal(before.Select(e => e.Kind), after.Select(e => e.Kind));
        for (int i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i].TimeDays, after[i].TimeDays, 1e-6);
        }
    }

    [Fact]
    public void Moon_GetsItsSeasonsFromTheStar()
    {
        (List<Body> bodies, Body planet) = EarthLike();
        var moon = new Body
        {
            Kind = BodyKind.Moon,
            AxialTiltDegrees = 10,
            Orbit = new Orbit { ParentId = planet.Id, DistanceKm = 384_400, PeriodDays = 27.3 },
        };
        bodies.Add(moon);

        Assert.Same(bodies[0], Seasons.StarFor(bodies, moon));
        Assert.Equal(4, Seasons.EventsBetween(bodies, moon, 1, 366).Count);
    }

    [Fact]
    public void SameWorldAndTime_AlwaysGivesTheSameEvents()
    {
        (List<Body> bodies, Body planet) = EarthLike();

        Assert.Equal(Seasons.EventsBetween(bodies, planet, 5, 700),
            Seasons.EventsBetween(bodies, planet, 5, 700));
    }

    // A sun and an Earth-like planet: tilt 23.4° leaning toward +X, starting at +X from the sun
    // (so the north pole leans away from it: northern winter solstice at time 0).
    private static (List<Body> Bodies, Body Planet) EarthLike()
    {
        var sun = new Body { Name = "Sun", Kind = BodyKind.Star, RadiusKm = 696_000 };
        var planet = new Body
        {
            Name = "Planet",
            AxialTiltDegrees = 23.4,
            Orbit = new Orbit
            {
                ParentId = sun.Id,
                DistanceKm = 149_600_000,
                PeriodDays = 365.25,
            },
        };
        return ([sun, planet], planet);
    }
}
