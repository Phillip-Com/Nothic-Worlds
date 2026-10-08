using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Tests.Geometry;

public sealed class CubeSphereTests
{
    private const int Size = 1024;

    [Theory]
    [InlineData(1, 0, 0, 0)]
    [InlineData(-1, 0, 0, 1)]
    [InlineData(0, 1, 0, 2)]   // North pole
    [InlineData(0, -1, 0, 3)]  // South pole
    [InlineData(0, 0, 1, 4)]
    [InlineData(0, 0, -1, 5)]
    public void EachAxis_PointsAtTheMiddleOfItsFace(double x, double y, double z, int face)
    {
        CubeCell cell = CubeSphere.CellAt(new Vector3D(x, y, z), Size);

        Assert.Equal(new CubeCell(face, Size / 2, Size / 2), cell);
    }

    [Fact]
    public void EveryCellCenter_LiesInItsOwnCell()
    {
        // Every cell on a small grid, plus the edges and corners of the real one.
        for (int face = 0; face < CubeSphere.FaceCount; face++)
        {
            for (int column = 0; column < 8; column++)
            {
                for (int row = 0; row < 8; row++)
                {
                    var cell = new CubeCell(face, column, row);
                    Assert.Equal(cell, CubeSphere.CellAt(CubeSphere.CellCenter(cell, 8), 8));
                }
            }

            foreach (int column in new[] { 0, 1, Size / 2, Size - 2, Size - 1 })
            {
                foreach (int row in new[] { 0, 1, Size / 2, Size - 2, Size - 1 })
                {
                    var cell = new CubeCell(face, column, row);
                    Assert.Equal(cell, CubeSphere.CellAt(CubeSphere.CellCenter(cell, Size), Size));
                }
            }
        }
    }

    [Fact]
    public void CellCenters_AreUnitLength()
    {
        Vector3D center = CubeSphere.CellCenter(new CubeCell(3, 17, 900), Size);

        Assert.Equal(1, center.Length, 12);
    }

    [Fact]
    public void RowZero_IsAtTheTopOfTheFace_AndColumnZeroAtTheLeft()
    {
        // On the +Z face, "up" is north (+Y) and "right" is east (+X).
        Vector3D topLeft = CubeSphere.CellCenter(new CubeCell(4, 0, 0), Size);

        Assert.True(topLeft.Y > 0.5);
        Assert.True(topLeft.X < -0.5);
    }

    [Fact]
    public void Cells_StayCloseToTheSameSize()
    {
        // The gap to the next cell, in the middle of a face and in its corner.
        double middle = Gap(
            new CubeCell(4, Size / 2, Size / 2), new CubeCell(4, Size / 2 + 1, Size / 2));
        double corner = Gap(new CubeCell(4, 0, 0), new CubeCell(4, 1, 0));

        Assert.InRange(middle / corner, 0.66, 1.5);
        Assert.InRange(middle, 0.087 * 0.9, 0.087 * 1.1);  // About 90° / 1024
    }

    [Fact]
    public void NeighborsAcrossAFaceEdge_AreOneCellApart()
    {
        // The east edge of the +Z face (its last column) meets the +X face's first column.
        Vector3D edgeOfZ = CubeSphere.CellCenter(new CubeCell(4, Size - 1, Size / 2), Size);
        CubeCell across = CubeSphere.CellAt(edgeOfZ + new Vector3D(0.0007, 0, -0.0007), Size);

        Assert.Equal(0, across.Face);
        Assert.Equal(0, across.Column);
        Assert.InRange(Gap(new CubeCell(4, Size - 1, Size / 2), across), 0.05, 0.13);
    }

    [Fact]
    public void ZeroOrBrokenDirections_AreRefused()
    {
        Assert.Throws<ArgumentException>(() => CubeSphere.CellAt(Vector3D.Zero, Size));
        Assert.Throws<ArgumentException>(
            () => CubeSphere.CellAt(new Vector3D(double.NaN, 0, 1), Size));
    }

    [Fact]
    public void CellsOffTheGrid_AreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CubeSphere.CellCenter(new CubeCell(6, 0, 0), Size));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CubeSphere.CellCenter(new CubeCell(0, Size, 0), Size));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CubeSphere.CellAt(new Vector3D(0, 0, 1), 0));
    }

    [Fact]
    public void ACellInsideAFace_HasItsEightNeighbors()
    {
        var cell = new CubeCell(2, 100, 200);

        HashSet<CubeCell> around = [.. CubeSphere.Neighbors(cell, Size)];

        Assert.Equal(8, around.Count);
        Assert.All(around, next => Assert.True(
            next.Face == 2 && Math.Abs(next.Column - 100) <= 1 && Math.Abs(next.Row - 200) <= 1));
    }

    [Theory]
    [InlineData(0, 0, 500)]           // On a face's edge
    [InlineData(3, 1023, 7)]          // On another edge
    [InlineData(4, 0, 0)]             // A cube's corner
    [InlineData(1, 1023, 1023)]       // Another corner
    public void CellsOnAnEdge_HaveNeighborsAcrossIt_AllClose(int face, int column, int row)
    {
        var cell = new CubeCell(face, column, row);

        List<CubeCell> around = [.. CubeSphere.Neighbors(cell, Size)];

        Assert.InRange(around.Count, 7, 8);
        Assert.Equal(around.Count, around.Distinct().Count());
        Assert.DoesNotContain(cell, around);
        Assert.Contains(around, next => next.Face != face);
        double side = Gap(new CubeCell(0, 512, 512), new CubeCell(0, 513, 512));
        Assert.All(around, next => Assert.InRange(Gap(cell, next), 0.3 * side, 2 * side));
    }

    [Theory]
    [InlineData(0, 0, 500)]
    [InlineData(4, 0, 0)]
    [InlineData(2, 300, 1023)]
    public void Neighbors_AreMutual(int face, int column, int row)
    {
        var cell = new CubeCell(face, column, row);

        Assert.All(CubeSphere.Neighbors(cell, Size),
            next => Assert.Contains(cell, CubeSphere.Neighbors(next, Size)));
    }

    // The angle between two cells' centers, in degrees.
    private static double Gap(CubeCell a, CubeCell b)
    {
        double dot = CubeSphere.CellCenter(a, Size).Dot(CubeSphere.CellCenter(b, Size));
        return double.RadiansToDegrees(Math.Acos(Math.Clamp(dot, -1, 1)));
    }
}
