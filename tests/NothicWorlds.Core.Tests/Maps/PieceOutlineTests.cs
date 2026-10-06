using NothicWorlds.Core.Maps;

namespace NothicWorlds.Core.Tests.Maps;

public class PieceOutlineTests
{
    [Fact]
    public void Rectangle_AcceptsCornersInAnyOrder()
    {
        PieceOutline outline = PieceOutline.Rectangle(new(0.6, 0.8), new(0.2, 0.3), 2.0);

        Assert.Equal(4, outline.Points.Count);
        Assert.Equal(0.2, outline.MinU);
        Assert.Equal(0.6, outline.MaxU);
        Assert.Equal(0.3, outline.MinV);
        Assert.Equal(0.8, outline.MaxV);
    }

    [Fact]
    public void Ellipse_FillsTheBoxBetweenTheCorners()
    {
        PieceOutline outline = PieceOutline.Ellipse(new(0.6, 0.8), new(0.2, 0.3), 2.0);

        Assert.Equal(PieceOutline.EllipsePoints, outline.Points.Count);
        Assert.Equal(0.2, outline.MinU, 1e-12);
        Assert.Equal(0.6, outline.MaxU, 1e-12);
        Assert.Equal(0.3, outline.MinV, 1e-12);
        Assert.Equal(0.8, outline.MaxV, 1e-12);
        Assert.True(outline.Contains(new(0.4, 0.55)));
        Assert.False(outline.Contains(new(0.21, 0.31)));  // A corner of the box is outside it.
    }

    [Fact]
    public void SquaredCorner_MakesASquareInPixels_TowardTheMouse()
    {
        // On a 2:1 image, 0.1 across is as many pixels as 0.2 down.
        ImagePoint corner = PieceOutline.SquaredCorner(new(0.5, 0.5), new(0.2, 0.9), 2.0);

        Assert.Equal(0.3, corner.U, 1e-12);  // The shorter side (0.6 px-units) wins.
        Assert.Equal(0.9, corner.V, 1e-12);
        PieceOutline circle = PieceOutline.Ellipse(new(0.5, 0.5), corner, 2.0);
        Assert.Equal(1.0, circle.BoxAspectRatio, 1e-9);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(12)]
    public void RegularShape_HasEqualCornersAroundTheCenter_InTrueProportions(int sides)
    {
        const double aspect = 2.0;
        PieceOutline outline =
            PieceOutline.RegularShape(new(0.5, 0.5), new(0.6, 0.5), sides, aspect);

        Assert.Equal(sides, outline.Points.Count);
        Assert.Equal(0.6, outline.Points[0].U, 1e-12);  // The first corner is at the mouse.
        foreach (ImagePoint point in outline.Points)
        {
            double across = (point.U - 0.5) * aspect;
            double down = point.V - 0.5;
            Assert.Equal(0.2, Math.Sqrt(across * across + down * down), 1e-9);
        }
    }

    [Fact]
    public void RegularShape_PullsCornersPastTheEdgeOntoTheImage()
    {
        PieceOutline outline =
            PieceOutline.RegularShape(new(0.1, 0.5), new(0.1, 0.1), 6, 1.0);

        Assert.All(outline.Points, point => Assert.InRange(point.U, 0.0, 1.0));
        Assert.Equal(0.0, outline.MinU);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(13)]
    public void RegularShape_RejectsSidesOutOfRange(int sides)
    {
        Assert.Throws<ArgumentException>(
            () => PieceOutline.RegularShape(new(0.5, 0.5), new(0.6, 0.5), sides, 1.0));
    }

    [Fact]
    public void BoxAspectRatio_AccountsForTheImageShape()
    {
        // Half the width and half the height of a 2:1 image: still 2:1 in reality.
        PieceOutline outline = PieceOutline.Rectangle(new(0, 0), new(0.5, 0.5), 2.0);

        Assert.Equal(2.0, outline.BoxAspectRatio, 1e-12);
    }

    [Theory]
    [MemberData(nameof(InvalidOutlines))]
    public void Create_RejectsUnusableOutlines(ImagePoint[] points, double aspectRatio)
    {
        Assert.Throws<ArgumentException>(() => PieceOutline.Create(points, aspectRatio));
    }

    public static TheoryData<ImagePoint[], double> InvalidOutlines => new()
    {
        { [new(0.1, 0.1), new(0.5, 0.5)], 2.0 },                    // Too few points
        { [new(0.1, 0.1), new(1.2, 0.1), new(0.5, 0.5)], 2.0 },     // Outside the image
        { [new(0.1, 0.1), new(0.2, 0.2), new(0.3, 0.3)], 2.0 },     // No area (a line)
        { [new(0.1, 0.1), new(0.5, 0.1), new(0.5, 0.5)], 0.0 },     // Bad aspect ratio
        { [new(0.1, double.NaN), new(0.5, 0.1), new(0.5, 0.5)], 2.0 },
    };

    [Fact]
    public void Mask_OfARectangle_IsFullyInside()
    {
        PieceOutline outline = PieceOutline.Rectangle(new(0.2, 0.2), new(0.4, 0.6), 1.0);

        byte[] mask = outline.RasterizeMask(20, 40);

        Assert.All(mask, value => Assert.Equal(255, value));
    }

    [Fact]
    public void Mask_OfATriangle_CoversAboutHalfWithSmoothEdges()
    {
        // Right triangle filling the lower-left half of its bounding box.
        PieceOutline outline = PieceOutline.Create(
            [new(0.0, 0.0), new(1.0, 1.0), new(0.0, 1.0)], 1.0);

        byte[] mask = outline.RasterizeMask(64, 64);

        double coverage = mask.Sum(v => v / 255.0) / mask.Length;
        Assert.InRange(coverage, 0.48, 0.52);
        Assert.Equal(255, mask[63 * 64 + 0]);   // Bottom-left: inside
        Assert.Equal(0, mask[0 * 64 + 63]);     // Top-right: outside
        Assert.Contains(mask, v => v is > 0 and < 255);  // Edge pixels are blended
    }

    [Fact]
    public void Mask_OfAnLShape_LeavesTheNotchEmpty()
    {
        // An L: the top-right quarter of the bounding box is cut away.
        PieceOutline outline = PieceOutline.Create(
        [
            new(0.0, 0.0), new(0.5, 0.0), new(0.5, 0.5), new(1.0, 0.5),
            new(1.0, 1.0), new(0.0, 1.0),
        ], 1.0);

        byte[] mask = outline.RasterizeMask(40, 40);

        Assert.Equal(0, mask[5 * 40 + 35]);     // In the notch
        Assert.Equal(255, mask[5 * 40 + 5]);    // Top-left arm
        Assert.Equal(255, mask[35 * 40 + 35]);  // Bottom-right arm
        double coverage = mask.Sum(v => v / 255.0) / mask.Length;
        Assert.InRange(coverage, 0.73, 0.77);   // Three quarters
    }

    [Theory]
    [InlineData(0.15, 0.85, true)]
    [InlineData(0.05, 0.5, false)]  // Left of the box
    [InlineData(0.8, 0.2, false)]   // In the box, but outside the triangle
    [InlineData(0.3, 0.7, true)]
    public void Contains_FollowsTheCutShape(double u, double v, bool expected)
    {
        // A triangle: top-left, bottom-left, bottom-right of the box (0.1–0.9).
        PieceOutline triangle = PieceOutline.Create(
            [new(0.1, 0.1), new(0.1, 0.9), new(0.9, 0.9)], 1.0);

        Assert.Equal(expected, triangle.Contains(new ImagePoint(u, v)));
    }
}
