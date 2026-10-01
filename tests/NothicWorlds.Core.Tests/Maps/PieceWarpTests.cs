using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Maps;

public class PieceWarpTests
{
    private static readonly PieceOutline _rectangle =
        PieceOutline.Rectangle(new(0.2, 0.2), new(0.6, 0.5), 2.0);

    // An L-shaped (concave) cut, to check the stretch works for any shape.
    private static readonly PieceOutline _lShape = PieceOutline.Create(
        [new(0.1, 0.1), new(0.5, 0.1), new(0.5, 0.4), new(0.3, 0.4), new(0.3, 0.9), new(0.1, 0.9)],
        1.0);

    [Fact]
    public void UnwarpedPoints_AreTheOutlineInsideItsBox()
    {
        IReadOnlyList<ImagePoint> points = PieceWarp.UnwarpedPoints(_rectangle);

        Assert.Equal([new(0, 0), new(1, 0), new(1, 1), new(0, 1)], points);
    }

    [Theory]
    [InlineData(0.5, 0.5)]
    [InlineData(0.1, 0.9)]
    [InlineData(0.73, 0.21)]
    public void Unmoved_ChangesNothing(double u, double v)
    {
        var warp = new PieceWarp(_lShape, PieceWarp.UnwarpedPoints(_lShape));

        ImagePoint moved = warp.Forward(new ImagePoint(u, v));

        Assert.Equal(u, moved.U, 1e-9);
        Assert.Equal(v, moved.V, 1e-9);
    }

    [Fact]
    public void EveryPoint_LandsExactlyWhereItWasDragged()
    {
        ImagePoint[] targets =
            [.. PieceWarp.UnwarpedPoints(_lShape).Select((p, i) => new ImagePoint(
                p.U + 0.05 * i, p.V - 0.03 * i))];
        var warp = new PieceWarp(_lShape, targets);

        IReadOnlyList<ImagePoint> start = PieceWarp.UnwarpedPoints(_lShape);
        for (int i = 0; i < targets.Length; i++)
        {
            ImagePoint moved = warp.Forward(start[i]);
            Assert.Equal(targets[i].U, moved.U, 1e-9);
            Assert.Equal(targets[i].V, moved.V, 1e-9);
        }
    }

    [Fact]
    public void MovingEveryPointTogether_SlidesTheWholePiece()
    {
        var warp = new PieceWarp(_lShape, [.. PieceWarp.UnwarpedPoints(_lShape)
            .Select(p => new ImagePoint(p.U + 0.1, p.V - 0.2))]);

        ImagePoint moved = warp.Forward(new ImagePoint(0.2, 0.6));

        Assert.Equal(0.3, moved.U, 1e-9);
        Assert.Equal(0.4, moved.V, 1e-9);
    }

    [Fact]
    public void PointsOnAnEdge_StayOnTheLineBetweenItsEnds()
    {
        // Pull the top-right corner up and right; the top edge's middle follows halfway.
        var warp = new PieceWarp(_rectangle,
            [new(0, 0), new(1.2, -0.2), new(1, 1), new(0, 1)]);

        ImagePoint middleOfTop = warp.Forward(new ImagePoint(0.5, 0));

        Assert.Equal(0.6, middleOfTop.U, 1e-9);
        Assert.Equal(-0.1, middleOfTop.V, 1e-9);
    }

    [Fact]
    public void MovingOneCorner_PullsNearbyAreasMoreThanFarOnes()
    {
        var warp = new PieceWarp(_rectangle,
            [new(0, 0), new(1.3, 0), new(1, 1), new(0, 1)]);

        double nearShift = warp.Forward(new ImagePoint(0.9, 0.1)).U - 0.9;
        double farShift = warp.Forward(new ImagePoint(0.1, 0.9)).U - 0.1;

        Assert.True(nearShift > farShift);
        Assert.True(farShift >= 0);
    }

