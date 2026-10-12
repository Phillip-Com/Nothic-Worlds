using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class BodyRoadsTests
{
    // A steep cone 6 km high and half a degree across, on the equator between the roads' ends.
    private static readonly Vector3D _peak = WaterGround.At(0, 0);
    private static readonly Lazy<HeightGrid> _hill = new(() => WaterGround.Make(at =>
        6000 * Math.Max(0, 1 - DegreesBetween(at, _peak) / 0.5)));

    private static readonly Lazy<HeightGrid> _level = new(() => WaterGround.Make(_ => 100));

    [Fact]
    public void DirtPaths_GoAroundASteepHill()
    {
        RoadCourseShown course = Single(_hill.Value, Routed(RoadStyle.DirtPaths));

        Assert.Null(course.Problem);
        Assert.All(course.Path, at => Assert.True(DegreesBetween(at, _peak) > 0.25));
    }

    [Fact]
    public void AStoneHighway_KeepsItsLineThroughTheHill()
    {
        RoadCourseShown course = Single(_hill.Value, Routed(RoadStyle.StoneHighway));

        Assert.Null(course.Problem);
        Assert.Contains(course.Path, at => DegreesBetween(at, _peak) < 0.15);
    }

    [Fact]
    public void DirtPaths_GoAroundThickPlants_ButAHighwayCutsThrough()
    {
        Body planet = Planet(waterLevel: null);
        planet.Surface.Terrain = TerrainGrid.Empty.Paint(_peak, 0.6, 9);
        TerrainType jungle = new(9, "Jungle", new RgbColor(0, 120, 0), Plants: PlantCover.Jungle);

        RoadCourseShown dirt = Assert.Single(BodyRoads.For(planet, _level.Value, null,
            [Routed(RoadStyle.DirtPaths) with { BodyId = planet.Id }], [jungle]));
        RoadCourseShown highway = Assert.Single(BodyRoads.For(planet, _level.Value, null,
            [Routed(RoadStyle.StoneHighway) with { BodyId = planet.Id }], [jungle]));

        Assert.All(dirt.Path, at => Assert.True(DegreesBetween(at, _peak) > 0.4));
        Assert.Contains(highway.Path, at => DegreesBetween(at, _peak) < 0.15);
    }

    [Fact]
    public void ARoad_GoesAroundTheSea()
    {
        // The sea: a round bay below the 0 m water level, between the ends.
        HeightGrid ground = WaterGround.Make(at => DegreesBetween(at, _peak) < 0.8 ? -50 : 100);
        Body planet = Planet(waterLevel: 0);

        RoadCourseShown course = Assert.Single(BodyRoads.For(planet, ground, null,
            [Routed(RoadStyle.StoneHighway) with { BodyId = planet.Id }], []));

        Assert.Null(course.Problem);
        Assert.All(course.Path, at => Assert.True(DegreesBetween(at, _peak) > 0.7));
    }

    [Fact]
    public void AnEndInTheSea_IsDrawnStraight_WithAProblem()
    {
        HeightGrid ground = WaterGround.Make(at => DegreesBetween(at, _peak) < 0.8 ? -50 : 100);
        Body planet = Planet(waterLevel: 0);
        Road road = Routed(RoadStyle.DirtPaths) with
        {
            BodyId = planet.Id,
            Points = [new(0, -3), new(0, 0)],
        };

        RoadCourseShown course = Assert.Single(BodyRoads.For(planet, ground, null, [road], []));

        Assert.Contains("point 1 to point 2", course.Problem);
        Assert.Equal(WaterGround.At(0, 0), course.Path[^1]);
    }

    [Fact]
    public void ALaterRoad_FollowsTheWayOfAnEarlierOne()
    {
        Body planet = Planet(waterLevel: null);
        Road first = Routed(RoadStyle.DirtPaths) with { BodyId = planet.Id };
        Road second = first with
        {
            Id = Guid.NewGuid(),
            Points = [new(0.3, -3), new(0.3, 3)],
        };

        IReadOnlyList<RoadCourseShown> alone = BodyRoads.For(planet, _level.Value, null,
            [second], []);
        IReadOnlyList<RoadCourseShown> together = BodyRoads.For(planet, _level.Value, null,
            [first, second], []);

        // Alone it runs straight along its own line; after the first, it joins the first's.
        Assert.All(alone[0].Path, at => Assert.True(Math.Abs(LatitudeOf(at) - 0.3) < 0.1));
        Assert.True(together[1].Path.Count(at => Math.Abs(LatitudeOf(at)) < 0.05)
            > together[1].Path.Count / 2);
    }

    [Fact]
    public void ADrawnRoad_KeepsItsPoints()
    {
        Body planet = Planet(waterLevel: null);
        Road road = Routed(RoadStyle.CartTrack) with
        {
            BodyId = planet.Id,
            Kind = RoadKind.Drawn,
            Points = [new(0, -3), new(1, 0), new(0, 3)],
        };

        RoadCourseShown course = Assert.Single(BodyRoads.For(planet, _hill.Value, null, [road],
            []));

        Assert.Null(course.Problem);
        Assert.Equal(WaterGround.At(0, -3), course.Path[0]);
        Assert.Contains(WaterGround.At(1, 0), course.Path);
        Assert.Equal(WaterGround.At(0, 3), course.Path[^1]);
    }

    [Fact]
    public void TheSameWorld_GivesTheSameWays_AndOtherBodiesRoadsAreLeftOut()
    {
        Body planet = Planet(waterLevel: null);
        Road road = Routed(RoadStyle.DirtPaths) with { BodyId = planet.Id };
        Road elsewhere = road with { Id = Guid.NewGuid(), BodyId = Guid.NewGuid() };

        IReadOnlyList<RoadCourseShown> once = BodyRoads.For(planet, _hill.Value, null,
            [road, elsewhere], []);
        IReadOnlyList<RoadCourseShown> again = BodyRoads.For(planet, _hill.Value, null,
            [road], []);

        Assert.Equal(road.Id, Assert.Single(once).RoadId);
        Assert.Equal(once[0].Path, again[0].Path);
    }

    private static RoadCourseShown Single(HeightGrid ground, Road road)
    {
        Body planet = Planet(waterLevel: null);
        return Assert.Single(BodyRoads.For(planet, ground, null,
            [road with { BodyId = planet.Id }], []));
    }

    private static Road Routed(RoadStyle style) => new()
    {
        BodyId = Guid.Empty,
        Name = "Kingsway",
        Style = style,
        Kind = RoadKind.Routed,
        Points = [new(0, -3), new(0, 3)],
        WidthMeters = Road.DefaultWidthMeters(style),
    };

    private static Body Planet(int? waterLevel) => new()
    {
        Name = "Aerth",
        Kind = BodyKind.Planet,
        WaterLevelMeters = waterLevel,
    };

    private static double DegreesBetween(Vector3D a, Vector3D b) =>
        double.RadiansToDegrees(Math.Acos(Math.Clamp(a.Dot(b), -1, 1)));

    private static double LatitudeOf(Vector3D at) =>
        SphericalPolygon.FromUnit(at).LatitudeDegrees;
}
