using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class SunlightThroughAirTests
{
    [Fact]
    public void ASunOverheadAtSeaLevel_IsWhiteAtFullStrength()
    {
        var light = SunlightThroughAir.At(90, 0, hasAir: true);

        Assert.Equal(1, light.Red, 6);
        Assert.Equal(1, light.Green, 6);
        Assert.Equal(1, light.Blue, 6);
        Assert.Equal(1, light.Strength, 6);
    }

    [Fact]
    public void AnEveningSun_IsDimmerAndRedder()
    {
        var noon = SunlightThroughAir.At(60, 0, hasAir: true);
        var evening = SunlightThroughAir.At(5, 0, hasAir: true);

        Assert.True(evening.Strength < noon.Strength / 2);
        Assert.Equal(1, evening.Red, 6);
        Assert.True(evening.Green < 0.75);
        Assert.True(evening.Blue < evening.Green / 2);
    }

    [Fact]
    public void ASetSun_GivesNoLight()
    {
        Assert.Equal(0, SunlightThroughAir.At(-1, 0, hasAir: true).Strength);
        Assert.Equal(0, SunlightThroughAir.At(-1, 0, hasAir: false).Strength);
    }

    [Fact]
    public void HigherUp_TheEveningSunIsLessReddened()
    {
        var low = SunlightThroughAir.At(5, 0, hasAir: true);
        var high = SunlightThroughAir.At(5, 10, hasAir: true);

        Assert.True(high.Blue > low.Blue);
        Assert.True(high.Strength > low.Strength);
    }

    [Fact]
    public void WithoutAir_TheSunIsWhiteRightDownToTheHorizon()
    {
        var light = SunlightThroughAir.At(1, 0, hasAir: false);

        Assert.Equal((1.0, 1.0, 1.0, 1.0), light);
    }

    [Fact]
    public void AirMass_IsOneOverheadAndAboutThirtyEightAtTheHorizon()
    {
        Assert.Equal(1, SunlightThroughAir.AirMass(90), 3);
        Assert.InRange(SunlightThroughAir.AirMass(0), 37, 39);
        Assert.Equal(SunlightThroughAir.AirMass(0), SunlightThroughAir.AirMass(-5));
    }

    [Fact]
    public void AirShare_FallsByAThirdEveryScaleHeight()
    {
        Assert.Equal(1, SunlightThroughAir.AirShare(-2));
        Assert.Equal(Math.Exp(-1), SunlightThroughAir.AirShare(SunlightThroughAir.AirScaleHeightKm),
            9);
    }
}
