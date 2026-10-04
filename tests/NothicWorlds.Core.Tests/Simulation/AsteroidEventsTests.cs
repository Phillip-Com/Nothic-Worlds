using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class AsteroidEventsTests
{
    private const double Year = 365.25;
    private const double Au = 149_600_000;

    [Fact]
    public void APlanetInsideADenseBelt_HasManyClosePasses()
    {
        (World world, Body planet) = System(beltFromAu: 0.8, beltToAu: 1.2, density: 1);

        List<AsteroidEvent> events = AsteroidEvents.Between(world.Bodies, planet, 0, 50 * Year);

        double perYear = events.Count / 50.0;
        Assert.InRange(perYear, 0.8 * AsteroidEvents.PassesPerYear,
            1.2 * AsteroidEvents.PassesPerYear);
    }

    [Fact]
    public void APlanetFarFromAnyBelt_HasNone()
    {
        (World world, Body planet) = System(beltFromAu: 30, beltToAu: 40, density: 1);

        Assert.Empty(AsteroidEvents.Between(world.Bodies, planet, 0, 50 * Year));
    }

    [Fact]
    public void HalfTheDensity_GivesAboutHalfThePasses()
    {
        (World dense, Body densePlanet) = System(0.8, 1.2, density: 1);
        (World sparse, Body sparsePlanet) = System(0.8, 1.2, density: 0.5);

        int many = AsteroidEvents.Between(dense.Bodies, densePlanet, 0, 100 * Year).Count;
        int fewer = AsteroidEvents.Between(sparse.Bodies, sparsePlanet, 0, 100 * Year).Count;

        Assert.InRange((double)fewer / many, 0.4, 0.6);
    }

    [Fact]
    public void ImpactsAreRare_AndSayWhereTheyHit()
    {
        (World world, Body planet) = System(0.8, 1.2, density: 1);

        List<AsteroidEvent> events = AsteroidEvents.Between(world.Bodies, planet, 0, 500 * Year);
        List<AsteroidEvent> impacts = [.. events.Where(e => e.Kind == AsteroidEventKind.Impact)];

        // About 24 passes a year, 0.4% of them impacts: one every ten years or so.
        Assert.InRange(impacts.Count, 25, 80);
        Assert.All(impacts, impact => Assert.NotNull(impact.Spot));
        Assert.All(events.Where(e => e.Kind == AsteroidEventKind.ClosePass), pass =>
        {
            Assert.Null(pass.Spot);
            Assert.InRange(pass.DistanceKm, 3 * planet.RadiusKm, 1001 * planet.RadiusKm);
        });
        Assert.All(events, e => Assert.InRange(e.SizeMeters, 10, 5000));
    }

    [Fact]
    public void TheSameWorld_AlwaysHasTheSameEvents_HoweverItsAskedFor()
    {
        (World world, Body planet) = System(0.8, 1.2, density: 1);

        List<AsteroidEvent> whole = AsteroidEvents.Between(world.Bodies, planet, 0, 10 * Year);
        List<AsteroidEvent> halves =
        [
            .. AsteroidEvents.Between(world.Bodies, planet, 0, 4.3 * Year),
            .. AsteroidEvents.Between(world.Bodies, planet, 4.3 * Year, 10 * Year),
        ];

        Assert.Equal(whole, AsteroidEvents.Between(world.Bodies, planet, 0, 10 * Year));
        Assert.Equal(whole, halves);
        Assert.True(whole.Zip(whole.Skip(1)).All(pair => pair.First.TimeDays
            <= pair.Second.TimeDays));
    }

    [Fact]
    public void AMoon_SeesTheBeltItsPlanetRunsThrough()
    {
        (World world, Body planet) = System(0.8, 1.2, density: 1);
        Body moon = NewBodies.Moon(world.Bodies, planet);
        world.Bodies.Add(moon);

        Assert.NotEmpty(AsteroidEvents.Between(world.Bodies, moon, 0, 5 * Year));
    }

    [Fact]
    public void Exposure_IsFullInsideABelt_AndFadesOutside()
    {
        (World world, Body planet) = System(0.8, 1.2, density: 1);
        AsteroidBelt belt = world.Bodies.Single(b => b.Kind == BodyKind.Star).Belts[0];

        Assert.Equal(1, AsteroidEvents.Exposure(planet.Orbit!, belt), 9);

        double justOutside = AsteroidEvents.Exposure(
            planet.Orbit! with { DistanceKm = 1.3 * Au }, belt);
        double farOutside = AsteroidEvents.Exposure(
            planet.Orbit! with { DistanceKm = 2 * Au }, belt);
        Assert.InRange(justOutside, 0.1, 0.5);
        Assert.True(farOutside < 0.001);
    }

    // The default world (an Earth-like planet 1 AU from its star) with one belt around the star.
    private static (World World, Body Planet) System(
        double beltFromAu, double beltToAu, double density)
    {
        World world = World.CreateNew();
        Body star = world.Bodies.Single(b => b.Kind == BodyKind.Star);
        Body planet = world.Bodies.Single(b => b.Kind == BodyKind.Planet);
        planet.Orbit = planet.Orbit! with { DistanceKm = Au, PeriodDays = Year };
        star.Belts =
        [
            new AsteroidBelt(Guid.Parse("b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1"), "Belt",
                beltFromAu * Au, beltToAu * Au, 10, density, new RgbColor(1, 2, 3)),
        ];
        return (world, planet);
    }
}
