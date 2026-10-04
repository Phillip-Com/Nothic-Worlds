using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class OrbitStabilityTests
{
    private const double EarthDistanceKm = 149_600_000;

    [Fact]
    public void TheEarth_HasItsRealHillSphere_AndTheSunHasNoLimit()
    {
        (World world, Body sun, Body earth) = SunAndEarth();

        // Real value: about 1.5 million km.
        Assert.InRange(OrbitStability.HillRadiusKm(world.Bodies, earth), 1.45e6, 1.55e6);
        Assert.True(double.IsPositiveInfinity(OrbitStability.HillRadiusKm(world.Bodies, sun)));
    }

    [Fact]
    public void TheEarthsRocheLimit_ForTheMoon_IsItsRealOne()
    {
        (_, _, Body earth) = SunAndEarth();

        // Real value: about 18,400 km for the Moon's density (fluid Roche limit).
        Assert.InRange(OrbitStability.RocheLimitKm(earth, 3.34), 18_000, 18_800);
    }

    [Fact]
    public void TheEarthsMoonZone_RunsFromItsRocheLimit_ToHalfItsHillSphere()
    {
        (World world, _, Body earth) = SunAndEarth();

        OrbitZone zone = OrbitStability.MoonZone(world.Bodies, earth)!;

        Assert.Equal(OrbitStability.RocheLimitKm(earth, OrbitStability.TypicalMoonDensity),
            zone.InnerKm);
        Assert.Equal(OrbitStability.HillRadiusKm(world.Bodies, earth) / 2, zone.OuterKm);
        Assert.InRange(384_400, zone.InnerKm, zone.OuterKm);  // The Moon fits.
    }

    [Fact]
    public void ABodyCloseToSomethingHeavy_HasNoMoonZone()
    {
        (World world, Body sun, _) = SunAndEarth();
        Body scorched = Planet(world, sun, "Scorched", 1_000_000);

        Assert.Null(OrbitStability.MoonZone(world.Bodies, scorched));
    }

    [Fact]
    public void ACenterPlanet_IsLimitedByTheStarCirclingIt_AsIfItCircledTheStar()
    {
        (World world, _, Body earth) = SunAndEarth();
        double hill = OrbitStability.HillRadiusKm(world.Bodies, earth);

        MakeCenter(world, earth);

        Assert.Equal(hill, OrbitStability.HillRadiusKm(world.Bodies, earth), 6);
    }

    [Fact]
    public void TheNaturalPeriod_AtTheEarthsDistance_IsAYear()
    {
        (World world, _, Body earth) = SunAndEarth();

        Assert.Equal(365.25, OrbitStability.NaturalPeriodDays(world.Bodies, earth)!.Value, 0);
    }

    [Fact]
    public void RealmsAndTheCenter_HaveNoNaturalPeriod()
    {
        (World world, Body sun, Body earth) = SunAndEarth();
        Body tree = NewBodies.WorldTree(world.Bodies, sun);
        world.Bodies.Add(tree);
        var realm = new Body { Name = "Asgard", Branch = 0, Orbit = Realms.OrbitOnBranch(tree, 0) };
        world.Bodies.Add(realm);

        Assert.Null(OrbitStability.NaturalPeriodDays(world.Bodies, sun));
        Assert.Null(OrbitStability.NaturalPeriodDays(world.Bodies, realm));
    }

    [Fact]
    public void ASystemLikeOurs_HasNoWarnings()
    {
        (World world, Body sun, Body earth) = SunAndEarth();
        Moon(world, earth, "Moon", 384_400, radiusKm: 1737);
        Planet(world, sun, "Mars", 227_900_000, radiusKm: 3390, eccentricity: 0.093);
        Planet(world, sun, "Jupiter", 778_500_000, radiusKm: 69_911, eccentricity: 0.049);
        Planet(world, sun, "Saturn", 1_433_500_000, radiusKm: 58_232, eccentricity: 0.057);

        Assert.Empty(OrbitStability.Warnings(world.Bodies));
    }

    [Fact]
    public void AMoonInsideTheRocheLimit_IsWarnedAbout()
    {
        (World world, _, Body earth) = SunAndEarth();
        Body moon = Moon(world, earth, "Shard", 12_000);

        OrbitWarning warning = Assert.Single(OrbitStability.Warnings(world.Bodies));

        Assert.Equal(moon.Id, warning.BodyId);
        Assert.Contains("Roche limit", warning.Message);
    }

    [Fact]
    public void AMoonRunningIntoItsPlanet_IsWarnedAbout()
    {
        (World world, _, Body earth) = SunAndEarth();
        Moon(world, earth, "Lump", 6600);  // Closer than the two radii, 6,871 km

        Assert.Contains("runs into", Assert.Single(OrbitStability.Warnings(world.Bodies)).Message);
    }

    [Fact]
    public void AFlatWorldsRim_ReachesFartherThanItsRadius()
    {
        (World world, _, Body earth) = SunAndEarth();
        earth.Shape = BodyShape.FlatDisc;
        Moon(world, earth, "Skimmer", 19_000);  // Outside the radius, inside the rim (20,015 km)

        Assert.Contains("runs into", Assert.Single(OrbitStability.Warnings(world.Bodies)).Message);
    }

    [Theory]
    [InlineData(0, true)]  // Going forwards: steady out to half the Hill sphere only
    [InlineData(170, false)]  // Going backwards: steady out to about 0.7 of it
    public void AFarMoon_IsWarnedAbout_UnlessItGoesBackwards(double tilt, bool warned)
    {
        (World world, _, Body earth) = SunAndEarth();
        Body moon = Moon(world, earth, "Wanderer", 900_000);
        moon.Orbit = moon.Orbit! with { TiltDegrees = tilt };

        IReadOnlyList<OrbitWarning> warnings = OrbitStability.Warnings(world.Bodies);

        Assert.Equal(warned, warnings.Count == 1);
        Assert.All(warnings, w => Assert.Contains("Sun's pull would take it away", w.Message));
    }

    [Fact]
    public void NeighborsTooClose_AreBothWarnedAbout()
    {
        (World world, Body sun, Body earth) = SunAndEarth();
        Body twin = Planet(world, sun, "Twin", EarthDistanceKm + 1_000_000);

        IReadOnlyList<OrbitWarning> warnings = OrbitStability.Warnings(world.Bodies);

        Assert.Equal(new[] { earth.Id, twin.Id }.Order(), warnings.Select(w => w.BodyId).Order());
        Assert.Contains("come within 1,000,000 km", warnings[0].Message);
    }

    [Fact]
    public void OverlappingOrbits_AreWarnedAbout()
    {
        (World world, Body sun, _) = SunAndEarth();
        Planet(world, sun, "Crosser", 200_000_000, eccentricity: 0.5);

        Assert.All(OrbitStability.Warnings(world.Bodies),
            w => Assert.Contains("overlap", w.Message));
        Assert.Equal(2, OrbitStability.Warnings(world.Bodies).Count);
    }

    [Fact]
    public void Realms_AreNeverWarnedAboutEachOther()
    {
        (World world, Body sun, _) = SunAndEarth();
        Body tree = NewBodies.WorldTree(world.Bodies, sun);
        world.Bodies.Add(tree);
        for (int branch = 0; branch < 3; branch++)
        {
            world.Bodies.Add(new Body
            {
                Name = $"Realm {branch}",
                RadiusKm = 100,
                Branch = branch,
                Orbit = Realms.OrbitOnBranch(tree, branch),
            });
        }

        Assert.DoesNotContain(OrbitStability.Warnings(world.Bodies),
            w => w.Message.Contains("Realm 0") && w.Message.Contains("Realm 1"));
    }

    [Fact]
    public void Reaches_SpreadAroundEachOrbit_ByItsPull()
    {
        (World world, Body sun, Body earth) = SunAndEarth();
        Body jupiter = Planet(world, sun, "Jupiter", 778_500_000, radiusKm: 69_911);

        IReadOnlyList<NeighborReach> reaches = OrbitStability.Reaches(world.Bodies, sun);

        Assert.Equal([earth.Id, jupiter.Id], reaches.Select(r => r.BodyId));
        double earthReach = 2 * Math.Sqrt(3) * OrbitStability.HillRadiusKm(world.Bodies, earth);
        Assert.Equal(EarthDistanceKm - earthReach, reaches[0].InnerKm, 0);
        Assert.Equal(EarthDistanceKm + earthReach, reaches[0].OuterKm, 0);
        Assert.True(reaches[1].OuterKm - reaches[1].InnerKm > 30 * (2 * earthReach));
    }

    [Fact]
    public void Reaches_LeaveOutAStarCirclingThePlanet()
    {
        (World world, _, Body earth) = SunAndEarth();
        Moon(world, earth, "Moon", 384_400, radiusKm: 1737);
        MakeCenter(world, earth);

        NeighborReach reach = Assert.Single(OrbitStability.Reaches(world.Bodies, earth));

        Assert.Equal("Moon", world.Bodies.Single(b => b.Id == reach.BodyId).Name);
    }

    private static void MakeCenter(World world, Body center)
    {
        foreach ((Guid id, Orbit? orbit) in SystemHierarchy.MakeCenter(world.Bodies, center.Id))
        {
            world.Bodies.Single(b => b.Id == id).Orbit = orbit;
        }
    }

    // The Sun and Earth with their real sizes and distance (the default world's).
    private static (World World, Body Sun, Body Earth) SunAndEarth()
    {
        World world = World.CreateNew();
        Body sun = world.Bodies.Single(b => b.Kind == BodyKind.Star);
        Body earth = world.Bodies.Single(b => b.Kind == BodyKind.Planet);
        Assert.Equal(EarthDistanceKm, earth.Orbit!.DistanceKm);
        return (world, sun, earth);
    }

    private static Body Planet(World world, Body sun, string name, double distanceKm,
        double radiusKm = 6371, double eccentricity = 0)
    {
        var planet = new Body
        {
            Name = name,
            RadiusKm = radiusKm,
            Orbit = new Orbit
            {
                ParentId = sun.Id,
                DistanceKm = distanceKm,
                PeriodDays = 365,
                Eccentricity = eccentricity,
            },
        };
        world.Bodies.Add(planet);
        return planet;
    }

    private static Body Moon(World world, Body planet, string name, double distanceKm,
        double radiusKm = 500)
    {
        var moon = new Body
        {
            Name = name,
            Kind = BodyKind.Moon,
            RadiusKm = radiusKm,
            Orbit = new Orbit { ParentId = planet.Id, DistanceKm = distanceKm, PeriodDays = 27 },
        };
        world.Bodies.Add(moon);
        return moon;
    }
}
