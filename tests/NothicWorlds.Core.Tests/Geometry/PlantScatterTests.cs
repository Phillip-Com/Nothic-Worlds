using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Tests.Geometry;

public sealed class PlantScatterTests
{
    private static readonly GroundTile _tile = new(2, 17, 70_000, 51_234);

    [Fact]
    public void Points_AreTheSameEveryTime()
    {
        Assert.Equal(PlantScatter.Points(5, 0, _tile, 40.3),
            PlantScatter.Points(5, 0, _tile, 40.3));
    }

    [Fact]
    public void Points_DifferBySeedLayerAndTile()
    {
        ScatterPoint first = PlantScatter.Points(5, 0, _tile, 10)[0];

        Assert.NotEqual(first, PlantScatter.Points(6, 0, _tile, 10)[0]);
        Assert.NotEqual(first, PlantScatter.Points(5, 1, _tile, 10)[0]);
        Assert.NotEqual(first, PlantScatter.Points(5, 0, _tile with { X = 70_001 }, 10)[0]);
    }

    [Fact]
    public void Points_NumberAboutTheExpected_OnAverage()
    {
        double total = 0;
        const int tiles = 2000;
        for (int x = 0; x < tiles; x++)
        {
            int count = PlantScatter.Points(9, 0, _tile with { X = x }, 3.25).Length;
            Assert.InRange(count, 3, 4);
            total += count;
        }

        Assert.Equal(3.25, total / tiles, 0.05);
    }

    [Fact]
    public void Points_LieOnTheTile_AndSpreadEvenly()
    {
        ScatterPoint[] points = PlantScatter.Points(1, 0, _tile, 20_000);

        Assert.All(points, point =>
        {
            foreach (double value in (double[])
                [point.Across, point.Down, point.Pick, point.Size, point.Turn, point.Shape])
            {
                Assert.InRange(value, 0, 1 - 1e-12);
            }
        });
        Assert.Equal(0.5, points.Average(point => point.Across), 0.01);
        Assert.Equal(0.25, points.Count(point => point.Down < 0.25) / (double)points.Length,
            0.01);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void Points_NoneExpected_GivesNone(double expected)
    {
        Assert.Empty(PlantScatter.Points(1, 0, _tile, expected));
    }
}
