using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A body's own clock (VISION.md SIM-02): how far it has spun, and what day and time it is
/// there, counted in its own day length. Until calendars exist (CAL-01), times are shown as
/// "Day 1,204, 14:30" in the selected planet's days (owner decision).
/// </summary>
public static class BodyClock
{
    /// <summary>
    /// How far the body has turned on its axis at <paramref name="timeDays"/>, in degrees
    /// (0 to 360): one full turn per day, eastward.
    /// </summary>
    public static double SpinDegrees(Body body, double timeDays)
    {
        double turns = timeDays * 24.0 / body.DayLengthHours;
        double fraction = turns - Math.Floor(turns);
        return fraction * 360.0;
    }

    /// <summary>
    /// How long a year is for a body, in standard days: one trip of the body (or, for a moon,
    /// its planet) around its star. In a planet-centered system, where the star circles the
    /// planet instead, it's the star's trip. Falls back to 365.25 days with no star in either
    /// place.
    /// </summary>
    public static double YearDays(IReadOnlyList<Body> bodies, Body body)
    {
        return YearOrbitOf(bodies, body)?.Orbit!.PeriodDays ?? 365.25;
    }

    /// <summary>
    /// The body whose orbit makes <paramref name="body"/>'s year (see <see cref="YearDays"/>):
    /// the body itself or its planet, circling a star, or (in a planet-centered system) the
    /// star circling them. Null if there's no star in either place.
    /// </summary>
    public static Body? YearOrbitOf(IReadOnlyList<Body> bodies, Body body)
    {
        var byId = bodies.ToDictionary(b => b.Id);
        var seen = new HashSet<Guid>();
        for (Body current = body; seen.Add(current.Id);)
        {
            // A star circling this body (or its planet) sets the year from the other side.
            Body? circlingStar = bodies.FirstOrDefault(b =>
                b.GivesLight && b.Orbit?.ParentId == current.Id);
            if (circlingStar is not null)
            {
                return circlingStar;
            }

            if (current.Orbit is not Orbit orbit
                || !byId.TryGetValue(orbit.ParentId, out Body? parent))
            {
                break;
            }

            if (parent.GivesLight)
            {
                return current;
            }

            current = parent;
        }

        return null;
    }

    /// <summary>
    /// The date and time on the body as words: in its calendar if it has one ("14 Highsun 1203,
    /// Moonday, 14:30"), otherwise "Day 1,204, 14:30" (VISION.md CAL-01, SIM-02).
    /// </summary>
    public static string Describe(Body body, double timeDays)
    {
        // A world tree's "day" is one slow turn (its realms' year), so count turns and days.
        if (body.Kind == BodyKind.WorldTree)
        {
            double turnDays = body.DayLengthHours / 24;
            double turns = Math.Floor(timeDays / turnDays);
            double day = Math.Floor(timeDays - turns * turnDays);
            return $"Turn {turns + 1:N0}, day {day + 1:N0}";
        }

        LocalTime local = LocalTimeOn(body, timeDays);
        if (body.Calendar is not Calendar calendar)
        {
            return local.ToString();
        }

        CalendarDate date = CalendarMath.DateOf(calendar, local.Day - 1);
        return $"{CalendarMath.Format(calendar, date)}, {local.Hour}:{local.Minute:00}";
    }

    /// <summary>
    /// The world time (standard days) at a given day and hour on the body. Day index 0 is the
    /// day at time 0 (day 1, or the calendar's start date); hours are standard hours into it.
    /// </summary>
    public static double TimeAt(Body body, long dayIndex, double hours)
    {
        return (dayIndex * body.DayLengthHours + hours) / 24.0;
    }

    /// <summary>
    /// The day and time of day on the body at <paramref name="timeDays"/>. Time 0 is the start
    /// of day 1. Hours are standard hours since the body's day began, so a 30-hour day runs
    /// from 0:00 to 29:59.
    /// </summary>
    public static LocalTime LocalTimeOn(Body body, double timeDays)
    {
        double hours = timeDays * 24.0;
        double days = Math.Floor(hours / body.DayLengthHours);
        double hoursIntoDay = hours - days * body.DayLengthHours;

        // Rounding to the minute could reach the day's length exactly; that's the next day.
        long totalMinutes = (long)Math.Floor(hoursIntoDay * 60.0 + 1e-9);
        if (totalMinutes >= (long)Math.Round(body.DayLengthHours * 60.0))
        {
            totalMinutes = 0;
            days++;
        }

        return new LocalTime((long)days + 1, (int)(totalMinutes / 60), (int)(totalMinutes % 60));
    }
}
