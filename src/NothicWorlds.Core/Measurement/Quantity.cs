namespace NothicWorlds.Core.Measurement;

/// <summary>
/// A kind of measurement that switches between metric and imperial (VISION.md UI-04). Each
/// is stored in its metric unit, named first; the imperial one is second. Astronomical units
/// (AU, Earth and Sun masses, light-years) aren't here: they're the same in both systems
/// (owner's choice).
/// </summary>
public enum Quantity
{
    /// <summary>Kilometers, or miles: sizes of worlds, distances over them and in space.</summary>
    Distance,

    /// <summary>Meters, or feet: heights, depths, and short distances.</summary>
    Length,

    /// <summary>Kilometers an hour, or miles an hour.</summary>
    Speed,

    /// <summary>Degrees Celsius, or Fahrenheit.</summary>
    Temperature,

    /// <summary>A difference between temperatures (a seasonal swing), in °C or °F.</summary>
    TemperatureChange,

    /// <summary>Millimeters, or inches: rain and snow, as water.</summary>
    Precipitation,

    /// <summary>Square kilometers, or square miles.</summary>
    Area,

    /// <summary>Liters, or US gallons.</summary>
    Volume,

    /// <summary>Kilograms, or pounds.</summary>
    Mass,

    /// <summary>Grams per cubic centimeter, or pounds per cubic foot.</summary>
    Density,
}
