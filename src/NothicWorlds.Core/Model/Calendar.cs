namespace NothicWorlds.Core.Model;

/// <summary>
/// A body's own calendar (VISION.md CAL-01): named months of any length, named weekdays, year
/// numbering with an optional era, and which date the world's time 0 falls on. It counts the
/// body's own days. It doesn't have to match the body's year: a calendar is the user's design,
/// not physics. Immutable; change a calendar by replacing it.
/// </summary>
public sealed record Calendar
{
    /// <summary>The most months a calendar can have.</summary>
    public const int MaxMonths = 100;

    /// <summary>The most weekdays a calendar can have.</summary>
    public const int MaxWeekdays = 100;

    /// <summary>The longest a month can be, in days.</summary>
    public const int MaxMonthDays = 100_000;

    /// <summary>The months of the year, in order. At least one.</summary>
    public required IReadOnlyList<CalendarMonth> Months { get; init; }

    /// <summary>The days of the week, in order. Empty for a calendar without weeks.</summary>
    public IReadOnlyList<string> Weekdays { get; init; } = [];

    /// <summary>The year number at time 0 (e.g. 1203).</summary>
    public long FirstYear { get; init; } = 1;

    /// <summary>
    /// Words shown after the year number (e.g. "of the Third Age"), or null for none.
    /// </summary>
    public string? Era { get; init; }

    /// <summary>The month at time 0 (0 is the first month).</summary>
    public int StartMonth { get; init; }

    /// <summary>The day of the month at time 0 (1 is the first day).</summary>
    public int StartDay { get; init; } = 1;

    /// <summary>The weekday at time 0 (0 is the first weekday).</summary>
    public int StartWeekday { get; init; }

    /// <summary>
    /// Whether the world is kept fitted to this calendar, and how (VISION.md CAL-02). See
    /// <c>Simulation.CalendarFitting</c>.
    /// </summary>
    public CalendarFit Fit { get; init; } = CalendarFit.None;

    /// <summary>
    /// A moon of this body whose orbit is kept so it goes from new moon to new moon once per
    /// (average) month of this calendar, or null for none (VISION.md CAL-02).
    /// </summary>
    public Guid? MonthMoonId { get; init; }

    /// <summary>How many days a year of this calendar has.</summary>
    public long DaysPerYear => Months.Sum(month => (long)month.Days);

    /// <summary>What's wrong with this calendar, or null if it's usable.</summary>
    public string? Problem()
    {
        if (Months is null || Months.Count is < 1 or > MaxMonths)
        {
            return $"a calendar needs 1 to {MaxMonths} months";
        }

        if (Months.Any(m => string.IsNullOrWhiteSpace(m.Name) || m.Days is < 1 or > MaxMonthDays))
        {
            return $"every month needs a name and 1 to {MaxMonthDays:N0} days";
        }

        if (Weekdays is null || Weekdays.Count > MaxWeekdays
            || Weekdays.Any(string.IsNullOrWhiteSpace))
        {
            return $"weekdays need names (at most {MaxWeekdays})";
        }

        if (StartMonth < 0 || StartMonth >= Months.Count
            || StartDay < 1 || StartDay > Months[StartMonth].Days)
        {
            return "the starting date isn't in the calendar";
        }

        return StartWeekday < 0 || (Weekdays.Count > 0 && StartWeekday >= Weekdays.Count)
            || (Weekdays.Count == 0 && StartWeekday != 0)
            ? "the starting weekday isn't in the calendar"
            : null;
    }

    /// <summary>Calendars are equal when every part matches, including the lists.</summary>
    public bool Equals(Calendar? other)
    {
        return other is not null
            && Months.SequenceEqual(other.Months)
            && Weekdays.SequenceEqual(other.Weekdays)
            && FirstYear == other.FirstYear
            && Era == other.Era
            && StartMonth == other.StartMonth
            && StartDay == other.StartDay
            && StartWeekday == other.StartWeekday
            && Fit == other.Fit
            && MonthMoonId == other.MonthMoonId;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(Months.Count, Weekdays.Count, FirstYear, Era, StartMonth,
            StartDay, StartWeekday, Fit);
    }
}
