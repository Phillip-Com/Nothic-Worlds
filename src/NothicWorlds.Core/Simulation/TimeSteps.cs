using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Steps the world clock by hours, days, months, or years measured on one body (VISION.md
/// CAL-05), as a calendar's arrows do: a month or year later is the same day of the month and
/// the same time of day, in its calendar.
/// </summary>
public static class TimeSteps
{
    /// <summary>
    /// The time <paramref name="count"/> steps of <paramref name="unit"/> after
    /// <paramref name="timeDays"/> (before, when negative), on <paramref name="body"/>. Null for
    /// months on a body without a calendar, which has none.
    /// </summary>
    public static double? Apply(IReadOnlyList<Body> bodies, Body body, double timeDays,
        TimeUnit unit, int count)
    {
        switch (unit)
        {
            case TimeUnit.Hour:
                return timeDays + count / 24.0;
            case TimeUnit.Day:
                return timeDays + count * body.DayLengthHours / 24.0;
        }

        if (body.Calendar is not Calendar calendar)
        {
            return unit == TimeUnit.Year
                ? timeDays + count * BodyClock.YearDays(bodies, body)
                : null;
        }

        // From the start of today, by whole calendar days, keeping the time of day.
        long today = BodyClock.LocalTimeOn(body, timeDays).Day - 1;
        double intoDay = timeDays - BodyClock.TimeAt(body, today, 0);
        long target = unit == TimeUnit.Month
            ? CalendarMath.AddMonths(calendar, today, count)
            : CalendarMath.AddYears(calendar, today, count);
        return BodyClock.TimeAt(body, target, 0) + intoDay;
    }
}
