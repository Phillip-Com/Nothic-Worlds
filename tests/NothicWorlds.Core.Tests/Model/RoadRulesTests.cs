using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public sealed class RoadRulesTests
{
    [Fact]
    public void RoadsOnAPlanet_HaveNoProblem()
    {
        World world = RoadWorld();

        Assert.Null(RoadRules.Problem(world));
    }

    [Fact]
    public void ARoadOnAStar_IsAProblem()
    {
        World world = RoadWorld();
        Guid star = world.Bodies.Single(b => b.Kind == BodyKind.Star).Id;
        world.Roads[0] = world.Roads[0] with { BodyId = star };

        Assert.Contains("star", RoadRules.Problem(world));
    }

    [Fact]
    public void TwoRoadsSharingAnId_AreAProblem()
    {
        World world = RoadWorld();
        world.Roads.Add(world.Roads[0] with { Name = "Twin" });

        Assert.Contains("share an ID", RoadRules.Problem(world));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(Road.MaxPoints + 1)]
    public void ARoadWithTooFewOrTooManyPoints_IsAProblem(int points)
    {
        Road road = NewRoad(Guid.NewGuid(), points);

        Assert.Contains("points", road.Problem());
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(500)]
    [InlineData(double.NaN)]
    public void ARoadTooNarrowOrTooWide_IsAProblem(double widthMeters)
    {
        Road road = NewRoad(Guid.NewGuid(), 2) with { WidthMeters = widthMeters };

        Assert.Contains("wide", road.Problem());
    }

    [Fact]
    public void AnUnnamedRoad_IsAProblem()
    {
        Road road = NewRoad(Guid.NewGuid(), 2) with { Name = " " };

        Assert.Contains("name", road.Problem());
    }

    [Fact]
    public void AnUnknownStyle_IsAProblem()
    {
        Road road = NewRoad(Guid.NewGuid(), 2) with { Style = (RoadStyle)99 };

        Assert.Contains("style", road.Problem());
    }

    [Fact]
    public void EachStyle_HasADefaultWidthARoadCanHave()
    {
        foreach (RoadStyle style in Enum.GetValues<RoadStyle>())
        {
            Road road = NewRoad(Guid.NewGuid(), 2) with
            {
                Style = style,
                WidthMeters = Road.DefaultWidthMeters(style),
            };

            Assert.Null(road.Problem());
        }
    }

    [Fact]
    public void RoadsWithTheSameCourse_AreEqual()
    {
        Road road = NewRoad(Guid.NewGuid(), 3);
        Road copy = road with { Points = [.. road.Points] };

        Assert.Equal(road, copy);
        Assert.NotEqual(road, copy with { Points = [.. road.Points.Reverse()] });
    }

    [Fact]
    public void Clone_KeepsRoads()
    {
        World world = RoadWorld();

        World copy = world.Clone();

        Assert.Equal(world.Roads, copy.Roads);
    }

    private static World RoadWorld()
    {
        World world = World.CreateNew();
        Guid planet = world.Bodies.Single(b => b.Kind == BodyKind.Planet).Id;
        world.Roads.Add(NewRoad(planet, 2));
        world.Roads.Add(NewRoad(planet, 3) with
        {
            Style = RoadStyle.DirtPaths,
            Kind = RoadKind.Drawn,
        });
        return world;
    }

    private static Road NewRoad(Guid body, int points) => new()
    {
        BodyId = body,
        Name = "Kingsway",
        Style = RoadStyle.StoneHighway,
        Kind = RoadKind.Routed,
        Points = [.. Enumerable.Range(0, points).Select(i => new GeoCoordinate(0, i * 0.01))],
    };
}
