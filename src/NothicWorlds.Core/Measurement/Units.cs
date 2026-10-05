using System.Globalization;

namespace NothicWorlds.Core.Measurement;

/// <summary>
/// Converting and writing measurements in the chosen <see cref="UnitSystem"/> (VISION.md UI-04;
/// owner's choices: everyday units switch, astronomical ones don't, and the default follows the
/// computer's region). Values come in and go out in the quantity's metric unit; only what's
/// shown and typed changes.
/// </summary>
public static class Units
{
    /// <summary>Kilometers in an astronomical unit.</summary>
    public const double KmPerAu = 149_597_870.7;

    // How many metric units make one imperial unit (for temperature, the scale part).
    private const double KmPerMile = 1.609344;
    private const double MetersPerFoot = 0.3048;
    private const double MmPerInch = 25.4;
    private const double SquareKmPerSquareMile = KmPerMile * KmPerMile;
    private const double LitersPerGallon = 3.785411784;
    private const double KgPerPound = 0.45359237;

    // g/cm³ is 1000 kg/m³; a pound per cubic foot is KgPerPound / MetersPerFoot³ kg/m³.
    private const double GramsPerCubicCmPerPoundPerCubicFoot =
        KgPerPound / (MetersPerFoot * MetersPerFoot * MetersPerFoot) / 1000;

    // Countries that use US customary units day to day (by their two-letter region code).
    private static readonly HashSet<string> _imperialRegions = ["US", "LR", "MM"];

    /// <summary>
    /// A metric value as shown in <paramref name="system"/> (e.g. 6,371 km as 3,958.8 miles).
    /// </summary>
    public static double ToShown(Quantity quantity, double metric, UnitSystem system)
    {
        if (system == UnitSystem.Metric)
        {
            return metric;
        }

        return quantity == Quantity.Temperature
            ? metric * 9 / 5 + 32
            : metric / MetricPerImperial(quantity);
    }

    /// <summary>A value typed in <paramref name="system"/>, back in metric to be stored.</summary>
    public static double ToMetric(Quantity quantity, double shown, UnitSystem system)
    {
        if (system == UnitSystem.Metric)
        {
            return shown;
        }

        return quantity == Quantity.Temperature
            ? (shown - 32) * 5 / 9
            : shown * MetricPerImperial(quantity);
    }

    /// <summary>The unit's symbol: "km" or "mi", "°C" or "°F".</summary>
    public static string Symbol(Quantity quantity, UnitSystem system) => (quantity, system) switch
    {
        (Quantity.Distance, UnitSystem.Metric) => "km",
        (Quantity.Distance, _) => "mi",
        (Quantity.Length, UnitSystem.Metric) => "m",
        (Quantity.Length, _) => "ft",
        (Quantity.Speed, UnitSystem.Metric) => "km/h",
        (Quantity.Speed, _) => "mph",
        (Quantity.Temperature or Quantity.TemperatureChange, UnitSystem.Metric) => "°C",
        (Quantity.Temperature or Quantity.TemperatureChange, _) => "°F",
        (Quantity.Precipitation, UnitSystem.Metric) => "mm",
        (Quantity.Precipitation, _) => "in",
        (Quantity.Area, UnitSystem.Metric) => "km²",
        (Quantity.Area, _) => "mi²",
        (Quantity.Volume, UnitSystem.Metric) => "L",
        (Quantity.Volume, _) => "gal",
        (Quantity.Mass, UnitSystem.Metric) => "kg",
        (Quantity.Mass, _) => "lb",
        (Quantity.Density, UnitSystem.Metric) => "g/cm³",
        _ => "lb/ft³",
    };

    /// <summary>
    /// A metric value written in <paramref name="system"/> with its symbol, to
    /// <paramref name="decimals"/> places, in the computer's number style: "3,958.8 mi".
    /// </summary>
    public static string Format(Quantity quantity, double metric, UnitSystem system,
        int decimals = 0)
    {
        double shown = ToShown(quantity, metric, system);
        return $"{shown.ToString($"N{decimals}", CultureInfo.CurrentCulture)} " +
            Symbol(quantity, system);
    }

    /// <summary>
    /// A distance in km written for reading: in AU from a tenth of one (the same in both
    /// systems), in millions of km or miles from a million, otherwise in whole km or miles.
    /// </summary>
    public static string FormatDistance(double km, UnitSystem system)
    {
        if (km >= 0.1 * KmPerAu)
        {
            return $"{(km / KmPerAu).ToString("#,0.###", CultureInfo.CurrentCulture)} AU";
        }

        double shown = ToShown(Quantity.Distance, km, system);
        string symbol = Symbol(Quantity.Distance, system);
        return shown >= 1e6
            ? $"{(shown / 1e6).ToString("0.##", CultureInfo.CurrentCulture)} million {symbol}"
            : $"{shown.ToString("N0", CultureInfo.CurrentCulture)} {symbol}";
    }

    /// <summary>
    /// The system to start in on a computer set to <paramref name="locale"/> ("en_US",
    /// "en-US", "my_MM"): imperial where it's used day to day (the United States, Liberia,
    /// Myanmar), metric everywhere else, and metric if the region can't be told.
    /// </summary>
    public static UnitSystem DefaultFor(string? locale)
    {
        string[] parts = (locale ?? "").Split('_', '-', '.', '@');
        return parts.Length > 1 && _imperialRegions.Contains(parts[1].ToUpperInvariant())
            ? UnitSystem.Imperial
            : UnitSystem.Metric;
    }

    // How many of the quantity's metric unit make one of its imperial unit.
    private static double MetricPerImperial(Quantity quantity) => quantity switch
    {
        Quantity.Distance or Quantity.Speed => KmPerMile,
        Quantity.Length => MetersPerFoot,
        Quantity.TemperatureChange => 5.0 / 9,
        Quantity.Precipitation => MmPerInch,
        Quantity.Area => SquareKmPerSquareMile,
        Quantity.Volume => LitersPerGallon,
        Quantity.Mass => KgPerPound,
        Quantity.Density => GramsPerCubicCmPerPoundPerCubicFoot,
        _ => throw new ArgumentOutOfRangeException(nameof(quantity), quantity, null),
    };
}
