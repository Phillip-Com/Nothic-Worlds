using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class CometTests
{
    [Fact]
    public void ANewComet_CrossesItsInnermostPlanetsOrbit()
    {
        World world = World.CreateNew();
        Body star = world.Bodies.Single(b => b.Kind == BodyKind.Star);
        Body planet = world.Bodies.Single(b => b.Kind == BodyKind.Planet);

        Body comet = NewBodies.Comet(world.Bodies, star);

        Orbit orbit = comet.Orbit!;
        double closest = orbit.DistanceKm * (1 - orbit.Eccentricity);
        double farthest = orbit.DistanceKm * (1 + orbit.Eccentricity);
        Assert.Equal(BodyKind.Comet, comet.Kind);
        Assert.Equal(star.Id, orbit.ParentId);
        Assert.InRange(planet.Orbit!.DistanceKm, closest * 1.1, farthest);
        Assert.Null(comet.Problem());
        Assert.False(comet.HasSurface);
        Assert.Equal(BodyAppearance.CometGrey, comet.Appearance.Color);
    }

    [Fact]
    public void Comets_CircleOnlyStars_AndNothingCirclesThem()
    {
        World world = World.CreateNew();
        Body star = world.Bodies.Single(b => b.Kind == BodyKind.Star);
        Body planet = world.Bodies.Single(b => b.Kind == BodyKind.Planet);
        Body comet = NewBodies.Comet(world.Bodies, star);
        world.Bodies.Add(comet);
        Assert.Null(SystemHierarchy.Problem(world.Bodies));

        comet.Orbit = comet.Orbit! with { ParentId = planet.Id };
        Assert.NotNull(SystemHierarchy.Problem(world.Bodies));

        comet.Orbit = comet.Orbit with { ParentId = star.Id };
        planet.Orbit = planet.Orbit! with { ParentId = comet.Id };
        Assert.Contains("can't circle the comet", SystemHierarchy.Problem(world.Bodies));
    }

    [Fact]
    public void AComet_CantBeTheCenter_OrHaveACalendar()
    {
        World world = World.CreateNew();
        Body star = world.Bodies.Single(b => b.Kind == BodyKind.Star);
        Body comet = NewBodies.Comet(world.Bodies, star);
        world.Bodies.Add(comet);

        Assert.Empty(SystemHierarchy.MakeCenter(world.Bodies, comet.Id));

        comet.Calendar = new Calendar { Months = [new("Month", 30)] };
        Assert.NotNull(comet.Problem());
    }

    [Fact]
    public void ACometHasNoEclipses()
    {
        World world = World.CreateNew();
        Body star = world.Bodies.Single(b => b.Kind == BodyKind.Star);
        Body comet = NewBodies.Comet(world.Bodies, star);
        world.Bodies.Add(comet);

        Assert.Empty(Eclipses.Between(world.Bodies, comet, 0, 3650));
    }

    [Fact]
    public void TheTail_GrowsNearTheStar_AndFadesFarOut()
    {
        double au = CometTail.KmPerAu;

        Assert.Equal(2e7, CometTail.LengthKm(au), 6);
        Assert.Equal(8e7, CometTail.LengthKm(0.5 * au), 6);    // Inverse square
        Assert.Equal(1.5e8, CometTail.LengthKm(0.1 * au), 6);  // Capped
        Assert.Equal(0, CometTail.LengthKm(6 * au));
        Assert.Equal(1, CometTail.Brightness(2 * au));
        Assert.Equal(0.5, CometTail.Brightness(4 * au), 12);
        Assert.Equal(0, CometTail.Brightness(5 * au));
    }
}
