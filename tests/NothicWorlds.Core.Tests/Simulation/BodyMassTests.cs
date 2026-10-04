using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class BodyMassTests
{
    [Theory]
    [InlineData(BodyKind.Star, 696_000, 1.989e30)]  // The Sun
    [InlineData(BodyKind.Planet, 6371, 5.972e24)]  // Earth
    [InlineData(BodyKind.Planet, 69_911, 1.898e27)]  // Jupiter
    [InlineData(BodyKind.Moon, 1737, 7.35e22)]  // The Moon
    public void TypicalBodies_WeighAboutWhatRealOnesDo(BodyKind kind, double radiusKm,
        double realKg)
    {
        var body = new Body { Kind = kind, RadiusKm = radiusKm };

        Assert.InRange(BodyMass.Kg(body) / realKg, 0.95, 1.05);
    }

    [Fact]
    public void PlanetsGrowingFromRockToGas_GetSteadilyLessDense()
    {
        double previous = double.PositiveInfinity;
        for (double radius = 5000; radius < 40_000; radius += 500)
        {
            double density = BodyMass.TypicalDensity(BodyKind.Planet, radius);
            Assert.True(density <= previous, $"At {radius} km");
            previous = density;
        }

        Assert.Equal(5.513, BodyMass.TypicalDensity(BodyKind.Planet, 6371));
        Assert.Equal(1.33, BodyMass.TypicalDensity(BodyKind.Planet, 30_000));
    }

    [Fact]
    public void ASetDensity_IsUsed_AndScalesTheMass()
    {
        var body = new Body { RadiusKm = 6371 };
        double typical = BodyMass.Kg(body);

        body.DensityGramsPerCm3 = 5.513 * 2;

        Assert.Equal(11.026, BodyMass.Density(body));
        Assert.Equal(2, BodyMass.Kg(body) / typical, 9);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(1e8)]
    [InlineData(double.NaN)]
    public void ADensityOutOfRange_IsAProblem(double density)
    {
        var body = new Body { DensityGramsPerCm3 = density };

        Assert.Contains("density", body.Problem());
    }

    [Fact]
    public void Density_IsCopied_AndCompared()
    {
        var body = new Body { DensityGramsPerCm3 = 3 };

        Body copy = body.Clone();

        Assert.Equal(3, copy.DensityGramsPerCm3);
        Assert.True(body.HasSameContent(copy));
        copy.DensityGramsPerCm3 = null;
        Assert.False(body.HasSameContent(copy));
    }
}
