using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public class RgbColorTests
{
    [Theory]
    [InlineData("#E6EDF5", 0xE6, 0xED, 0xF5)]
    [InlineData("#e6edf5", 0xE6, 0xED, 0xF5)]  // Lowercase is accepted
    [InlineData("#000000", 0, 0, 0)]
    [InlineData("#FFFFFF", 255, 255, 255)]
    public void TryParseHex_ValidCodes(string text, byte r, byte g, byte b)
    {
        Assert.True(RgbColor.TryParseHex(text, out RgbColor color));
        Assert.Equal(new RgbColor(r, g, b), color);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("E6EDF5")]     // Missing #
    [InlineData("#E6EDF")]     // Too short
    [InlineData("#E6EDF5FF")]  // Alpha isn't supported
    [InlineData("#GGGGGG")]
    [InlineData("blue")]
    public void TryParseHex_InvalidCodes(string? text)
    {
        Assert.False(RgbColor.TryParseHex(text, out _));
    }

    [Fact]
    public void ToHex_IsUppercaseAndRoundTrips()
    {
        var color = new RgbColor(0x0A, 0xB1, 0xFF);

        Assert.Equal("#0AB1FF", color.ToHex());
        Assert.True(RgbColor.TryParseHex(color.ToHex(), out RgbColor parsed));
        Assert.Equal(color, parsed);
    }
}
