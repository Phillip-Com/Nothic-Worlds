namespace NothicWorlds.Core.Simulation;

/// <summary>The live weather at one spot at one moment (VISION.md WTH-02).</summary>
/// <param name="CloudCover">How much of the sky is cloud, 0 (clear) to 1 (overcast).</param>
/// <param name="PrecipitationMmPerHour">
/// How hard rain or snow is falling, as water, in mm per standard hour (0 when dry).
/// </param>
/// <param name="Precipitation">What is falling.</param>
/// <param name="WindEastMs">The wind toward the east, in m/s (negative: toward the west).</param>
/// <param name="WindNorthMs">
/// The wind toward the north, in m/s (negative: toward the south).
/// </param>
/// <param name="TemperatureC">The day's mean temperature here, in °C.</param>
/// <param name="Storm">The storm the spot is in, or null.</param>
public readonly record struct WeatherSample(
    double CloudCover,
    double PrecipitationMmPerHour,
    PrecipitationKind Precipitation,
    double WindEastMs,
    double WindNorthMs,
    double TemperatureC,
    StormKind? Storm)
{
    /// <summary>How fast the wind blows, in m/s.</summary>
    public double WindSpeedMs => Math.Sqrt(WindEastMs * WindEastMs + WindNorthMs * WindNorthMs);

    /// <summary>
    /// The compass direction the wind blows from, in degrees clockwise from north (a west wind,
    /// blowing toward the east, is 270).
    /// </summary>
    public double WindFromDegrees =>
        (double.RadiansToDegrees(Math.Atan2(-WindEastMs, -WindNorthMs)) + 360) % 360;
}
