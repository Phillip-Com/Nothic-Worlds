using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public sealed class TerrainGridTests
{
    private const byte Forest = 5;
    private const byte Desert = 9;

    [Fact]
    public void Empty_HasNothingPainted()
    {
        TerrainGrid grid = TerrainGrid.Empty;

        Assert.True(grid.IsEmpty);
        Assert.Equal(0, grid.CodeAt(At(12, 34)));
        Assert.False(grid.Uses(Forest));
    }

    [Fact]
    public void Paint_FillsEveryCellWithinTheRadius_AndNothingBeyond()
    {
        TerrainGrid grid = TerrainGrid.Empty.Paint(At(20, 40), 5, Forest);

        Assert.Equal(Forest, grid.CodeAt(At(20, 40)));
        Assert.Equal(Forest, grid.CodeAt(At(24.5, 40)));    // 4.5° north
        Assert.Equal(Forest, grid.CodeAt(At(20, 44.7)));    // About 4.4° east
        Assert.Equal(0, grid.CodeAt(At(25.5, 40)));         // 5.5° north
        Assert.Equal(0, grid.CodeAt(At(20, 46.5)));         // About 6.1° east
        Assert.True(grid.Uses(Forest));
    }

    [Fact]
    public void Paint_LeavesTheOriginalGridAlone()
    {
        TerrainGrid before = TerrainGrid.Empty.Paint(At(0, 0), 3, Forest);

        TerrainGrid after = before.Paint(At(0, 0), 3, Desert);

        Assert.Equal(Forest, before.CodeAt(At(0, 0)));
        Assert.Equal(Desert, after.CodeAt(At(0, 0)));
    }

    [Fact]
    public void PaintingWithoutChangingAnything_ReturnsTheSameGrid()
    {
        TerrainGrid grid = TerrainGrid.Empty.Paint(At(0, 0), 3, Forest);

        Assert.Same(grid, grid.Paint(At(0, 0), 3, Forest));
        Assert.Same(TerrainGrid.Empty, TerrainGrid.Empty.Paint(At(0, 0), 3, 0));
    }

    [Fact]
    public void ErasingEverything_LeavesAnEmptyGrid()
    {
        TerrainGrid grid = TerrainGrid.Empty.Paint(At(10, 10), 4, Forest);

        TerrainGrid erased = grid.Paint(At(10, 10), 6, 0);

        Assert.True(erased.IsEmpty);
        Assert.True(erased.HasSameCells(TerrainGrid.Empty));
    }

    [Fact]
    public void ATinyBrush_PaintsTheCellUnderIt()
    {
        CubeCell cell = new(2, 300, 700);
        Vector3D center = CubeSphere.CellCenter(cell, TerrainGrid.FaceSize);

        TerrainGrid grid =
            TerrainGrid.Empty.Paint(center, TerrainGrid.MinBrushRadiusDegrees, Forest);

        Assert.Equal(Forest, grid.CodeAt(cell));
        Assert.Equal(0, grid.CodeAt(cell with { Column = 301 }));
    }

    [Fact]
    public void ABrushAcrossAFaceEdge_PaintsBothFaces()
    {
        // Longitude 45° on the equator is the corner between the +Z and +X faces.
        TerrainGrid grid = TerrainGrid.Empty.Paint(At(0, 45), 2, Forest);

        Assert.Equal(Forest, grid.CodeAt(At(0, 44)));
        Assert.Equal(Forest, grid.CodeAt(At(0, 46)));
        Assert.Equal([0, 4], grid.FacesChangedFrom(TerrainGrid.Empty));
    }

    [Fact]
    public void ABrushOnThePole_PaintsAllAroundIt()
    {
        TerrainGrid grid = TerrainGrid.Empty.Paint(At(90, 0), 3, Forest);

        foreach (double longitude in new[] { -180.0, -90, 0, 90, 135 })
        {
            Assert.Equal(Forest, grid.CodeAt(At(88, longitude)));
            Assert.Equal(0, grid.CodeAt(At(86.5, longitude)));
        }
    }

    [Fact]
    public void TheLargestBrush_PaintsAHemisphere()
    {
        TerrainGrid grid =
            TerrainGrid.Empty.Paint(At(0, 0), TerrainGrid.MaxBrushRadiusDegrees, Desert);

        Assert.Equal(Desert, grid.CodeAt(At(0, 89)));
        Assert.Equal(Desert, grid.CodeAt(At(89, 0)));
        Assert.Equal(0, grid.CodeAt(At(0, 91)));
        Assert.Equal(0, grid.CodeAt(At(-30, 180)));
    }

    [Fact]
    public void PaintStroke_PaintsTheWholePath_WithRoundEnds()
    {
        TerrainGrid grid = TerrainGrid.Empty.PaintStroke(At(0, 0), At(0, 20), 2, Forest);

        Assert.Equal(Forest, grid.CodeAt(At(0, 10)));
        Assert.Equal(Forest, grid.CodeAt(At(1.8, 10)));
        Assert.Equal(0, grid.CodeAt(At(2.3, 10)));
        Assert.Equal(Forest, grid.CodeAt(At(0, 21.8)));   // Round end
        Assert.Equal(0, grid.CodeAt(At(0, 22.3)));
        Assert.Equal(0, grid.CodeAt(At(1.8, 21.8)));     // Not a square end
    }

    [Fact]
    public void PaintStroke_OverThePole_FollowsTheShortestPath()
    {
        TerrainGrid grid = TerrainGrid.Empty.PaintStroke(At(80, 0), At(80, 180), 1, Forest);

        Assert.Equal(Forest, grid.CodeAt(At(90, 0)));
        Assert.Equal(Forest, grid.CodeAt(At(85, 180)));
        Assert.Equal(0, grid.CodeAt(At(85, 90)));
    }

    [Fact]
    public void PaintStroke_BetweenOppositePoints_PaintsBothEnds()
    {
        TerrainGrid grid = TerrainGrid.Empty.PaintStroke(At(0, 0), At(0, 180), 1, Forest);

        Assert.Equal(Forest, grid.CodeAt(At(0, 0)));
        Assert.Equal(Forest, grid.CodeAt(At(0, 180)));
    }

    [Fact]
    public void PaintStroke_FromAPointToItself_IsAStamp()
    {
        TerrainGrid stroke = TerrainGrid.Empty.PaintStroke(At(5, 5), At(5, 5), 2, Forest);

        Assert.True(stroke.HasSameCells(TerrainGrid.Empty.Paint(At(5, 5), 2, Forest)));
    }

    [Fact]
    public void Replace_ChangesOneCodeEverywhere()
    {
        TerrainGrid grid = TerrainGrid.Empty
            .Paint(At(0, 0), 3, Forest)
            .Paint(At(40, 40), 3, Desert);

        TerrainGrid replaced = grid.Replace(Forest, 0);

        Assert.Equal(0, replaced.CodeAt(At(0, 0)));
        Assert.Equal(Desert, replaced.CodeAt(At(40, 40)));
        Assert.False(replaced.Uses(Forest));
        Assert.Same(grid, grid.Replace(77, 0));  // Nothing uses 77.
    }

    [Fact]
    public void ReplacingUnpainted_FillsTheRestOfTheBody()
    {
        TerrainGrid grid = TerrainGrid.Empty.Paint(At(0, 0), 3, Forest);

        TerrainGrid filled = grid.Replace(0, Desert);

        Assert.Equal(Forest, filled.CodeAt(At(0, 0)));
        Assert.Equal(Desert, filled.CodeAt(At(-60, 120)));
        Assert.False(filled.Uses(0));
    }

    [Fact]
    public void FacesChangedFrom_NamesOnlyTheFacesPainted()
    {
        TerrainGrid before = TerrainGrid.Empty.Paint(At(90, 0), 3, Forest);  // North face

        TerrainGrid after = before.Paint(At(0, 0), 3, Desert);  // Middle of the +Z face

        Assert.Equal([4], after.FacesChangedFrom(before));
        Assert.Empty(after.FacesChangedFrom(after));
    }

    [Fact]
    public void CopyFaceAndFromCells_RoundTrip()
    {
        TerrainGrid grid = TerrainGrid.Empty
            .PaintStroke(At(-10, -70), At(30, 100), 4, Forest)
            .Paint(At(-80, 0), 8, Desert);
        var cells = new byte[TerrainGrid.CellCount];
        for (int face = 0; face < CubeSphere.FaceCount; face++)
        {
            grid.CopyFace(face, cells.AsSpan(face * TerrainGrid.CellsPerFace));
        }

        TerrainGrid copy = TerrainGrid.FromCells(cells);

        Assert.True(copy.HasSameCells(grid));
        Assert.False(copy.HasSameCells(grid.Paint(At(50, 50), 1, Desert)));
    }

    [Fact]
    public void FromCells_TheWrongNumberOfCells_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => TerrainGrid.FromCells(new byte[100]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90.5)]
    [InlineData(double.NaN)]
    public void BrushRadiiOutOfRange_AreRefused(double radius)
    {
        Assert.Throws<ArgumentException>(() => TerrainGrid.Empty.Paint(At(0, 0), radius, Forest));
    }

    [Fact]
    public void ABrokenCenter_IsRefused()
    {
        Assert.Throws<ArgumentException>(
            () => TerrainGrid.Empty.Paint(Vector3D.Zero, 1, Forest));
        Assert.Throws<ArgumentException>(
            () => TerrainGrid.Empty.PaintStroke(At(0, 0), new Vector3D(0, double.NaN, 1), 1, 1));
    }

    private static Vector3D At(double latitude, double longitude)
    {
        System.Numerics.Vector3 direction =
            SphericalCoordinates.ToDirection(new GeoCoordinate(latitude, longitude));
        return new Vector3D(direction.X, direction.Y, direction.Z);
    }
}
