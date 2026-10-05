namespace NothicWorlds.Core.Measurement;

/// <summary>
/// Which units measurements are shown and typed in (VISION.md UI-04; owner's choice: a
/// setting for this computer). Worlds always store metric, so they read the same everywhere.
/// </summary>
public enum UnitSystem
{
    /// <summary>Kilometers, meters, °C, millimeters, kilograms.</summary>
    Metric,

    /// <summary>Miles, feet, °F, inches, pounds (US customary).</summary>
    Imperial,
}
