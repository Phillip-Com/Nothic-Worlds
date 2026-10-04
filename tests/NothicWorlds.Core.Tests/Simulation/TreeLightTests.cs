using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class TreeLightTests
{
    [Fact]
    public void AGlowingTree_IsItsRealmsSun_AndOneTurnIsTheirYear()
    {
        (World world, Body tree, Body realm) = TreeWithRealm(glow: 1);

        Assert.Same(tree, Seasons.StarFor(world.Bodies, realm));
        Assert.Same(realm, BodyClock.YearOrbitOf(world.Bodies, realm));
        Assert.Equal(tree.DayLengthHours / 24, BodyClock.YearDays(world.Bodies, realm), 9);
    }

    [Fact]
    public void ADarkTree_LeavesItsRealmsToTheStar()
    {
        (World world, Body tree, Body realm) = TreeWithRealm(glow: 0);
        Body star = world.Bodies.Single(b => b.Kind == BodyKind.Star);

        Assert.Same(star, Seasons.StarFor(world.Bodies, realm));
        Assert.Same(tree, BodyClock.YearOrbitOf(world.Bodies, realm));  // The tree's orbit
    }

    [Fact]
    public void ATiltedRealm_HasSeasonsOnTheTreesTurn()
    {
        (World world, Body tree, Body realm) = TreeWithRealm(glow: 1);
        realm.AxialTiltDegrees = 23.4;
        double turn = tree.DayLengthHours / 24;

        List<SeasonEvent> events = Seasons.EventsBetween(world.Bodies, realm, 0, turn);

        Assert.Equal(4, events.Count);
        List<SeasonEvent> nextTurn = Seasons.EventsBetween(world.Bodies, realm, turn, 2 * turn);
        Assert.Equal(events[0].TimeDays + turn, nextTurn[0].TimeDays, 3);
    }

    [Fact]
    public void ARealmsMoon_SharesItsSun()
    {
        (World world, Body tree, Body realm) = TreeWithRealm(glow: 1);
        Body moon = NewBodies.Moon(world.Bodies, realm);
        world.Bodies.Add(moon);

        Assert.Same(tree, Seasons.StarFor(world.Bodies, moon));
        Assert.Same(realm, BodyClock.YearOrbitOf(world.Bodies, moon));
    }

    [Fact]
    public void ARealm_HasWeather()
    {
        (World world, _, Body realm) = TreeWithRealm(glow: 1);
        realm.AxialTiltDegrees = 23.4;

        ClimateYear year = ClimateYear.At(world.Bodies, realm, new GeoCoordinate(40, 0), 0)!;

        // Like a planet's: a little cooler than the world's average at 40°, with seasons.
        Assert.InRange(year.Days.Average(d => d.MeanC), 5, realm.AverageTemperatureC);
        Assert.True(year.Days.Max(d => d.DaylightHours) > year.Days.Min(d => d.DaylightHours));
    }

    [Fact]
    public void ARealmsYear_CantBeFittedToItsCalendar_ButItsDayCan()
    {
        (World world, _, Body realm) = TreeWithRealm(glow: 1);
        var calendar = new Calendar { Months = [new("Month", 30)] };

        Assert.Contains("one turn of", CalendarFitting.Problem(world.Bodies, realm,
            calendar with { Fit = CalendarFit.YearLength }));
        Assert.Null(CalendarFitting.Problem(world.Bodies, realm,
            calendar with { Fit = CalendarFit.DayLength }));
    }

    [Fact]
    public void ATreesTime_IsCountedInTurns()
    {
        (_, Body tree, _) = TreeWithRealm(glow: 1);

        Assert.Equal("Turn 1, day 1", BodyClock.Describe(tree, 0));
        Assert.Equal("Turn 1, day 92", BodyClock.Describe(tree, 91.5));
        Assert.Equal("Turn 3, day 1", BodyClock.Describe(tree, 2 * 365.25 + 0.1));
    }

    [Fact]
    public void MarksOnARealmsYear_AreWhereTheRealmIsDrawn()
    {
        (World world, Body tree, Body realm) = TreeWithRealm(glow: 1);

        Dictionary<Guid, DisplayBody> layout =
            SystemLayout.At(world.Bodies, 77, SystemScale.Readable);
        Vector3D mark = SystemLayout.OrbitPoint(realm, tree, 77, SystemScale.Readable);

        Assert.True((layout[tree.Id].Position + mark - layout[realm.Id].Position).Length < 1e-9);
    }

    private static (World World, Body Tree, Body Realm) TreeWithRealm(double glow)
    {
        World world = World.CreateNew();
        Body star = world.Bodies.Single(b => b.Kind == BodyKind.Star);
        Body tree = NewBodies.WorldTree(world.Bodies, star);
        tree.Tree = tree.Tree! with { GlowStrength = glow };
        world.Bodies.Add(tree);
        var realm = new Body { Name = "Asgard", Branch = 2, Orbit = Realms.OrbitOnBranch(tree, 2) };
        world.Bodies.Add(realm);
        return (world, tree, realm);
    }
}
