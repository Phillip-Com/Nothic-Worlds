using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public sealed class WaterRulesTests
{
    [Fact]
    public void RiversAndLakesOnAPlanet_HaveNoProblem()
    {
        World world = WaterWorld();

        Assert.Null(WaterRules.Problem(world));
    }

    [Fact]
    public void ARiverOnAStar_IsAProblem()
    {
        World world = WaterWorld();
        Guid star = world.Bodies.Single(b => b.Kind == BodyKind.Star).Id;
        world.Rivers[0] = world.Rivers[0] with { BodyId = star };

        Assert.Contains("star", WaterRules.Problem(world));
    }

    [Fact]
    public void ALakeOnAMissingBody_IsAProblem()
    {
        World world = WaterWorld();
        world.Lakes[0] = world.Lakes[0] with { BodyId = Guid.NewGuid() };

        Assert.Contains("doesn't exist", WaterRules.Problem(world));
    }

    [Fact]
    public void TwoRiversSharingAnId_AreAProblem()
    {
        World world = WaterWorld();
        world.Rivers.Add(world.Rivers[0] with { Name = "Twin" });

        Assert.Contains("share an ID", WaterRules.Problem(world));
    }

    [Theory]
    [InlineData(RiverKind.Drawn, 1, "2 to")]
    [InlineData(RiverKind.Natural, 2, "one source")]
    public void ARiverWithTheWrongNumberOfPoints_IsAProblem(RiverKind kind, int points,
        string cause)
    {
        River river = NewRiver(Guid.NewGuid(), kind, points);

        Assert.Contains(cause, river.Problem());
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(500)]
    [InlineData(double.NaN)]
    public void ARiverTooNarrowOrTooWide_IsAProblem(double widthKm)
    {
        River river = NewRiver(Guid.NewGuid(), RiverKind.Natural, 1) with { WidthKm = widthKm };

        Assert.Contains("wide", river.Problem());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UnnamedRiversAndLakes_AreAProblem(string name)
    {
        River river = NewRiver(Guid.NewGuid(), RiverKind.Natural, 1) with { Name = name };
        Lake lake = NewLake(Guid.NewGuid()) with { Name = name };

        Assert.Contains("name", river.Problem());
        Assert.Contains("name", lake.Problem());
    }

    [Fact]
    public void ALakeSurfaceOutOfRange_IsAProblem()
    {
        Lake lake = NewLake(Guid.NewGuid()) with { LevelMeters = 20_000 };

        Assert.Contains("surface", lake.Problem());
    }

    [Fact]
    public void RiversWithTheSameCourse_AreEqual()
    {
        var id = Guid.NewGuid();
        River river = NewRiver(id, RiverKind.Drawn, 3);
        River copy = river with { Points = [.. river.Points] };

        Assert.Equal(river, copy);
        Assert.NotEqual(river, copy with { Points = [.. river.Points.Reverse()] });
    }

    [Fact]
    public void Clone_KeepsRiversAndLakes()
    {
        World world = WaterWorld();

        World copy = world.Clone();

        Assert.Equal(world.Rivers, copy.Rivers);
        Assert.Equal(world.Lakes, copy.Lakes);
    }

    private static World WaterWorld()
    {
        World world = World.CreateNew();
        Guid planet = world.Bodies.Single(b => b.Kind == BodyKind.Planet).Id;
        world.Rivers.Add(NewRiver(planet, RiverKind.Drawn, 2));
        world.Rivers.Add(NewRiver(planet, RiverKind.Natural, 1));
        world.Lakes.Add(NewLake(planet));
        return world;
    }

    private static River NewRiver(Guid body, RiverKind kind, int points) => new()
    {
        BodyId = body,
        Name = "Silverrun",
        Kind = kind,
        Points = [.. Enumerable.Range(0, points).Select(i => new GeoCoordinate(i, 2 * i))],
    };

    private static Lake NewLake(Guid body) => new()
    {
        BodyId = body,
        Name = "Still Mere",
        Spot = new GeoCoordinate(10, 20),
        LevelMeters = 300,
    };
}
