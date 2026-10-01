using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Converts between a body's day count and dates in its calendar (VISION.md CAL-01). Day index
/// 0 is the day at the world's time 0, which is the calendar's start date. Negative indexes
/// count back before it.
/// </summary>
public static class CalendarMath
{
    /// <summary>The date of a day in the calendar.</summary>
    public static CalendarDate DateOf(Calendar calendar, long dayIndex)
    {
        long daysPerYear = calendar.DaysPerYear;
        long fromYearStart = DaysBeforeMonth(calendar, calendar.StartMonth)
            + calendar.StartDay - 1 + dayIndex;
        long yearOffset = FloorDivide(fromYearStart, daysPerYear);
        long dayOfYear = fromYearStart - yearOffset * daysPerYear;

        int month = 0;
        while (dayOfYear >= calendar.Months[month].Days)
        {
            dayOfYear -= calendar.Months[month].Days;
            month++;
        }

        int? weekday = calendar.Weekdays.Count == 0
            ? null
            : (int)Modulo(calendar.StartWeekday + dayIndex, calendar.Weekdays.Count);
        return new CalendarDate(calendar.FirstYear + yearOffset, month, (int)dayOfYear + 1,
            weekday);
    }

    /// <summary>
    /// The day index of a date (the weekday is ignored). The day may run past its month's
    /// length; it simply carries on into the following months.
    /// </summary>
    public static long DayIndexOf(Calendar calendar, long year, int month, int day)
    {
        long startOffset = DaysBeforeMonth(calendar, calendar.StartMonth) + calendar.StartDay - 1;
        long target = (year - calendar.FirstYear) * calendar.DaysPerYear
            + DaysBeforeMonth(calendar, month) + day - 1;
        return target - startOffset;
    }

    /// <summary>
    /// A date as words, e.g. "14 Highsun 1203 of the Third Age, Moonday" (no weekday part for a
    /// calendar without weeks).
    /// </summary>
    public static string Format(Calendar calendar, CalendarDate date)
    {
        string era = string.IsNullOrWhiteSpace(calendar.Era) ? "" : $" {calendar.Era.Trim()}";
        string text = $"{date.Day} {calendar.Months[date.Month].Name} {date.Year}{era}";
        return date.Weekday is int weekday ? $"{text}, {calendar.Weekdays[weekday]}" : text;
    }

    private static long DaysBeforeMonth(Calendar calendar, int month)
    {
        long days = 0;
        for (int i = 0; i < month; i++)
        {
            days += calendar.Months[i].Days;
        }

        return days;
    }

    private static long FloorDivide(long value, long divisor)
    {
        long quotient = value / divisor;
        return value % divisor < 0 ? quotient - 1 : quotient;
    }

    private static long Modulo(long value, long divisor)
    {
        long remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }
}
