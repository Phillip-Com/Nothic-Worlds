using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A body's solstices and equinoxes over a stretch of time, worked out once (VISION.md CAL-03).
/// The time bar asks it for the season and the next event every frame while time runs; it only
/// needs working out again when time leaves the stretch or the world changes.
/// </summary>
public sealed class SeasonTimeline
{
    private readonly List<SeasonEvent> _events;

    private readonly double _yearDays;

    private SeasonTimeline(List<SeasonEvent> events, double yearDays, double fromDays,
        double toDays)
    {
        _events = events;
        _yearDays = yearDays;
        FromDays = fromDays;
        ToDays = toDays;
    }

    /// <summary>The start of the stretch covered, in standard days.</summary>
    public double FromDays { get; }

    /// <summary>The end of the stretch covered.</summary>
    public double ToDays { get; }

    /// <summary>The solstices and equinoxes in the stretch, in order.</summary>
    public IReadOnlyList<SeasonEvent> Events => _events;

    /// <summary>
    /// Works out the events from two years before <paramref name="aroundDays"/> to two years
    /// after it, so time can run a year either way before they need working out again
    /// (a year being the body's own; see <see cref="BodyClock.YearDays"/>).
    /// </summary>
    public static SeasonTimeline Around(IReadOnlyList<Body> bodies, Body body, double aroundDays)
    {
        double year = BodyClock.YearDays(bodies, body);
        double from = aroundDays - year * 2.02;
        double to = aroundDays + year * 2.02;
        return new SeasonTimeline(
            Seasons.EventsBetween(bodies, body, from, to), year, from, to);
    }

    /// <summary>
    /// True if the stretch can answer for <paramref name="timeDays"/>: a whole year of events
    /// before it and after it.
    /// </summary>
    public bool Covers(double timeDays)
    {
        double margin = _yearDays * 1.01;
        return timeDays - margin >= FromDays && timeDays + margin <= ToDays;
    }

    /// <summary>
    /// The season in each hemisphere (from the most recent event), or null if there are no
    /// seasons.
    /// </summary>
    public (Season Northern, Season Southern)? SeasonAt(double timeDays)
    {
        SeasonEvent? latest = null;
        foreach (SeasonEvent e in _events)
        {
            if (e.TimeDays > timeDays)
            {
                break;
            }

            latest = e;
        }

        return latest is SeasonEvent found ? Seasons.SeasonsAfter(found.Kind) : null;
    }

    /// <summary>The next event after a time, or null if there isn't one in the stretch.</summary>
    public SeasonEvent? NextEvent(double timeDays)
    {
        foreach (SeasonEvent e in _events)
        {
            if (e.TimeDays > timeDays)
            {
                return e;
            }
        }

        return null;
    }

    /// <summary>
    /// The next <paramref name="count"/> events after a time (four of them cover a year).
    /// </summary>
    public IReadOnlyList<SeasonEvent> Upcoming(double timeDays, int count)
    {
        return [.. _events.Where(e => e.TimeDays > timeDays).Take(count)];
    }
}
