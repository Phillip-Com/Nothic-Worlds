using NothicWorlds.Core.Maps;

namespace NothicWorlds.Core.Tests.Maps;

public class MapImageRulesTests
{
    [Theory]
    [InlineData(2048, 1024)]
    [InlineData(8192, 4096)]
    [InlineData(2048, 1025)]   // Within 1% of 2:1
    [InlineData(16384, 8192)]  // Too large, but still the right shape
    public void Check_TwoToOne_IsSupported(int width, int height)
    {
        Assert.True(MapImageRules.Check(width, height).IsSupportedLayout);
    }

    [Theory]
    [InlineData(1000, 1000)]  // Square
    [InlineData(3000, 1000)]  // 3:1
    [InlineData(2048, 1060)]  // About 3.5% off
    [InlineData(1024, 2048)]  // Upright 1:2
    public void Check_OtherShapes_AreNotSupported(int width, int height)
    {
        Assert.False(MapImageRules.Check(width, height).IsSupportedLayout);
    }

    [Theory]
    [InlineData(2048, 1024)]
    [InlineData(8192, 4096)]  // Exactly at the limit
    [InlineData(1, 1)]
    public void Check_WithinLimit_KeepsSize(int width, int height)
    {
        MapImageCheck check = MapImageRules.Check(width, height);

        Assert.False(check.NeedsResize);
        Assert.Equal(width, check.TargetWidth);
        Assert.Equal(height, check.TargetHeight);
    }

    [Theory]
    [InlineData(16384, 8192, 8192, 4096)]   // Twice the limit
    [InlineData(10000, 5000, 8192, 4096)]   // Slightly over, 2:1
    [InlineData(12000, 3000, 8192, 2048)]   // Too wide: width decides
    [InlineData(6000, 6000, 4096, 4096)]    // Too tall: height decides
    public void Check_OverLimit_ShrinksKeepingProportions(
        int width, int height, int expectedWidth, int expectedHeight)
    {
        MapImageCheck check = MapImageRules.Check(width, height);

        Assert.True(check.NeedsResize);
        Assert.Equal(expectedWidth, check.TargetWidth);
        Assert.Equal(expectedHeight, check.TargetHeight);
        Assert.InRange(check.TargetWidth, 1, MapImageRules.MaxWidth);
        Assert.InRange(check.TargetHeight, 1, MapImageRules.MaxHeight);
    }

    [Fact]
    public void Check_ExtremelyThinImage_NeverShrinksBelowOnePixel()
    {
        MapImageCheck check = MapImageRules.Check(100_000, 1);

        Assert.Equal(MapImageRules.MaxWidth, check.TargetWidth);
        Assert.Equal(1, check.TargetHeight);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-5, 100)]
    public void Check_NonPositiveSize_Throws(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapImageRules.Check(width, height));
    }
}
