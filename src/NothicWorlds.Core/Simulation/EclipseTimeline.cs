using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A body's eclipses over a stretch of time, worked out once (VISION.md EVT-01): from half a
/// year before a time to a year and a half after it (a year being the body's own). The app
/// asks it what's coming every frame while time runs; it only needs working out again when time
/// leaves the stretch or the world changes.
/// </summary>
public sealed class EclipseTimeline
{
    private readonly List<Eclipse> _eclipses;
    private readonly double _yearDays;

    private EclipseTimeline(List<Eclipse> eclipses, double yearDays, double fromDays,
        double toDays)
    {
        _eclipses = eclipses;
        _yearDays = yearDays;
        FromDays = fromDays;
        ToDays = toDays;
    }

    /// <summary>The start of the stretch covered, in standard days.</summary>
    public double FromDays { get; }

    /// <summary>The end of the stretch covered.</summary>
    public double ToDays { get; }

    /// <summary>The eclipses peaking in the stretch, in order.</summary>
    public IReadOnlyList<Eclipse> Eclipses => _eclipses;

    /// <summary>Works out the eclipses around a time.</summary>
    /// <exception cref="ArgumentException">The bodies' orbits are invalid.</exception>
    public static EclipseTimeline Around(IReadOnlyList<Body> bodies, Body body, double aroundDays)
    {
        double year = BodyClock.YearDays(bodies, body);
        double from = aroundDays - year * 0.5;
        double to = aroundDays + year * 1.5;
        return new EclipseTimeline(
            Simulation.Eclipses.Between(bodies, body, from, to), year, from, to);
    }

    /// <summary>
    /// True if the stretch can answer for <paramref name="timeDays"/>: a quarter year before it
    /// (for the last eclipse of each moon) and a whole year after it.
    /// </summary>
    public bool Covers(double timeDays)
    {
        return timeDays - _yearDays * 0.25 >= FromDays && timeDays + _yearDays <= ToDays;
    }

    /// <summary>
    /// The eclipses not yet over at a time (including one under way), up to a year ahead.
    /// </summary>
    public IReadOnlyList<Eclipse> Upcoming(double timeDays)
    {
        return [.. _eclipses.Where(e =>
            e.EndDays >= timeDays && e.PeakDays <= timeDays + _yearDays)];
    }
}