    [Fact]
    public void WrongNumberOfPoints_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PieceWarp(_rectangle, [new(0, 0)]));
    }

    [Fact]
    public void NonFinitePoints_Throw()
    {
        Assert.Throws<ArgumentException>(() => new PieceWarp(_rectangle,
            [new(0, 0), new(double.NaN, 0), new(1, 1), new(0, 1)]));
    }

    [Theory]
    [InlineData(0.5, 0.5)]
    [InlineData(0.2, 0.8)]
    [InlineData(0.95, 0.05)]
    public void Lookup_UndoesTheWarp(double u, double v)
    {
        var warp = new PieceWarp(_rectangle,
            [new(-0.1, 0), new(1.2, -0.1), new(1, 1.1), new(0.1, 0.9)]);
        WarpLookup lookup = warp.BakeLookup();

        ImagePoint moved = warp.Forward(new ImagePoint(u, v));
        ImagePoint? back = lookup.Sample(moved);

        Assert.NotNull(back);
        Assert.Equal(u, back.Value.U, 0.01);  // Within a fraction of a lookup cell
        Assert.Equal(v, back.Value.V, 0.01);
    }

    [Theory]
    [InlineData(0.995, true)]   // Just inside the right edge
    [InlineData(1.005, false)]  // Just outside it: cut exactly, not at the lookup's cells
    public void Lookup_CutsTheEdgeExactly(double u, bool covered)
    {
        var warp = new PieceWarp(_rectangle, PieceWarp.UnwarpedPoints(_rectangle));

        ImagePoint? found = warp.BakeLookup().Sample(new ImagePoint(u, 0.5));

        Assert.Equal(covered, found is not null);
    }

    [Fact]
    public void Lookup_IsEmptyAwayFromTheCut()
    {
        // The L-shape's box has an empty corner (bottom right), far from the cut.
        var warp = new PieceWarp(_lShape, PieceWarp.UnwarpedPoints(_lShape));
        WarpLookup lookup = warp.BakeLookup();

        Assert.Null(lookup.Sample(new ImagePoint(0.95, 0.95)));
        Assert.Null(lookup.Sample(new ImagePoint(5, 5)));  // Far outside the piece
        Assert.NotNull(lookup.Sample(new ImagePoint(0.2, 0.5)));
    }

    [Fact]
    public void Lookup_ExtentCoversTheWarpedPiece()
    {
        var warp = new PieceWarp(_rectangle,
            [new(0, 0), new(1.5, -0.25), new(1, 1), new(0, 1)]);

        WarpLookup lookup = warp.BakeLookup();

        // It reaches a little past the piece, so the piece's edge can be cut exactly.
        Assert.InRange(lookup.MaxU, 1.5, 1.6);
        Assert.InRange(lookup.MinV, -0.35, -0.25);
    }

    [Fact]
    public void Reach_CoversTheWarpedExtent()
    {
        var warp = new PieceWarp(_rectangle,
            [new(0, 0), new(2, 0), new(1, 1), new(0, 1)]);
        WarpLookup lookup = warp.BakeLookup();

        // Unwarped, a 0.2 × 0.1 radian box reaches about 0.112; stretched to twice the width
        // on one side, it reaches farther.
        Assert.True(lookup.ReachRadians(0.2, 0.1) > 0.15);
    }

    [Fact]
    public void PieceAt_FindsTheStretchedPartOfAWarpedPiece()
    {
        var piece = new MapPiece
        {
            AssetName = "assets/a.png",
            Outline = PieceOutline.Rectangle(new(0, 0), new(1, 1), 1.0),
            Center = new GeoCoordinate(0, 0),
            WidthDegrees = 20,
        };
        var beyondTheRightEdge = new GeoCoordinate(0, 14);  // The box ends at 10° E
        Assert.Null(PieceManipulation.PieceAt([piece], beyondTheRightEdge));

        // Drag both right-hand corners further east.
        piece.WarpedPoints = [new(0, 0), new(1.5, 0), new(1.5, 1), new(0, 1)];

        Assert.Same(piece, PieceManipulation.PieceAt([piece], beyondTheRightEdge));
    }
}
