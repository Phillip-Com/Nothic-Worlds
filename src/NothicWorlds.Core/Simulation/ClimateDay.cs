namespace NothicWorlds.Core.Simulation;

/// <summary>One day's weather at a spot (see <see cref="ClimateYear"/>).</summary>
/// <param name="TimeDays">The day's middle, in standard days.</param>
/// <param name="MeanC">The day's average temperature, in °C.</param>
/// <param name="LowC">The night-time low, in °C.</param>
/// <param name="HighC">The daytime high, in °C.</param>
/// <param name="DaylightHours">How long the star is up, in standard hours.</param>
/// <param name="NoonSunDegrees">
/// How high the star stands at midday, in degrees above the horizon (below zero: it doesn't
/// rise).
/// </param>
/// <param name="Sunlight">
/// The day's sunlight compared with the body's yearly average over its whole surface (1 is
/// average; Earth's equator gets about 1.2, its poles about 0.5 over a year).
/// </param>
public readonly record struct ClimateDay(
    double TimeDays,
    double MeanC,
    double LowC,
    double HighC,
    double DaylightHours,
    double NoonSunDegrees,
    double Sunlight);
