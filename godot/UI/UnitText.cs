using NothicWorlds.Core.Measurement;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// Measurements as they're shown, in the units chosen in File ▸ Settings (VISION.md UI-04):
/// a short way to reach <see cref="Units"/> with <see cref="AppSettings.Units"/> filled in.
/// Values given are always metric, as the world stores them.
/// </summary>
public static class UnitText
{
    /// <summary>The units chosen now.</summary>
    public static UnitSystem System => AppSettings.Units;

    /// <summary>A metric value written in the chosen units: "3,959 mi", "59 °F".</summary>
    public static string Format(Quantity quantity, double metric, int decimals = 0) =>
        Units.Format(quantity, metric, System, decimals);

    /// <summary>A distance in km for reading: AU, millions, or whole km or miles.</summary>
    public static string Distance(double km) => Units.FormatDistance(km, System);

    /// <summary>The chosen unit's symbol: "km" or "mi".</summary>
    public static string Symbol(Quantity quantity) => Units.Symbol(quantity, System);

    /// <summary>A metric value as a number in the chosen units.</summary>
    public static double Shown(Quantity quantity, double metric) =>
        Units.ToShown(quantity, metric, System);
}
