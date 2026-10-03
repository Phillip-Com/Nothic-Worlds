using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class MeteorShowerTests
{
    private const double Year = 365.25;
    private const double Au = CometTail.KmPerAu;

    [Fact]
    public void ACometCrossingTheOrbit_GivesTwoShowersAYear()
    {
        (World world, Body planet, Body comet) = SystemWithComet(closestAu: 0.6, elongation: 0.8);

        List<MeteorShower> showers = For(world, planet).Between(0, Year);

        Assert.Equal(2, showers.Count);
        Assert.All(showers, shower => Assert.Equal(comet.Id, shower.CometId));

        // Right through the dust: full strength, 10 an hour per km of the comet's radius.
        Assert.All(showers, shower => Assert.Equal(50, shower.PeakPerHour, 0));
        Assert.All(showers, shower => Assert.InRange(shower.EndDays - shower.StartDays, 5, 60));
    }

    [Fact]
    public void Showers_ComeBackOnTheSameDayEveryYear()
    {
        (World world, Body planet, _) = SystemWithComet(closestAu: 0.6, elongation: 0.8);

        List<MeteorShower> showers = For(world, planet).Between(0, 3 * Year);

        Assert.Equal(6, showers.Count);
        Assert.Equal(showers[0].PeakDays + Year, showers[2].PeakDays, 6);
        Assert.Equal(showers[1].PeakDays + 2 * Year, showers[5].PeakDays, 6);
    }

    [Fact]
    public void ThePeak_IsWhereThePlanetMeetsTheCometsOrbit()
    {
        (World world, Body planet, Body comet) = SystemWithComet(closestAu: 0.6, elongation: 0.8);

        foreach (MeteorShower shower in For(world, planet).Between(0, Year))
        {
            // The planet is on a circle, so the comet's orbit must reach the same distance
            // from the star in the planet's direction. Find the comet's point that way.
            Assert.True(DistanceToCometOrbit(planet, comet, shower.PeakDays) < 0.001 * Au);
        }
    }

    [Fact]
    public void ANearMiss_GivesAWeakerShower()
    {
        // Comes in to 1.05 AU: half the dust's width (0.1 AU) from the planet's 1 AU orbit.
        (World world, Body planet, _) = SystemWithComet(closestAu: 1.05, elongation: 0.5);

        MeteorShower shower = Assert.Single(For(world, planet).Between(0, Year));

        Assert.Equal(25, shower.PeakPerHour, 0);
    }

    [Fact]
    public void AFarAwayComet_GivesNoShowers()
    {
        (World world, Body planet, _) = SystemWithComet(closestAu: 2, elongation: 0.5);

        Assert.True(For(world, planet).IsEmpty);
    }

    [Fact]
    public void AMoon_SeesItsPlanetsShowers()
    {
        (World world, Body planet, _) = SystemWithComet(closestAu: 0.6, elongation: 0.8);
        Body moon = NewBodies.Moon(world.Bodies, planet);
        world.Bodies.Add(moon);

        Assert.Equal(For(world, planet).Between(0, Year), For(world, moon).Between(0, Year));
    }

    [Fact]
    public void StarsAndComets_SeeNoShowers()
    {
        (World world, _, Body comet) = SystemWithComet(closestAu: 0.6, elongation: 0.8);
        Body star = world.Bodies.Single(b => b.Kind == BodyKind.Star);

        Assert.True(For(world, star).IsEmpty);
        Assert.True(For(world, comet).IsEmpty);
    }

    [Fact]
    public void MakingThePlanetTheCenter_KeepsTheSameShowers()
    {
        (World world, Body planet, _) = SystemWithComet(closestAu: 0.6, elongation: 0.8);
        List<MeteorShower> before = For(world, planet).Between(0, Year);

        foreach ((Guid id, Orbit? orbit) in SystemHierarchy.MakeCenter(world.Bodies, planet.Id))
        {
            world.Bodies.Single(b => b.Id == id).Orbit = orbit;
        }

        List<MeteorShower> after = For(world, planet).Between(0, Year);
        Assert.Equal(before.Count, after.Count);
        for (int i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i].PeakDays, after[i].PeakDays, 4);
            Assert.Equal(before[i].PeakPerHour, after[i].PeakPerHour, 4);
        }
    }

    [Fact]
    public void TheRate_RisesToThePeakAndFallsAgain()
    {
        var shower = new MeteorShower(Guid.Empty, 10, 14, 20, 80);

        Assert.Equal(0, shower.PerHourAt(9));
        Assert.Equal(40, shower.PerHourAt(12), 9);
        Assert.Equal(80, shower.PerHourAt(14), 9);
        Assert.Equal(40, shower.PerHourAt(17), 9);
        Assert.Equal(0, shower.PerHourAt(20));
        Assert.True(shower.IsActiveAt(19.9));
        Assert.False(shower.IsActiveAt(20.1));
    }

    [Fact]
    public void ActiveAt_FindsTheShowerUnderWay()
    {
        (World world, Body planet, _) = SystemWithComet(closestAu: 0.6, elongation: 0.8);
        MeteorShowerTimeline timeline = For(world, planet);
        MeteorShower first = timeline.Between(0, Year)[0];

        Assert.Equal(first.PeakDays + Year, timeline.ActiveAt(first.PeakDays + Year)!.PeakDays, 6);
        Assert.Null(timeline.ActiveAt(first.EndDays + 0.01));
    }

    [Fact]
    public void Showers_AreTheSameEveryTime()
    {
        (World world, Body planet, _) = SystemWithComet(closestAu: 0.6, elongation: 0.8);

        Assert.Equal(For(world, planet).Between(0, Year), For(world, planet).Between(0, Year));
    }

    private static MeteorShowerTimeline For(World world, Body body) =>
        MeteorShowerTimeline.For(world.Bodies, body);

    // The default world (a Sun-like star and an Earth-like planet on a 1 AU circle) plus a 5 km
    // comet coming in to `closestAu` on a flat orbit with the given elongation.
    private static (World World, Body Planet, Body Comet) SystemWithComet(
        double closestAu, double elongation)
    {
        World world = World.CreateNew();
        Body star = world.Bodies.Single(b => b.Kind == BodyKind.Star);
        Body planet = world.Bodies.Single(b => b.Kind == BodyKind.Planet);
        planet.Orbit = planet.Orbit! with { DistanceKm = Au, PeriodDays = Year };
        var comet = new Body
        {
            Name = "Comet",
            Kind = BodyKind.Comet,
            RadiusKm = 5,
            Orbit = new Orbit
            {
                ParentId = star.Id,
                DistanceKm = closestAu * Au / (1 - elongation),
                PeriodDays = 2000,
                Eccentricity = elongation,
                ClosestApproachDegrees = 40,
            },
        };
        world.Bodies.Add(comet);
        return (world, planet, comet);
    }

    private static double DistanceToCometOrbit(Body planet, Body comet, double timeDays)
    {
        var where = OrbitMath.OffsetFromParent(planet.Orbit!, timeDays);
        return OrbitMath.EvenlySpacedTimes(comet.Orbit!, 100_000)
            .Min(time => (OrbitMath.OffsetFromParent(comet.Orbit!, time) - where).Length);
    }
}
