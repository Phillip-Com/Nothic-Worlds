using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class OrbitGuideTests
{
    [Fact]
    public void APlanet_GetsItsMoonZone_ThenWhereItCouldCircleItsStar()
    {
        (World world, Body sun, Body earth) = SunAndEarth();

        IReadOnlyList<GuideBand> bands = OrbitGuide.Bands(world.Bodies, earth);

        OrbitZone moons = OrbitStability.MoonZone(world.Bodies, earth)!;
        OrbitZone planets = OrbitStability.MoonZone(world.Bodies, sun)!;
        Assert.Equal(
        [
            new GuideBand(earth.Id, earth.RadiusKm, moons.InnerKm, Steady: false),
            new GuideBand(earth.Id, moons.InnerKm, moons.OuterKm, Steady: true),
            new GuideBand(sun.Id, sun.RadiusKm, planets.InnerKm, Steady: false),
            // Nothing limits the Sun's zone: drawn to half again past the Earth's orbit.
            new GuideBand(sun.Id, planets.InnerKm, 1.5 * earth.Orbit!.DistanceKm, Steady: true),
        ], bands);
    }

    [Fact]
    public void Neighbors_CutUnsteadyGaps_IntoTheZone()
    {
        (World world, Body sun, Body earth) = SunAndEarth();
        Body jupiter = Planet(world, sun, "Jupiter", 778_500_000, 69_911);
        Moon(world, earth, 384_400);

        IReadOnlyList<GuideBand> bands = OrbitGuide.Bands(world.Bodies, earth);

        // Around the Earth: the Moon's reach splits its zone.
        Assert.Equal([false, true, false, true],
            bands.Where(b => b.CenterId == earth.Id).Select(b => b.Steady));
        // Around the Sun: Jupiter's reach (not the Earth's own), then past it.
        NeighborReach reach = OrbitStability.Reaches(world.Bodies, sun)
            .Single(r => r.BodyId == jupiter.Id);
        GuideBand[] aroundSun = [.. bands.Where(b => b.CenterId == sun.Id)];
        Assert.Equal([false, true, false, true], aroundSun.Select(b => b.Steady));
        Assert.Equal(reach.InnerKm, aroundSun[2].InnerKm);
        Assert.Equal(reach.OuterKm, aroundSun[2].OuterKm);
    }

    [Fact]
    public void Bands_RunEdgeToEdge_WithoutOverlapping()
    {
        (World world, Body sun, Body earth) = SunAndEarth();
        Planet(world, sun, "Venus", 108_200_000, 6052);
        Planet(world, sun, "Twin", 150_600_000, 6371);  // Reaches overlap the Earth's
        Planet(world, sun, "Jupiter", 778_500_000, 69_911);

        foreach (Body selected in world.Bodies)
        {
            foreach (IGrouping<Guid, GuideBand> around in OrbitGuide.Bands(world.Bodies, selected)
                .GroupBy(b => b.CenterId))
            {
                GuideBand[] bands = [.. around];
                Assert.All(bands, b => Assert.True(b.OuterKm > b.InnerKm));
                for (int i = 1; i < bands.Length; i++)
                {
                    Assert.Equal(bands[i - 1].OuterKm, bands[i].InnerKm);
                    Assert.NotEqual(bands[i - 1].Steady, bands[i].Steady);
                }
            }
        }
    }

    [Fact]
    public void ABodyWithNoRoomForMoons_IsUnsteadyAllRound()
    {
        (World world, Body sun, _) = SunAndEarth();
        Body scorched = Planet(world, sun, "Scorched", 1_000_000, 6371);

        GuideBand around = OrbitGuide.Bands(world.Bodies, scorched)
            .Single(b => b.CenterId == scorched.Id);

        Assert.False(around.Steady);
        Assert.Equal(scorched.RadiusKm, around.InnerKm);
    }

    [Fact]
    public void ARealm_GetsNoBandsAroundItsTree()
    {
        (World world, Body sun, _) = SunAndEarth();
        Body tree = NewBodies.WorldTree(world.Bodies, sun);
        world.Bodies.Add(tree);
        var realm = new Body { Name = "Asgard", Branch = 0, Orbit = Realms.OrbitOnBranch(tree, 0) };
        world.Bodies.Add(realm);

        Assert.All(OrbitGuide.Bands(world.Bodies, realm), b => Assert.Equal(realm.Id, b.CenterId));
    }

    [Theory]
    [InlineData(SystemScale.Readable)]
    [InlineData(SystemScale.True)]
    public void DisplayDistance_IsHowFarDisplayOffsetDraws(SystemScale scale)
    {
        var offset = new Vector3D(3e7, 1e6, -4e7);

        double expected = SystemLayout.DisplayOffset(offset, 6.5, 1, scale).Length;

        Assert.Equal(expected, SystemLayout.DisplayDistance(offset.Length, 6.5, 1, scale), 9);
    }

    private static (World World, Body Sun, Body Earth) SunAndEarth()
    {
        World world = World.CreateNew();
        return (world, world.Bodies.Single(b => b.Kind == BodyKind.Star),
            world.Bodies.Single(b => b.Kind == BodyKind.Planet));
    }

    private static Body Planet(
        World world, Body sun, string name, double distanceKm, double radiusKm)
    {
        var planet = new Body
        {
            Name = name,
            RadiusKm = radiusKm,
            Orbit = new Orbit { ParentId = sun.Id, DistanceKm = distanceKm, PeriodDays = 365 },
        };
        world.Bodies.Add(planet);
        return planet;
    }

    private static void Moon(World world, Body planet, double distanceKm)
    {
        world.Bodies.Add(new Body
        {
            Name = "Moon",
            Kind = BodyKind.Moon,
            RadiusKm = 1737,
            Orbit = new Orbit { ParentId = planet.Id, DistanceKm = distanceKm, PeriodDays = 27 },
        });
    }
}
