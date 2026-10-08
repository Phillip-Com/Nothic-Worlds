using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class BodyWaterTests
{
    // Ground rising 100 m a degree from a low point, with a basin 2° across floored at 600 m.
    private static readonly Vector3D _low = WaterGround.At(0, 0);
    private static readonly Vector3D _basin = WaterGround.At(8, 6);
    private static readonly Lazy<HeightGrid> _ground = new(() => WaterGround.Make(at =>
        at.Dot(_basin) > Math.Cos(double.DegreesToRadians(2))
            ? 600
            : 100 * double.RadiansToDegrees(Math.Acos(Math.Clamp(at.Dot(_low), -1, 1)))));

    [Fact]
    public void TheSea_IsTheGroundBelowTheWaterLevel()
    {
        Body planet = Planet(waterLevel: 200);

        BodyWater water = BodyWater.For(planet, _ground.Value, [], [], []);

        Assert.True(water.IsSea(WaterCells.IndexAt(_low)));
        Assert.False(water.IsSea(WaterCells.IndexAt(_basin)));
    }

    [Fact]
    public void WaterTerrain_IsSeaToo()
    {
        Body planet = Planet(waterLevel: null);
        planet.Surface.Terrain = TerrainGrid.Empty.Paint(_basin, 1, 7);
        var ocean = new TerrainType(7, "Deep", new RgbColor(0, 0, 200), ClimateKind.Water);

        BodyWater water = BodyWater.For(planet, _ground.Value, [], [], [ocean]);

        Assert.True(water.IsSea(WaterCells.IndexAt(_basin)));
        Assert.False(water.IsSea(WaterCells.IndexAt(_low)));
    }

    [Fact]
    public void ALake_FillsItsBasin_WithItsLevelDrawnAroundIt()
    {
        Body planet = Planet(waterLevel: 200);
        Lake lake = NewLake(planet.Id, flowsOut: false);

        BodyWater water = BodyWater.For(planet, _ground.Value, [lake], [], []);

        int middle = WaterCells.IndexAt(_basin);
        Assert.True(water.IsWater(middle));
        Assert.False(water.IsSea(middle));
        Assert.True(water.Lakes[lake.Id].Cells.SetEquals(WaterGround.Within(_basin, 2)));
        Assert.Equal(700, water.LakeLevels.HeightAt(_basin));
        Assert.Equal(HeightGrid.MinHeightMeters, water.LakeLevels.HeightAt(_low));
        Assert.Empty(water.Rivers);
    }

    [Fact]
    public void ALakeThatFlowsOut_SendsARiverToTheSea()
    {
        Body planet = Planet(waterLevel: 200);
        Lake lake = NewLake(planet.Id, flowsOut: true);

        BodyWater water = BodyWater.For(planet, _ground.Value, [lake], [], []);

        RiverCourseShown outflow = Assert.Single(water.Rivers);
        Assert.Equal(lake.Id, outflow.LakeId);
        Assert.True(outflow.ReachesWater);
        Assert.True(water.IsSea(WaterCells.IndexAt(outflow.Points[^1])));
        Assert.DoesNotContain(outflow.Points,
            point => water.Lakes[lake.Id].Cells.Contains(WaterCells.IndexAt(point)));
    }

    [Fact]
    public void ANaturalRiver_EndsInALake_AndADrawnOneKeepsItsCourse()
    {
        Body planet = Planet(waterLevel: 200);
        Lake lake = NewLake(planet.Id, flowsOut: false);
        GeoCoordinate uphill = new(14, 10.5);  // Straight uphill beyond the lake
        River natural = new()
        {
            BodyId = planet.Id,
            Name = "Brook",
            Kind = RiverKind.Natural,
            Points = [uphill],
        };
        River drawn = new()
        {
            BodyId = planet.Id,
            Name = "Canal",
            Kind = RiverKind.Drawn,
            Points = [new GeoCoordinate(1, 1), new GeoCoordinate(2, 3)],
        };
        River elsewhere = natural with { Id = Guid.NewGuid(), BodyId = Guid.NewGuid() };

        BodyWater water = BodyWater.For(planet, _ground.Value, [lake],
            [natural, drawn, elsewhere], []);

        Assert.Equal(2, water.Rivers.Count);
        RiverCourseShown brook = water.Rivers[0];
        Assert.True(brook.ReachesWater);
        Assert.Contains(WaterCells.IndexAt(brook.Points[^1]), water.Lakes[lake.Id].Cells);
        Assert.Equal(drawn.Points.Select(SphericalPolygon.ToUnit), water.Rivers[1].Points);
    }

    private static Body Planet(int? waterLevel) => new()
    {
        Name = "Aerth",
        Kind = BodyKind.Planet,
        WaterLevelMeters = waterLevel,
    };

    private static Lake NewLake(Guid body, bool flowsOut) => new()
    {
        BodyId = body,
        Name = "Still Mere",
        Spot = new GeoCoordinate(8, 6),
        LevelMeters = 700,  // Below the rim, 800 m at its lowest
        FlowsOut = flowsOut,
    };
}
