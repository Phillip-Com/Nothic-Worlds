using System.Globalization;
using NothicWorlds.Core.Measurement;

namespace NothicWorlds.Core.Tests.Measurement;

public class UnitsTests
{
    [Theory]
    [InlineData(Quantity.Distance, 6371, 3958.76)]        // Earth's radius
    [InlineData(Quantity.Length, 8848.86, 29031.7)]      // Everest
    [InlineData(Quantity.Speed, 100, 62.137)]
    [InlineData(Quantity.Temperature, 0, 32)]
    [InlineData(Quantity.Temperature, 100, 212)]
    [InlineData(Quantity.Temperature, -40, -40)]
    [InlineData(Quantity.TemperatureChange, 10, 18)]
    [InlineData(Quantity.Precipitation, 25.4, 1)]
    [InlineData(Quantity.Area, 2.589988, 1)]
    [InlineData(Quantity.Volume, 3.785412, 1)]
    [InlineData(Quantity.Mass, 1, 2.20462)]
    [InlineData(Quantity.Density, 5.514, 344.23)]        // Earth's mean density
    public void Imperial_ShowsKnownValues(Quantity quantity, double metric, double imperial)
    {
        double shown = Units.ToShown(quantity, metric, UnitSystem.Imperial);

        Assert.Equal(imperial, shown, Math.Abs(imperial) * 1e-4 + 1e-6);
    }

    [Fact]
    public void EveryQuantity_GoesThereAndBackExactly_InBothSystems()
    {
        foreach (Quantity quantity in Enum.GetValues<Quantity>())
        {
            foreach (UnitSystem system in Enum.GetValues<UnitSystem>())
            {
                foreach (double metric in new[] { -273.15, -1.5, 0, 0.001, 12.25, 6371, 1e9 })
                {
                    double back = Units.ToMetric(quantity, Units.ToShown(quantity, metric, system),
                        system);

                    Assert.Equal(metric, back, Math.Abs(metric) * 1e-12 + 1e-12);
                }
            }
        }
    }

    [Fact]
    public void Metric_IsShownAsItIs()
    {
        foreach (Quantity quantity in Enum.GetValues<Quantity>())
        {
            Assert.Equal(12.5, Units.ToShown(quantity, 12.5, UnitSystem.Metric));
            Assert.Equal(12.5, Units.ToMetric(quantity, 12.5, UnitSystem.Metric));
        }
    }

    [Fact]
    public void EveryQuantity_HasASymbolInEachSystem_AndTheyDiffer()
    {
        foreach (Quantity quantity in Enum.GetValues<Quantity>())
        {
            string metric = Units.Symbol(quantity, UnitSystem.Metric);
            string imperial = Units.Symbol(quantity, UnitSystem.Imperial);

            Assert.False(string.IsNullOrEmpty(metric));
            Assert.False(string.IsNullOrEmpty(imperial));
            Assert.NotEqual(metric, imperial);
        }
    }

    [Fact]
    public void Format_WritesTheShownValueAndSymbol()
    {
        using var _ = new InvariantCulture();

        Assert.Equal("3,958.8 mi", Units.Format(Quantity.Distance, 6371, UnitSystem.Imperial, 1));
        Assert.Equal("6,371 km", Units.Format(Quantity.Distance, 6371, UnitSystem.Metric));
        Assert.Equal("59 °F", Units.Format(Quantity.Temperature, 15, UnitSystem.Imperial));
        Assert.Equal("15 °C", Units.Format(Quantity.Temperature, 15, UnitSystem.Metric));
    }

    [Theory]
    [InlineData(384_400, UnitSystem.Metric, "384,400 km")]
    [InlineData(384_400, UnitSystem.Imperial, "238,855 mi")]
    [InlineData(5_000_000, UnitSystem.Metric, "5 million km")]
    [InlineData(5_000_000, UnitSystem.Imperial, "3.11 million mi")]
    [InlineData(149_597_870.7, UnitSystem.Metric, "1 AU")]       // The same in both systems
    [InlineData(149_597_870.7, UnitSystem.Imperial, "1 AU")]
    [InlineData(778_500_000, UnitSystem.Imperial, "5.204 AU")]
    public void Distances_ReadInTheirSystem_ButAUStayAU(double km, UnitSystem system,
        string expected)
    {
        using var _ = new InvariantCulture();

        Assert.Equal(expected, Units.FormatDistance(km, system));
    }

    [Theory]
    [InlineData("en_US", UnitSystem.Imperial)]
    [InlineData("en-US", UnitSystem.Imperial)]
    [InlineData("es_US", UnitSystem.Imperial)]
    [InlineData("en_LR", UnitSystem.Imperial)]
    [InlineData("my_MM", UnitSystem.Imperial)]
    [InlineData("en_US.UTF-8", UnitSystem.Imperial)]
    [InlineData("en_GB", UnitSystem.Metric)]
    [InlineData("fr_CA", UnitSystem.Metric)]
    [InlineData("de", UnitSystem.Metric)]       // No region: metric
    [InlineData("", UnitSystem.Metric)]
    [InlineData(null, UnitSystem.Metric)]
    public void TheDefault_FollowsTheComputersRegion(string? locale, UnitSystem expected)
    {
        Assert.Equal(expected, Units.DefaultFor(locale));
    }

    // Numbers written the invariant way ("3,958.8") for the length of a test.
    private sealed class InvariantCulture : IDisposable
    {
        private readonly CultureInfo _was = CultureInfo.CurrentCulture;

        public InvariantCulture() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        public void Dispose() => CultureInfo.CurrentCulture = _was;
    }
}
