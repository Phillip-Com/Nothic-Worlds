using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class GravitySimulationTests
{
    private const double AuKm = 149_600_000;
    private const double YearDays = 365.25;

    [Fact]
    public void AtTheStart_BodiesAreWhereTheirDesignPutsThem()
    {
        (World world, _, _, _) = SunEarthMoon();
        Dictionary<Guid, Vector3D> designed = SystemPositions.At(world.Bodies, 100);

        GravitySimulation physics = GravitySimulation.Start(world.Bodies, 100);

        Assert.True(physics.TryPositionsAt(100, int.MaxValue, out var positions));
        Assert.All(designed, pair => Assert.True((positions[pair.Key] - pair.Value).Length < 1e-3));
        Assert.True(physics.TryPositionsAt(50, int.MaxValue, out var before));  // Before: the start
        Assert.Equal(positions, before);
    }

    [Fact]
    public void TheEarth_KeepsItsOrbit_ForTenYears()
    {
        (World world, Body sun, Body earth, _) = SunEarthMoon();
        GravitySimulation physics = GravitySimulation.Start(world.Bodies, 0);

        for (double time = 0; time <= 10 * YearDays; time += 50)
        {
            physics.TryPositionsAt(time, int.MaxValue, out var positions);
            double distance = (positions[earth.Id] - positions[sun.Id]).Length;
            Assert.InRange(distance / AuKm, 0.995, 1.005);
        }
    }

    [Fact]
    public void TheMoon_StaysWithTheEarth_ForAYear()
    {
        (World world, _, Body earth, Body moon) = SunEarthMoon();
        GravitySimulation physics = GravitySimulation.Start(world.Bodies, 0);

        for (double time = 0; time <= YearDays; time += 5)
        {
            physics.TryPositionsAt(time, int.MaxValue, out var positions);
            double distance = (positions[moon.Id] - positions[earth.Id]).Length;
            Assert.InRange(distance, 0.95 * 384_400, 1.05 * 384_400);
        }
    }

    [Fact]
    public void TheSameTime_GivesTheSameAnswer_HoweverItsReached()
    {
        (World world, _, _, _) = SunEarthMoon();
        GravitySimulation direct = GravitySimulation.Start(world.Bodies, 0);
        GravitySimulation stepped = GravitySimulation.Start(world.Bodies, 0);
        GravitySimulation back = GravitySimulation.Start(world.Bodies, 0);

        direct.TryPositionsAt(200, int.MaxValue, out var expected);
        for (double time = 0; time < 200; time += 0.37)
        {
            stepped.TryPositionsAt(time, int.MaxValue, out _);
        }

        stepped.TryPositionsAt(200, int.MaxValue, out var afterSmallSteps);
        back.TryPositionsAt(700, int.MaxValue, out _);
        back.TryPositionsAt(200, int.MaxValue, out var afterGoingBack);
        Assert.Equal(expected, afterSmallSteps);
        Assert.Equal(expected, afterGoingBack);
    }

    [Fact]
    public void ALongJump_CanBeSpreadOverSeveralCalls()
    {
        (World world, _, _, _) = SunEarthMoon();
        GravitySimulation physics = GravitySimulation.Start(world.Bodies, 0);

        Assert.False(physics.TryPositionsAt(YearDays, 100, out var partial));
        Assert.NotEmpty(partial);  // Where it got to, to draw meanwhile
        int calls = 1;
        while (!physics.TryPositionsAt(YearDays, 100, out _))
        {
            calls++;
        }

        Assert.InRange(calls, 2, 1000);
    }

    [Fact]
    public void BodiesThatMeet_Merge_TheSmallerIntoTheBigger_OnceInTheLog()
    {
        (World world, Body sun, Body earth, _) = SunEarthMoon();
        Body twin = HeadOnTwin(world, sun);
        GravitySimulation physics = GravitySimulation.Start(world.Bodies, 0);

        physics.TryPositionsAt(100, int.MaxValue, out var positions);
        physics.TryPositionsAt(10, int.MaxValue, out _);
        physics.TryPositionsAt(100, int.MaxValue, out _);

        Collision collision = Assert.Single(physics.Collisions);
        Assert.Equal(twin.Id, collision.AbsorbedId);
        Assert.Equal(earth.Id, collision.IntoId);
        Assert.InRange(collision.TimeDays, 80, 100);  // A quarter of a year each, head on
        Assert.False(positions.ContainsKey(twin.Id));
        Assert.True(positions.ContainsKey(earth.Id));
    }

    [Fact]
    public void KeptOrbits_AtTheStart_AreTheDesignWithGravitysPeriods()
    {
        (World world, _, Body earth, Body moon) = SunEarthMoon();
        GravitySimulation physics = GravitySimulation.Start(world.Bodies, 30);

        KeptOrbits kept = physics.KeepAsOrbits(30);

        Assert.Empty(kept.NotKept);
        foreach (Body body in new[] { earth, moon })
        {
            Orbit orbit = kept.Orbits[body.Id];
            Assert.Equal(body.Orbit!.DistanceKm, orbit.DistanceKm, body.Orbit.DistanceKm * 1e-6);
            Assert.True(orbit.Eccentricity < 1e-4);
            double natural = OrbitStability.NaturalPeriodDays(world.Bodies, body)!.Value;
            Assert.Equal(natural, orbit.PeriodDays, natural * 1e-4);
        }
    }

    [Fact]
    public void ABodyThatMerged_CantKeepAnOrbit()
    {
        (World world, Body sun, _, _) = SunEarthMoon();
        Body twin = HeadOnTwin(world, sun);
        GravitySimulation physics = GravitySimulation.Start(world.Bodies, 0);

        KeptOrbits kept = physics.KeepAsOrbits(200);

        Assert.Equal("merged into Earth", kept.NotKept[twin.Id]);
        Assert.False(kept.Orbits.ContainsKey(twin.Id));
    }

    [Fact]
    public void Realms_RideTheirBranches()
    {
        (World world, Body sun, _, _) = SunEarthMoon();
        Body tree = NewBodies.WorldTree(world.Bodies, sun);
        world.Bodies.Add(tree);
        var realm = new Body { Name = "Asgard", Branch = 1, Orbit = Realms.OrbitOnBranch(tree, 1) };
        world.Bodies.Add(realm);
        GravitySimulation physics = GravitySimulation.Start(world.Bodies, 0);

        physics.TryPositionsAt(123.4, int.MaxValue, out var positions);

        Vector3D offset = positions[realm.Id] - positions[tree.Id];
        Vector3D designed = OrbitMath.OffsetFromParent(realm.Orbit!, 123.4);
        Assert.True((offset - designed).Length < 1, $"{offset} vs {designed}");
        Assert.DoesNotContain(physics.KeepAsOrbits(123.4).Orbits.Keys, id => id == realm.Id);
    }

    [Fact]
    public void TheLayout_DrawsPhysicsPositions_AsItDrawsDesignedOnes()
    {
        (World world, _, _, Body moon) = SunEarthMoon();
        Dictionary<Guid, Vector3D> positions = SystemPositions.At(world.Bodies, 42);

        Assert.Equal(SystemLayout.At(world.Bodies, 42, SystemScale.Readable),
            SystemLayout.At(world.Bodies, positions, SystemScale.Readable));
    }

    [Fact]
    public void TheLayout_DrawsAMoonWhosePlanetMergedAway_FromTheStar()
    {
        (World world, Body sun, Body earth, Body moon) = SunEarthMoon();
        Dictionary<Guid, Vector3D> positions = SystemPositions.At(world.Bodies, 0);
        positions.Remove(earth.Id);

        Dictionary<Guid, DisplayBody> layout =
            SystemLayout.At(world.Bodies, positions, SystemScale.Readable);

        Assert.False(layout.ContainsKey(earth.Id));
        Vector3D expected = SystemLayout.DisplayOffset(positions[moon.Id] - positions[sun.Id],
            layout[sun.Id].Radius, layout[moon.Id].Radius, SystemScale.Readable);
        Assert.True((layout[moon.Id].Position - layout[sun.Id].Position - expected).Length < 1e-9);
    }

    // The default Sun and Earth, with the Moon at its real distance and period.
    private static (World World, Body Sun, Body Earth, Body Moon) SunEarthMoon()
    {
        World world = World.CreateNew();
        Body sun = world.Bodies.Single(b => b.Kind == BodyKind.Star);
        Body earth = world.Bodies.Single(b => b.Kind == BodyKind.Planet);
        earth.Name = "Earth";
        var moon = new Body
        {
            Name = "Moon",
            Kind = BodyKind.Moon,
            RadiusKm = 1737,
            Orbit = new Orbit { ParentId = earth.Id, DistanceKm = 384_400, PeriodDays = 27.32 },
        };
        world.Bodies.Add(moon);
        return (world, sun, earth, moon);
    }

    // A smaller planet on the Earth's orbit going the other way, half a year round from it:
    // they meet head on after a quarter of a year. The Moon is taken away: it swings the Earth
    // round their balance point, so the two would just miss.
    private static Body HeadOnTwin(World world, Body sun)
    {
        world.Bodies.RemoveAll(b => b.Name == "Moon");
        Body earth = world.Bodies.Single(b => b.Name == "Earth");
        var twin = new Body
        {
            Name = "Twin",
            RadiusKm = 3000,
            Orbit = earth.Orbit! with
            {
                ParentId = sun.Id,
                TiltDegrees = 180,
                StartAngleDegrees = earth.Orbit.StartAngleDegrees + 180,
            },
        };
        world.Bodies.Add(twin);
        return twin;
    }
}
