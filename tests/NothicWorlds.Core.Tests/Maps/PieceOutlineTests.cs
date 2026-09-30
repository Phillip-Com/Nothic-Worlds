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
}
