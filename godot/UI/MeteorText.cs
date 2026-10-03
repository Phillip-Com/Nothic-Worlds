using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.UI;

/// <summary>How meteor showers are put into words (VISION.md EVT-02), in one place.</summary>
public static class MeteorText
{
    /// <summary>"Meteor shower from Halley" (or "from a comet" if it's gone).</summary>
    public static string Title(MeteorShower shower, IReadOnlyList<Body> bodies)
    {
        string? comet = bodies.FirstOrDefault(b => b.Id == shower.CometId)?.Name;
        return comet is null ? "Meteor shower from a comet" : $"Meteor shower from {comet}";
    }

    /// <summary>"Up to 50 meteors an hour · 19 days" (in the body's own days).</summary>
    public static string Details(MeteorShower shower, Body body)
    {
        long days = Math.Max(1,
            (long)Math.Round((shower.EndDays - shower.StartDays) * 24.0 / body.DayLengthHours));
        return $"Up to {PerHour(shower.PeakPerHour)} meteors an hour · " +
            (days == 1 ? "1 day" : $"{days:N0} days");
    }

    /// <summary>
    /// The time bar's line while a shower is under way: "Meteor shower from Halley: about 30 an
    /// hour, peak in 2 days" (in the body's own days).
    /// </summary>
    public static string UnderWay(
        MeteorShower shower, Body body, IReadOnlyList<Body> bodies, double now)
    {
        string howFar = SeasonText.HowFar(body, now, shower.PeakDays);
        string peak = howFar == "today" ? "peaking today"
            : howFar.EndsWith(" ago", StringComparison.Ordinal) ? $"peaked {howFar}"
            : $"peak {howFar}";
        return $"{Title(shower, bodies)}: about {PerHour(shower.PerHourAt(now))} an hour, {peak}";
    }

    private static string PerHour(double perHour) => perHour < 1 ? "1" : $"{perHour:N0}";
}
