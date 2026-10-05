using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public class NewBodiesTests
{
    [Fact]
    public void Planet_AroundTheSun_IsEarthLike_AndBeyondTheExistingPlanet()
    {
        World world = World.CreateNew();
        Body sun = world.Bodies[1];

        Body planet = NewBodies.Planet(world.Bodies, sun);

        Assert.Equal("Planet 1", planet.Name);  // "Planet" is taken by a different name scheme
        Assert.Equal(BodyKind.Planet, planet.Kind);
        Assert.Equal(sun.Id, planet.Orbit!.ParentId);
        Assert.Equal(149_600_000 * 1.6, planet.Orbit.DistanceKm, 1e-3);
        Assert.Equal(365.25 * Math.Pow(1.6, 1.5), planet.Orbit.PeriodDays, 1e-6);
        Assert.NotEqual(0, planet.Orbit.StartAngleDegrees);  // Not lined up with the first
        Assert.Null(planet.Problem());
    }

    [Fact]
    public void Names_TakeTheFirstFreeNumber()
    {
        World world = World.CreateNew();
        Body sun = world.Bodies[1];
        world.Bodies.Add(NewBodies.Planet(world.Bodies, sun));

        Assert.Equal("Planet 2", NewBodies.Planet(world.Bodies, sun).Name);
    }

    [Fact]
    public void Moon_IsMoonLike_AndAlwaysFacesItsPlanet()
    {
        World world = World.CreateNew();
        Body planet = world.Bodies[0];

        Body moon = NewBodies.Moon(world.Bodies, planet);

        Assert.Equal(BodyKind.Moon, moon.Kind);
        Assert.Equal(384_400, moon.Orbit!.DistanceKm);
        Assert.Equal(27.3, moon.Orbit.PeriodDays, 1e-9);
        Assert.Equal(moon.Orbit.PeriodDays * 24, moon.DayLengthHours, 1e-9);
        Assert.Null(moon.Problem());
    }

    [Fact]
    public void NewPlanetsHaveAir_AndNewMoonsDont()
    {
        World world = World.CreateNew();
        Body planet = world.Bodies[0];
        Body sun = world.Bodies.Single(b => b.Kind == BodyKind.Star);

        Assert.True(planet.HasAtmosphere);
        Assert.True(NewBodies.Planet(world.Bodies, sun).HasAtmosphere);
        Assert.False(NewBodies.Moon(world.Bodies, planet).HasAtmosphere);
    }

    [Fact]
    public void Atmosphere_IsCopied_AndCompared()
    {
        Body planet = World.CreateNew().Bodies[0];

        Body copy = planet.Clone();

        Assert.True(copy.HasSameContent(planet));
        copy.HasAtmosphere = false;
        Assert.False(copy.HasSameContent(planet));
        Assert.False(copy.Clone().HasAtmosphere);
    }

    [Fact]
    public void SecondMoon_OrbitsFartherOut_AndTakesLonger()
    {
        World world = World.CreateNew();
        Body planet = world.Bodies[0];
        Body first = NewBodies.Moon(world.Bodies, planet);
        world.Bodies.Add(first);

        Body second = NewBodies.Moon(world.Bodies, planet);

        Assert.True(second.Orbit!.DistanceKm > first.Orbit!.DistanceKm);
        Assert.True(second.Orbit.PeriodDays > first.Orbit.PeriodDays);
    }

    [Fact]
    public void CompanionStar_OrbitsFarOut()
    {
        World world = World.CreateNew();
        Body sun = world.Bodies[1];

        Body star = NewBodies.Star(world.Bodies, sun);

        Assert.Equal(BodyKind.Star, star.Kind);
        Assert.True(star.Orbit!.DistanceKm > 149_600_000 * 10);
        Assert.Null(star.Problem());
        world.Bodies.Add(star);
        Assert.Null(SystemHierarchy.Problem(world.Bodies));
    }

    [Fact]
    public void Planet_AroundALonePlanet_GetsAMoonLikePeriod()
    {
        // Older worlds have one planet and no sun; a body added around it shouldn't get a
        // star-sized period.
        var lonePlanet = new Body { Name = "Old World" };

        Body added = NewBodies.Planet([lonePlanet], lonePlanet);

        Assert.InRange(added.Orbit!.PeriodDays, 1, 10_000);
    }

    [Fact]
    public void DescendantsOf_FindsMoonsOfMoons()
    {
        World world = World.CreateNew();
        Body planet = world.Bodies[0];
        Body moon = NewBodies.Moon(world.Bodies, planet);
        world.Bodies.Add(moon);
        Body moonlet = NewBodies.Moon(world.Bodies, moon);
        world.Bodies.Add(moonlet);

        Assert.Equal([moon.Id, moonlet.Id],
            SystemHierarchy.DescendantsOf(world.Bodies, planet.Id).Select(b => b.Id));
        Assert.Equal(3, SystemHierarchy.DescendantsOf(world.Bodies, world.Bodies[1].Id).Count);
    }

    [Fact]
    public void Body_HasSameContent_NoticesEveryProperty()
    {
        World world = World.CreateNew();
        Body planet = world.Bodies[0];

        Assert.True(planet.HasSameContent(planet.Clone()));

        foreach (Action<Body> change in new Action<Body>[]
        {
            b => b.Name = "Other",
            b => b.Kind = BodyKind.Moon,
            b => b.RadiusKm = 1,
            b => b.DayLengthHours = 10,
            b => b.AxialTiltDegrees = 1,
            b => b.Orbit = b.Orbit! with { PeriodDays = 100 },
            b => b.Surface.FillColor = new RgbColor(1, 2, 3),
        })
        {
            Body copy = planet.Clone();
            change(copy);
            Assert.False(planet.HasSameContent(copy));
        }
    }
}
