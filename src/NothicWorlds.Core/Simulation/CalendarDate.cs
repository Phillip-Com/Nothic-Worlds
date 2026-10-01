namespace NothicWorlds.Core.Simulation;

/// <summary>A date in a body's calendar (see <see cref="CalendarMath"/>).</summary>
/// <param name="Year">The year number.</param>
/// <param name="Month">The month (0 is the first).</param>
/// <param name="Day">The day of the month (1 is the first).</param>
/// <param name="Weekday">
/// The weekday (0 is the first), or null for a calendar without weeks.
/// </param>
public readonly record struct CalendarDate(long Year, int Month, int Day, int? Weekday);
