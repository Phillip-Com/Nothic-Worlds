namespace NothicWorlds.Core.Model;

/// <summary>One month of a <see cref="Calendar"/>.</summary>
/// <param name="Name">Its name, e.g. "Highsun".</param>
/// <param name="Days">How many days it has (the body's own days).</param>
public readonly record struct CalendarMonth(string Name, int Days);
