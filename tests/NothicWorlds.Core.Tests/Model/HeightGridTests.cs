using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public sealed class HeightGridTests
{
    private static readonly Vector3D _spot = At(20, 30);

    [Fact]
    public void Raising_LiftsTheMiddleFully_EasingToNothingAtTheEdge()
    {
        // On the equator, degrees of longitude are degrees of arc. Cells are about 0.09°
        // across, so a cell's middle can be a little off the point asked about.
        HeightGrid grid = HeightGrid.Empty.Raise(At(0, 30), At(0, 30), 2, 1000);

        Assert.InRange(grid.HeightAt(At(0, 30)), 990, 1000);
        Assert.InRange(grid.HeightAt(At(0, 31)), 450, 550);  // Halfway out: about half
        Assert.InRange(grid.HeightAt(At(0, 31.9)), 0, 30);
        Assert.Equal(0, grid.HeightAt(At(0, 32.2)));
        Assert.True(HeightGrid.Empty.IsEmpty);  // Immutable
    }

    [Fact]
    public void AStroke_RaisesAnEvenRidge_AndStrokesAddUp()
    {
        Vector3D from = At(0, 10);
        Vector3D to = At(0, 30);

        HeightGrid once = HeightGrid.Empty.Raise(from, to, 1, 500);
        HeightGrid twice = once.Raise(from, to, 1, 500);

        foreach (double longitude in new[] { 12.0, 17.3, 20.0, 26.1 })
        {
            // Even along the ridge, give or take a cell's middle being off the stroke's line.
            Assert.InRange(once.HeightAt(At(0, longitude)), 490, 500);
            Assert.InRange(twice.HeightAt(At(0, longitude)), 980, 1000);
        }
    }

    [Fact]
    public void AStrokeAlongSeveralPoints_HasNoBumpsAtItsJoins()
    {
        Vector3D[] path = [At(0, 10), At(0, 14), At(5, 18), At(5, 25)];

        HeightGrid whole = HeightGrid.Empty.Raise(path, 1, 500);

        // At most the stroke's height anywhere, and the full height all along it.
        Assert.Equal(500, whole.Highest);
        foreach (Vector3D point in path)
        {
            Assert.InRange(whole.HeightAt(point), 490, 500);
        }

        Assert.InRange(whole.HeightAt(At(2.5, 16)), 490, 500);  // Between two points
    }

    [Fact]
    public void Lowering_DigsDown_AndHeightsStayInRange()
    {
        HeightGrid dug = HeightGrid.Empty.Raise(_spot, _spot, 1, -800);
        HeightGrid tall = HeightGrid.Empty.Raise(_spot, _spot, 1, 50_000);
        HeightGrid deep = HeightGrid.Empty.Raise(_spot, _spot, 1, -50_000);

        Assert.InRange(dug.HeightAt(_spot), -800, -790);
        Assert.Equal(HeightGrid.MaxHeightMeters, tall.HeightAt(_spot));
        Assert.Equal(HeightGrid.MinHeightMeters, deep.HeightAt(_spot));
    }

    [Fact]
    public void Flattening_LevelsTowardTheTarget()
    {
        HeightGrid hill = HeightGrid.Empty.Raise(_spot, _spot, 3, 2000);

        HeightGrid fully = hill.Flatten(_spot, _spot, 3, 500, amount: 1);
        HeightGrid half = hill.Flatten(_spot, _spot, 3, 500, amount: 0.5);

        Assert.InRange(fully.HeightAt(_spot), 500, 520);
        Assert.InRange(half.HeightAt(_spot), 1240, 1260);
    }

    [Fact]
    public void Smoothing_WearsDownASpike_AndLeavesFlatGroundAlone()
    {
        HeightGrid spike = HeightGrid.Empty.Raise(_spot, _spot, 0.1, 3000);

        HeightGrid smoothed = spike.Smooth(_spot, _spot, 1, amount: 1);

        Assert.True(smoothed.HeightAt(_spot) < spike.HeightAt(_spot) / 2);
        Assert.True(smoothed.HeightAt(At(20, 30.15)) > spike.HeightAt(At(20, 30.15)));
        Assert.Same(HeightGrid.Empty, HeightGrid.Empty.Smooth(_spot, _spot, 5, 1));
    }

    [Fact]
    public void Smoothing_ReachesAcrossFaceEdges()
    {
        // A face's corner: the +X face meets +Y and +Z there.
        Vector3D corner = new Vector3D(1, 1, 1) * (1 / Math.Sqrt(3));
        HeightGrid spike = HeightGrid.Empty.Raise(corner, corner, 0.1, 3000);

        HeightGrid smoothed = spike.Smooth(corner, corner, 1, amount: 1);

        Assert.True(smoothed.HeightAt(corner) < spike.HeightAt(corner));
        Assert.Equal(3, smoothed.FacesChangedFrom(HeightGrid.Empty).Count);
    }

    [Fact]
    public void AStroke_SharesEverythingItDidntTouch()
    {
        HeightGrid before = HeightGrid.Empty.Raise(At(0, 0), At(0, 0), 1, 100);

        HeightGrid after = before.Raise(_spot, _spot, 1, 100);

        Assert.Single(after.FacesChangedFrom(before));
        Assert.Equal(before.HeightAt(At(0, 0)), after.HeightAt(At(0, 0)));
        Assert.False(after.HasSameCells(before));
        Assert.Same(after, after.Raise(_spot, _spot, 1, 0));  // Nothing changed
    }

    [Fact]
    public void TheSameStrokes_GiveTheSameHeights()
    {
        HeightGrid Sculpt() => HeightGrid.Empty
            .Raise(At(10, 10), At(15, 25), 2, 1234)
            .Smooth(At(12, 15), At(12, 20), 1.5, 0.7)
            .Flatten(At(10, 10), At(10, 30), 1, 300, 0.8);

        Assert.True(Sculpt().HasSameCells(Sculpt()));
    }

    [Fact]
    public void Cells_CopyOutAndBackIn()
    {
        HeightGrid grid = HeightGrid.Empty.Raise(At(-40, 100), At(50, -60), 3, -2500);
        var cells = new short[HeightGrid.CellCount];
        for (int face = 0; face < CubeSphere.FaceCount; face++)
        {
            grid.CopyFace(face, cells.AsSpan(face * HeightGrid.CellsPerFace));
        }

        Assert.True(HeightGrid.FromCells(cells).HasSameCells(grid));
        cells[7] = short.MinValue;
        Assert.Throws<ArgumentException>(() => HeightGrid.FromCells(cells));
    }

    [Fact]
    public void Sampling_BlendsSmoothlyBetweenCells()
    {
        HeightGrid hill = HeightGrid.Empty.Raise(At(0, 30), At(0, 30), 2, 1000);

        // At a cell's middle a sample is the cell; between cells it's between theirs.
        Vector3D middle = CubeSphere.CellCenter(CubeSphere.CellAt(At(0, 30.5), 1024), 1024);
        Assert.Equal(hill.HeightAt(middle), hill.SampleAt(middle), 6);
        double previous = hill.SampleAt(At(0, 30.2));  // Past the peak (a cell from 30°)
        for (double longitude = 30.21; longitude < 32.5; longitude += 0.01)
        {
            double sample = hill.SampleAt(At(0, longitude));
            Assert.True(sample <= previous + 1e-9, $"At {longitude}");  // Downhill, no steps
            Assert.True(previous - sample < 10, $"At {longitude}");
            previous = sample;
        }

        Assert.Equal(0, hill.SampleAt(At(0, 33)));
    }

    [Fact]
    public void TheHighestCell_IsKnown()
    {
        Assert.Equal(0, HeightGrid.Empty.Highest);
        HeightGrid grid = HeightGrid.Empty.Raise(_spot, _spot, 1, 1234)
            .Raise(At(-50, 0), At(-50, 0), 1, -5000);

        Assert.Equal(grid.HeightAt(_spot), grid.Highest);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(91.0)]
    public void ABrushOutOfRange_IsRefused(double radius)
    {
        Assert.Throws<ArgumentException>(() => HeightGrid.Empty.Raise(_spot, _spot, radius, 1));
    }

    private static Vector3D At(double latitude, double longitude)
    {
        System.Numerics.Vector3 direction =
            SphericalCoordinates.ToDirection(new GeoCoordinate(latitude, longitude));
        return new Vector3D(direction.X, direction.Y, direction.Z);
    }

    [Fact]
    public void ASparseGrid_HoldsItsCells_AndTheFillEverywhereElse()
    {
        var lake = new CubeCell(2, 100, 200);
        var shore = new CubeCell(2, 101, 200);

        HeightGrid grid = HeightGrid.Sparse(HeightGrid.MinHeightMeters,
            [(lake, (short)420), (shore, (short)420)]);

        Assert.Equal(420, grid.HeightAt(lake));
        Assert.Equal(420, grid.HeightAt(shore));
        Assert.Equal(HeightGrid.MinHeightMeters, grid.HeightAt(new CubeCell(2, 900, 900)));
        Assert.Equal(HeightGrid.MinHeightMeters, grid.HeightAt(new CubeCell(5, 0, 0)));
        Assert.False(grid.IsEmpty);
        Assert.True(HeightGrid.Sparse(0, []).IsEmpty);
    }
}
