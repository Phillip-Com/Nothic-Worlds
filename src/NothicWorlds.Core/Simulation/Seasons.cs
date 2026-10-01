using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Solstices, equinoxes, and seasons (VISION.md CAL-03), worked out from the simulation: how
/// high the body's star stands over its equator (the star's declination), given the body's
/// tilted axis and where it is around its orbit. Northern summer peaks when the north pole
/// leans most toward the star. Deterministic, like everything in the simulation.
/// </summary>
public static class Seasons
{
    // Below this axial tilt there are no real seasons to find.
    private const double MinimumTiltDegrees = 0.01;

    // Points checked per year when searching for events; then each one is refined exactly.
    private const int SamplesPerYear = 720;

    /// <summary>
    /// The star a body's seasons come from: the nearest star up its chain of parents, or (in a
    /// planet-centered system) a star circling it or its planet. Null if there's none.
    /// </summary>
    public static Body? StarFor(IReadOnlyList<Body> bodies, Body body)
    {
        var byId = bodies.ToDictionary(b => b.Id);
        var seen = new HashSet<Guid>();
        for (Body? current = body; current is not null && seen.Add(current.Id);)
        {
            if (current.Id != body.Id && current.Kind == BodyKind.Star)
            {
                return current;
            }

            Body? circling = bodies.FirstOrDefault(b =>
                b.Kind == BodyKind.Star && b.Orbit?.ParentId == current.Id);
            if (circling is not null)
            {
                return circling;
            }

            current = current.Orbit is Orbit orbit ? byId.GetValueOrDefault(orbit.ParentId) : null;
        }

        return null;
    }

    /// <summary>
    /// How high the star stands over the body's equator at a time, in degrees: positive when
    /// it's over the northern hemisphere. Its extremes are the solstices; zero is an equinox.
    /// </summary>
    public static double StarDeclinationDegrees(
        IReadOnlyList<Body> bodies, Body body, Body star, double timeDays)
    {
        Dictionary<Guid, Vector3D> positions = SystemPositions.At(bodies, timeDays);
        Vector3D toStar = positions[star.Id] - positions[body.Id];
        double sine = toStar.Dot(BodyOrientation.NorthPole(body)) / toStar.Length;
        return double.RadiansToDegrees(Math.Asin(Math.Clamp(sine, -1, 1)));
    }

    /// <summary>
    /// Every solstice and equinox between two times, in order. Empty for a body without a star
    /// or without tilt.
    /// </summary>
    public static List<SeasonEvent> EventsBetween(
        IReadOnlyList<Body> bodies, Body body, double fromDays, double toDays)
    {
        var events = new List<SeasonEvent>();
        if (StarFor(bodies, body) is not Body star || body.AxialTiltDegrees < MinimumTiltDegrees
            || toDays <= fromDays)
        {
            return events;
        }

        double step = BodyClock.YearDays(bodies, body) / SamplesPerYear;
        double Declination(double t) => StarDeclinationDegrees(bodies, body, star, t);

        double previousTime = fromDays;
        double previous = Declination(previousTime);
        double previousSlope = Declination(previousTime + step / 100) - previous;
        for (double time = fromDays + step; previousTime < toDays; time += step)
        {
            double value = Declination(time);
            double slope = value - previous;
            if ((previous < 0) != (value < 0))
            {
                double crossing = Bisect(Declination, previousTime, time, previous < 0);
                events.Add(new SeasonEvent(previous < 0
                    ? SeasonEventKind.NorthernSpringEquinox
                    : SeasonEventKind.NorthernAutumnEquinox, crossing));
            }

            if ((previousSlope > 0) != (slope > 0) && previousSlope != 0)
            {
                bool peak = previousSlope > 0;
                double extreme = Extreme(Declination, previousTime - step, time, peak);
                events.Add(new SeasonEvent(peak
                    ? SeasonEventKind.NorthernSummerSolstice
                    : SeasonEventKind.NorthernWinterSolstice, extreme));
            }

            previousTime = time;
            previous = value;
            previousSlope = slope;
        }

        return [.. events.Where(e => e.TimeDays >= fromDays && e.TimeDays < toDays)
            .OrderBy(e => e.TimeDays)];
    }

    /// <summary>
    /// The next solstice or equinox after a time, or null if there are no seasons.
    /// </summary>
    public static SeasonEvent? NextEvent(IReadOnlyList<Body> bodies, Body body, double timeDays)
    {
        double year = BodyClock.YearDays(bodies, body);
        return EventsBetween(bodies, body, timeDays, timeDays + year * 1.01)
            .Cast<SeasonEvent?>()
            .FirstOrDefault();
    }

    /// <summary>
    /// The season in each hemisphere at a time (from the most recent solstice or equinox), or
    /// null if the body has no seasons.
    /// </summary>
    public static (Season Northern, Season Southern)? SeasonAt(
        IReadOnlyList<Body> bodies, Body body, double timeDays)
    {
        double year = BodyClock.YearDays(bodies, body);
        List<SeasonEvent> recent =
            EventsBetween(bodies, body, timeDays - year * 1.01, timeDays + 1e-9);
        if (recent.Count == 0)
        {
            return null;
        }

        return SeasonsAfter(recent[^1].Kind);
    }

    /// <summary>The season each hemisphere enters at a solstice or equinox.</summary>
    public static (Season Northern, Season Southern) SeasonsAfter(SeasonEventKind kind)
    {
        Season northern = kind switch
        {
            SeasonEventKind.NorthernSpringEquinox => Season.Spring,
            SeasonEventKind.NorthernSummerSolstice => Season.Summer,
            SeasonEventKind.NorthernAutumnEquinox => Season.Autumn,
            _ => Season.Winter,
        };
        Season southern = (Season)(((int)northern + 2) % 4);
        return (northern, southern);
    }

    // Narrows down where a smooth function crosses zero between two times.
    private static double Bisect(Func<double, double> f, double low, double high, bool rising)
    {
        for (int i = 0; i < 60; i++)
        {
            double middle = (low + high) / 2;
            if ((f(middle) < 0) == rising)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return (low + high) / 2;
    }

    // Narrows down where a smooth function peaks (or bottoms out) between two times
    // (golden-section search).
    private static double Extreme(Func<double, double> f, double low, double high, bool peak)
    {
        double ratio = (Math.Sqrt(5) - 1) / 2;
        double a = high - ratio * (high - low);
        double b = low + ratio * (high - low);
        double fa = f(a);
        double fb = f(b);
        for (int i = 0; i < 80; i++)
        {
            if ((fa > fb) == peak)
            {
                high = b;
                b = a;
                fb = fa;
                a = high - ratio * (high - low);
                fa = f(a);
            }
            else
            {
                low = a;
                a = b;
                fa = fb;
                b = low + ratio * (high - low);
                fb = f(b);
            }
        }

        return (low + high) / 2;
    }
}
