using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Keeps the world fitted to its calendars (VISION.md CAL-02; owner's choice: a lasting switch
/// per calendar). A fitted calendar's year lasts exactly one calendar year, so its dates and
/// seasons never drift apart. The user picks what changes:
/// <list type="bullet">
/// <item><see cref="CalendarFit.YearLength"/>: the period of the orbit that makes the year (the
/// body's own, its planet's for a moon, or a star's circling it).</item>
/// <item><see cref="CalendarFit.DayLength"/>: how long the body takes to spin.</item>
/// </list>
/// A calendar can also name a <see cref="Calendar.MonthMoonId">month moon</see>: a moon of the
/// body whose orbit is set so it goes from new moon to new moon once per average month.
/// </summary>
/// <remarks>
/// Fits are applied in a fixed order (years, then days, then months, each in body order), since
/// each step uses the one before: deterministic, like all simulation.
/// </remarks>
public static class CalendarFitting
{
    /// <summary>
    /// Sets every fitted value in the world (orbit periods and day lengths) from the calendars.
    /// A fit that can't be met (no star, or a value out of range) is skipped; see
    /// <see cref="Problem"/>. Returns true if anything changed.
    /// </summary>
    public static bool Apply(IReadOnlyList<Body> bodies)
    {
        return Apply(bodies, failures: []);
    }

    // Applies the fits, noting each one that can't be met.
    private static bool Apply(IReadOnlyList<Body> bodies, List<string> failures)
    {
        bool changed = false;
        var fittedOrbits = new HashSet<Guid>();
        foreach (Body body in Fitted(bodies, CalendarFit.YearLength))
        {
            if (BodyClock.YearOrbitOf(bodies, body) is Body owner && fittedOrbits.Add(owner.Id))
            {
                double period = body.Calendar!.AverageDaysPerYear * body.DayLengthHours / 24.0;
                changed |= SetPeriod(owner, period, failures);
            }
        }

        foreach (Body body in Fitted(bodies, CalendarFit.DayLength))
        {
            if (BodyClock.YearOrbitOf(bodies, body) is not null)
            {
                double hours = BodyClock.YearDays(bodies, body) * 24.0
                    / body.Calendar!.AverageDaysPerYear;
                changed |= SetDayLength(body, hours, failures);
            }
        }

        foreach (Body body in bodies.Where(b => b.Calendar?.MonthMoonId is not null))
        {
            if (MonthMoon(bodies, body) is not Body moon)
            {
                continue;
            }

            if (MoonPeriodDays(bodies, body, moon) is double period)
            {
                changed |= SetPeriod(moon, period, failures);
            }
            else
            {
                failures.Add($"a month that long can't be one cycle of {moon.Name} (it would " +
                    "have to stand still against the stars)");
            }
        }

        return changed;
    }

    /// <summary>
    /// What's wrong with giving <paramref name="body"/> this <paramref name="calendar"/>'s fit,
    /// or null if it can be met. Checks that there's a year to fit, that no other calendar
    /// already sets the same orbit, that the month moon is a moon of this body, and that the
    /// fitted values stay in range.
    /// </summary>
    public static string? Problem(IReadOnlyList<Body> bodies, Body body, Calendar calendar)
    {
        if (calendar.Fit != CalendarFit.None && BodyClock.YearOrbitOf(bodies, body) is null)
        {
            return $"fitting the year needs a star for {body.Name} to circle";
        }

        if (calendar.Fit == CalendarFit.YearLength
            && BodyClock.YearOrbitOf(bodies, body) is Body owner
            && Fitted(bodies, CalendarFit.YearLength).FirstOrDefault(other =>
                other.Id != body.Id && BodyClock.YearOrbitOf(bodies, other)?.Id == owner.Id)
                is Body rival)
        {
            return $"{rival.Name}'s calendar already sets {owner.Name}'s orbit; fit the day " +
                "length instead";
        }

        if (calendar.MonthMoonId is Guid moonId && !IsMoonOf(bodies, moonId, body))
        {
            return $"the month moon must be a moon circling {body.Name}";
        }

        var failures = new List<string>();
        Trial(bodies, body, calendar, failures, out _);
        return failures.FirstOrDefault();
    }

    /// <summary>
    /// What fitting <paramref name="body"/> with <paramref name="calendar"/> would change, for
    /// showing before it's applied: each fitted value that would differ from now.
    /// </summary>
    public static IReadOnlyList<FitChange> Preview(
        IReadOnlyList<Body> bodies, Body body, Calendar calendar)
    {
        Trial(bodies, body, calendar, failures: [], out List<FitChange> changes);
        return changes;
    }

    /// <summary>
    /// Which calendars set <paramref name="body"/>'s day length and orbit period (null where
    /// none does), so the System panel can lock those fields and say why.
    /// </summary>
    public static (Body? DayLengthBy, Body? PeriodBy) FittedBy(
        IReadOnlyList<Body> bodies, Body body)
    {
        Body? dayBy = body.Calendar?.Fit == CalendarFit.DayLength
            && BodyClock.YearOrbitOf(bodies, body) is not null ? body : null;
        Body? periodBy = Fitted(bodies, CalendarFit.YearLength)
            .FirstOrDefault(b => BodyClock.YearOrbitOf(bodies, b)?.Id == body.Id)
            ?? bodies.FirstOrDefault(b => b.Calendar?.MonthMoonId == body.Id
                && MonthMoon(bodies, b) is not null);
        return (dayBy, periodBy);
    }

    // The bodies whose calendars fit the world this way, in body order.
    private static IEnumerable<Body> Fitted(IReadOnlyList<Body> bodies, CalendarFit fit) =>
        bodies.Where(b => b.Calendar?.Fit == fit);

    // The calendar's month moon, if it's (still) a moon circling the body.
    private static Body? MonthMoon(IReadOnlyList<Body> bodies, Body body)
    {
        return body.Calendar?.MonthMoonId is Guid id && IsMoonOf(bodies, id, body)
            ? bodies.First(b => b.Id == id)
            : null;
    }

    private static bool IsMoonOf(IReadOnlyList<Body> bodies, Guid moonId, Body body) =>
        bodies.FirstOrDefault(b => b.Id == moonId) is { Kind: BodyKind.Moon, Orbit: Orbit orbit }
        && orbit.ParentId == body.Id;

    // The moon's orbital period (standard days) that makes new moon to new moon last one
    // average month of the body's calendar, or null if no period can. Seen from the body, the
    // star drifts once around the sky each year, so the moon must gain one extra lap on it
    // (or lose one, if it orbits the other way round).
    private static double? MoonPeriodDays(IReadOnlyList<Body> bodies, Body body, Body moon)
    {
        if (BodyClock.YearOrbitOf(bodies, body) is not Body owner || moon.Orbit is null)
        {
            return null;
        }

        Calendar calendar = body.Calendar!;
        double monthDays = calendar.AverageDaysPerYear / calendar.Months.Count
            * body.DayLengthHours / 24.0;
        double yearDays = owner.Orbit!.PeriodDays;
        bool sameWayRound = moon.Orbit.TiltDegrees > 90 == owner.Orbit.TiltDegrees > 90;
        double perDay = sameWayRound
            ? 1 / monthDays + 1 / yearDays
            : 1 / monthDays - 1 / yearDays;
        return perDay > 0 ? 1 / perDay : null;
    }

    // A copy of the bodies with the calendar applied and every fit worked out, plus what
    // changed against the originals.
    private static List<Body> Trial(IReadOnlyList<Body> bodies, Body body, Calendar calendar,
        List<string> failures, out List<FitChange> changes)
    {
        List<Body> trial = [.. bodies.Select(b => b.Clone())];
        trial.First(b => b.Id == body.Id).Calendar = calendar;
        Apply(trial, failures);
        changes = [];
        for (int i = 0; i < bodies.Count; i++)
        {
            Body before = bodies[i];
            Body after = trial[i];
            if (before.DayLengthHours != after.DayLengthHours)
            {
                changes.Add(new FitChange(before.Id, before.Name, FitTarget.DayLength,
                    before.DayLengthHours, after.DayLengthHours));
            }

            if (before.Orbit?.PeriodDays is double period
                && after.Orbit?.PeriodDays is double newPeriod && period != newPeriod)
            {
                changes.Add(new FitChange(
                    before.Id, before.Name, FitTarget.OrbitPeriod, period, newPeriod));
            }
        }

        return trial;
    }

    private static bool SetPeriod(Body body, double periodDays, List<string> failures)
    {
        if (body.Orbit is not Orbit orbit || orbit.PeriodDays == periodDays)
        {
            return false;
        }

        if ((orbit with { PeriodDays = periodDays }).Problem() is string problem)
        {
            failures.Add($"{body.Name}'s orbit would take {periodDays:G4} days, but {problem}");
            return false;
        }

        body.Orbit = orbit with { PeriodDays = periodDays };
        return true;
    }

    private static bool SetDayLength(Body body, double hours, List<string> failures)
    {
        if (body.DayLengthHours == hours)
        {
            return false;
        }

        if (!double.IsFinite(hours) || hours <= 0 || hours > Body.MaxDayLengthHours)
        {
            failures.Add($"{body.Name}'s day would last {hours:G4} hours, more than the " +
                $"{Body.MaxDayLengthHours:G} allowed");
            return false;
        }

        body.DayLengthHours = hours;
        return true;
    }
}
