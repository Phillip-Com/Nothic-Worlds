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
