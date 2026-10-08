using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// One month of a body's calendar laid out as a page of a wall calendar (VISION.md CAL-05): its
/// name, how many days it has this year, the day index of its first day, and which weekday
/// column that day falls in. Moving to the next or previous month crosses years as needed.
/// </summary>
/// <param name="Year">The year number.</param>
/// <param name="Month">The month (0 is the first).</param>
/// <param name="Name">The month's name.</param>
/// <param name="Days">How many days it has this year (leap days included).</param>
/// <param name="FirstDayIndex">
/// The day index of its first day (see <see cref="CalendarMath"/>: 0 is the calendar's start
/// date).
/// </param>
/// <param name="FirstWeekday">
/// The weekday column its first day falls in (0 is the first weekday); 0 for a calendar without
/// weeks.
/// </param>
public sealed record MonthPage(long Year, int Month, string Name, int Days, long FirstDayIndex,
    int FirstWeekday)
{
    /// <summary>The page for a month of a year.</summary>
    public static MonthPage Of(Calendar calendar, long year, int month)
    {
        long first = CalendarMath.DayIndexOf(calendar, year, month, 1);
        int weekday = CalendarMath.DateOf(calendar, first).Weekday ?? 0;
        return new MonthPage(year, month, calendar.Months[month].Name,
            calendar.DaysInMonth(year, month), first, weekday);
    }

    /// <summary>The page for the month a day falls in.</summary>
    public static MonthPage Containing(Calendar calendar, long dayIndex)
    {
        CalendarDate date = CalendarMath.DateOf(calendar, dayIndex);
        return Of(calendar, date.Year, date.Month);
    }

    /// <summary>
    /// The page <paramref name="months"/> months later (earlier when negative), into other
    /// years as needed.
    /// </summary>
    public MonthPage Moved(Calendar calendar, int months)
    {
        int count = calendar.Months.Count;
        long total = Year * count + Month + months;
        long year = FloorDivide(total, count);
        return Of(calendar, year, (int)(total - year * count));
    }

    /// <summary>Whether a day falls on this page.</summary>
    public bool Holds(long dayIndex) =>
        dayIndex >= FirstDayIndex && dayIndex < FirstDayIndex + Days;

    private static long FloorDivide(long value, long divisor)
    {
        long quotient = value / divisor;
        return value % divisor < 0 ? quotient - 1 : quotient;
    }
}
