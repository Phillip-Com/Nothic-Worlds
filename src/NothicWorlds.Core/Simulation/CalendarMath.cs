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
        // Days are counted from the start of the calendar's first year.
        long fromFirstYear = StartOffset(calendar) + dayIndex;
        long year = calendar.FirstYear
            + (long)Math.Floor(fromFirstYear / calendar.AverageDaysPerYear);
        while (DaysBeforeYear(calendar, year) > fromFirstYear)
        {
            year--;
        }

        while (DaysBeforeYear(calendar, year + 1) <= fromFirstYear)
        {
            year++;
        }

        long dayOfYear = fromFirstYear - DaysBeforeYear(calendar, year);
        int month = 0;
        while (dayOfYear >= calendar.DaysInMonth(year, month))
        {
            dayOfYear -= calendar.DaysInMonth(year, month);
            month++;
        }

        int? weekday = calendar.Weekdays.Count == 0
            ? null
            : (int)Modulo(calendar.StartWeekday + dayIndex, calendar.Weekdays.Count);
        return new CalendarDate(year, month, (int)dayOfYear + 1, weekday);
    }

    /// <summary>
    /// The day index of a date (the weekday is ignored). The day may run past its month's
    /// length; it simply carries on into the following months.
    /// </summary>
    public static long DayIndexOf(Calendar calendar, long year, int month, int day)
    {
        long target = DaysBeforeYear(calendar, year) + DaysBeforeMonth(calendar, year, month)
            + day - 1;
        return target - StartOffset(calendar);
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

    // How far the start date is into the calendar's first year, in days.
    private static long StartOffset(Calendar calendar) =>
        DaysBeforeMonth(calendar, calendar.FirstYear, calendar.StartMonth)
        + calendar.StartDay - 1;

    private static long DaysBeforeMonth(Calendar calendar, long year, int month)
    {
        long days = 0;
        for (int i = 0; i < month; i++)
        {
            days += calendar.DaysInMonth(year, i);
        }

        return days;
    }

    // Days from the start of the calendar's first year to the start of `year` (negative before
    // it). Leap years are counted, not stepped through, so even far-off years are quick.
    private static long DaysBeforeYear(Calendar calendar, long year)
    {
        long days = (year - calendar.FirstYear) * calendar.DaysPerYear;
        if (calendar.Leap is LeapRule leap)
        {
            days += (LeapYearsBefore(leap, year) - LeapYearsBefore(leap, calendar.FirstYear))
                * leap.Days;
        }

        return days;
    }

    // How many leap years are numbered from 0 up to (not including) `year`, or minus how many
    // from `year` up to 0 when it's negative.
    private static long LeapYearsBefore(LeapRule leap, long year)
    {
        long count = MultiplesBefore(year, leap.Every);
        if (leap.Except is int except)
        {
            count -= MultiplesBefore(year, except);
        }

        if (leap.ExceptAgain is int again)
        {
            count += MultiplesBefore(year, again);
        }

        return count;
    }

    // How many multiples of `step` lie in [0, year), or minus how many in [year, 0).
    private static long MultiplesBefore(long year, long step) => CeilingDivide(year, step);

    private static long CeilingDivide(long value, long divisor) =>
        -FloorDivide(-value, divisor);

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
