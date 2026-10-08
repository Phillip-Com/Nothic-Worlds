using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Tick marks for a stretch of time on a body (VISION.md LORE-03: the timeline strip's ruler).
/// It picks the finest unit that fits (hours, days, months, or years of the body's calendar;
/// plain day numbers without one) and labels each tick in the body's own terms.
/// </summary>
public static class TimeRuler
{
    // Step multiples tried for each unit, smallest first.
    private static readonly int[] _hourSteps = [1, 2, 3, 6, 12];
    private static readonly long[] _niceSteps = [1, 2, 5];

    /// <summary>
    /// At most <paramref name="maxTicks"/> ticks between two times (standard days), in order.
    /// </summary>
    public static List<RulerTick> Ticks(Body body, double fromDays, double toDays, int maxTicks)
    {
        if (!(toDays > fromDays) || maxTicks < 1 || !double.IsFinite(toDays - fromDays))
        {
            return [];
        }

        double dayDays = body.DayLengthHours / 24.0;
        double spanBodyDays = (toDays - fromDays) / dayDays;

        // Hours, while a body day is still wide enough to split.
        foreach (int hours in _hourSteps)
        {
            if (hours < body.DayLengthHours && (toDays - fromDays) * 24 / hours <= maxTicks)
            {
                return HourTicks(body, fromDays, toDays, hours);
            }
        }

        if (body.Calendar is not Calendar calendar)
        {
            long step = NiceStep(spanBodyDays, maxTicks);
            return DayTicks(body, fromDays, toDays, step, day => LocalTime.DayName(day + 1));
        }

        foreach (long days in new long[] { 1, 2, 5, 10 })
        {
            if (spanBodyDays / days <= maxTicks)
            {
                return DayTicks(body, fromDays, toDays, days, day =>
                {
                    CalendarDate date = CalendarMath.DateOf(calendar, day);
                    return $"{date.Day} {calendar.Months[date.Month].Name}";
                });
            }
        }

        double monthDays = calendar.AverageDaysPerYear / calendar.Months.Count;
        if (spanBodyDays / monthDays <= maxTicks)
        {
            return MonthTicks(body, calendar, fromDays, toDays);
        }

        long years = NiceStep(spanBodyDays / calendar.AverageDaysPerYear, maxTicks);
        return YearTicks(body, calendar, fromDays, toDays, years);
    }

    // The smallest of 1, 2, 5, 10, 20, 50, … that keeps a span within the tick limit.
    private static long NiceStep(double span, int maxTicks)
    {
        for (long scale = 1; scale < long.MaxValue / 10; scale *= 10)
        {
            foreach (long multiple in _niceSteps)
            {
                if (span / (multiple * scale) <= maxTicks)
                {
                    return multiple * scale;
                }
            }
        }

        return long.MaxValue / 10;
    }

    private static List<RulerTick> HourTicks(Body body, double fromDays, double toDays, int hours)
    {
        var ticks = new List<RulerTick>();
        for (double hour = Math.Ceiling(fromDays * 24 / hours) * hours; hour <= toDays * 24;
            hour += hours)
        {
            LocalTime local = BodyClock.LocalTimeOn(body, hour / 24);
            ticks.Add(new RulerTick(hour / 24, $"{local.Hour}:{local.Minute:00}"));
        }

        return ticks;
    }

    // Every `step`-th day (counting from day index 0, the day at time 0), labelled by index.
    private static List<RulerTick> DayTicks(
        Body body, double fromDays, double toDays, long step, Func<long, string> label)
    {
        double dayDays = body.DayLengthHours / 24.0;
        long first = (long)Math.Ceiling(fromDays / dayDays / step) * step;
        var ticks = new List<RulerTick>();
        for (long day = first; day * dayDays <= toDays; day += step)
        {
            ticks.Add(new RulerTick(BodyClock.TimeAt(body, day, 0), label(day)));
        }

        return ticks;
    }

    private static List<RulerTick> MonthTicks(
        Body body, Calendar calendar, double fromDays, double toDays)
    {
        CalendarDate start = CalendarMath.DateOf(calendar,
            (long)Math.Floor(fromDays * 24 / body.DayLengthHours));
        var ticks = new List<RulerTick>();
        (long year, int month) = (start.Year, start.Month);
        while (true)
        {
            double time = BodyClock.TimeAt(body,
                CalendarMath.DayIndexOf(calendar, year, month, 1), 0);
            if (time > toDays)
            {
                return ticks;
            }

            if (time >= fromDays)
            {
                ticks.Add(new RulerTick(time, $"{calendar.Months[month].Name} {year}"));
            }

            if (++month == calendar.Months.Count)
            {
                (year, month) = (year + 1, 0);
            }
        }
    }

    // Every `step`-th year (whole multiples of `step`), each at its first day.
    private static List<RulerTick> YearTicks(
        Body body, Calendar calendar, double fromDays, double toDays, long step)
    {
        long fromYear = CalendarMath.DateOf(calendar,
            (long)Math.Floor(fromDays * 24 / body.DayLengthHours)).Year;
        long year = (long)Math.Ceiling(fromYear / (double)step) * step;
        var ticks = new List<RulerTick>();
        while (true)
        {
            double time = BodyClock.TimeAt(body,
                CalendarMath.DayIndexOf(calendar, year, 0, 1), 0);
            if (time > toDays)
            {
                return ticks;
            }

            if (time >= fromDays)
            {
                ticks.Add(new RulerTick(time, $"{year}"));
            }

            year += step;
        }
    }
}
