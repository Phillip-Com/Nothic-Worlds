using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class RiverCourseTests
{
    // A sea 3° across, with the ground rising 100 m for each degree away from it.
    private static readonly Vector3D _sea = WaterGround.At(0, 0);
    private static readonly Vector3D _source = WaterGround.At(6, 8);
    private static readonly Lazy<HashSet<int>> _seaCells = new(() =>
        WaterGround.Within(_sea, 3));

    private static readonly Lazy<HeightGrid> _slope = new(() => WaterGround.Make(Slope));

    private static double Slope(Vector3D at) =>
        100 * double.RadiansToDegrees(Math.Acos(Math.Clamp(at.Dot(_sea), -1, 1)));

    private static bool IsSea(int cell) => _seaCells.Value.Contains(cell);

    [Fact]
    public void ARiver_RunsDownhill_IntoTheSea()
    {
        HeightGrid ground = _slope.Value;

        RiverPath river = RiverCourse.Trace(ground, _source, IsSea);

        Assert.True(river.ReachesWater);
        Assert.True(IsSea(WaterCells.IndexAt(river.Points[^1])));
        Assert.Equal(WaterCells.IndexAt(_source), WaterCells.IndexAt(river.Points[0]));
        short[] heights = [.. river.Points.Select(ground.HeightAt)];
        for (int i = 1; i < heights.Length - 1; i++)
        {
            Assert.True(heights[i] < heights[i - 1], $"step {i} climbs");
        }
    }

    [Fact]
    public void ARiverInAHollow_FindsTheWayOut_AndStillReachesTheSea()
    {
        // A pit 1° across around the source, 300 m deep.
        HeightGrid ground = WaterGround.Make(at =>
            Slope(at) - (at.Dot(_source) > Math.Cos(double.DegreesToRadians(1)) ? 300 : 0));

        RiverPath river = RiverCourse.Trace(ground, _source, IsSea);

        Assert.True(river.ReachesWater);
        Assert.Contains(river.Points, point => WaterGround.Degrees(_source, WaterCells.IndexAt(
            point)) > 1);
    }

    [Fact]
    public void OverRoughGround_TheCourse_IsTheSameAsTheSimpleSearchFinds()
    {
        // Bumpy, nearly level ground (a gentle tilt toward the sea), so rivers wander from
        // hollow to hollow and later searches spread back over earlier ones; and now and then
        // no water to find, so a search runs to its limit.
        HeightGrid ground = WaterGround.Make(at => Slope(at) * 0.05
            + TerrainNoise.Offset(at, 6371, 300, 80, 20, seed: 7));
        var random = new Random(3);

        for (int river = 0; river < 12; river++)
        {
            Vector3D source = WaterGround.At(random.NextDouble() * 10 - 5,
                random.NextDouble() * 10 - 5);
            Func<int, bool> water = river % 4 == 3 ? _ => false : IsSea;
            int[] cells = [.. RiverCourse.Trace(ground, source, water).Points
                .Select(WaterCells.IndexAt)];
            Assert.Equal(SimpleRiverCourse.Trace(ground, source, water), cells);
        }
    }

    [Fact]
    public void ARiver_NeverCrossesBlockedCells()
    {
        // A wall 1° across straight between the source and the sea.
        HashSet<int> wall = WaterGround.Within(WaterGround.At(3, 4), 1);

        RiverPath river = RiverCourse.Trace(_slope.Value, _source, IsSea, wall.Contains);

        Assert.True(river.ReachesWater);
        Assert.DoesNotContain(river.Points, point => wall.Contains(WaterCells.IndexAt(point)));
    }

    [Fact]
    public void ARiverThatStartsInTheSea_IsJustItsSource()
    {
        RiverPath river = RiverCourse.Trace(_slope.Value, _sea, IsSea);

        Assert.True(river.ReachesWater);
        Assert.Single(river.Points);
    }

    [Fact]
    public void ARiverWithNoWaterToReach_EndsWhereItGotTo()
    {
        RiverPath river = RiverCourse.Trace(_slope.Value, _source, _ => false);

        Assert.False(river.ReachesWater);
        Assert.NotEmpty(river.Points);
    }

    [Fact]
    public void TheSameGround_AlwaysGivesTheSameCourse()
    {
        RiverPath first = RiverCourse.Trace(_slope.Value, _source, IsSea);
        RiverPath second = RiverCourse.Trace(_slope.Value, _source, IsSea);

        Assert.Equal(first.Points, second.Points);
    }
}
