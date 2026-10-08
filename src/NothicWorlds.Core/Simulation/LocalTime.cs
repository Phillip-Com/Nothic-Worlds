namespace NothicWorlds.Core.Simulation;

/// <summary>A day and time of day on one body (see <see cref="BodyClock.LocalTimeOn"/>).</summary>
/// <param name="Day">
/// The day number: 1 is the world's first day. Before time 0 it's 0 or less.
/// </param>
/// <param name="Hour">Standard hours since the day began.</param>
/// <param name="Minute">Minutes past the hour, 0 to 59.</param>
public readonly record struct LocalTime(long Day, int Hour, int Minute)
{
    /// <summary>For example "Day 1,204, 14:30".</summary>
    public override string ToString() => $"{DayName(Day)}, {Hour}:{Minute:00}";

    /// <summary>
    /// How a day number reads (VISION.md UI-07): "Day 1" for the world's first day, and before
    /// it, counted back as years are, with no day 0: the day before Day 1 is "Day -1".
    /// </summary>
    /// <param name="day">The day number, as <see cref="Day"/>.</param>
    public static string DayName(long day) => $"Day {(day > 0 ? day : day - 1):N0}";
}
