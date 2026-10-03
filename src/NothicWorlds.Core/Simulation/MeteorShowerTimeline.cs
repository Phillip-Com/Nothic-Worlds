using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The meteor showers a planet or moon sees (VISION.md EVT-02; owner's choice: simulated from
/// comets). A comet leaves dust all along its orbit, so wherever the planet's orbit passes
/// close to a comet's orbit, the planet runs into that dust at the same point of every year.
/// A moon sees its planet's showers.
/// </summary>
/// <remarks>
/// Orbits never change on their own (they're designed, not simulated), so each shower repeats
/// exactly once a year: they're found once, as a time within the year, and every year's dates
/// follow from that.
/// </remarks>
public sealed class MeteorShowerTimeline
{
    /// <summary>
    /// Dust spreads this far from a comet's orbit, as a share of the planet's orbit size (about
    /// 0.1 AU for an Earth-like orbit).
    /// </summary>
    public const double StreamWidthShare = 0.1;

    /// <summary>
    /// Meteors an hour at the peak, per km of the comet's radius, when the planet passes right
    /// through its orbit (owner's choice: bigger comets shed more dust). A 13 km comet gives
    /// about 130 an hour, like the Perseids.
    /// </summary>
    public const double PerHourPerKm = 10;

    // Points along the planet's year, to find where it passes close to a comet's orbit, and
    // points along each comet's orbit, which the distance is measured against.
    private const int YearSamples = 720;
    private const int CometSamples = 2048;

    // How closely times are pinned down, in days (under a second).
    private const double Precision = 1e-5;

    private readonly List<Stream> _streams;

    private MeteorShowerTimeline(List<Stream> streams, double yearDays)
    {
        _streams = streams;
        YearDays = yearDays;
    }

    /// <summary>A timeline with no showers (for stars, comets, and bodies with no star).</summary>
    public static MeteorShowerTimeline None { get; } = new([], 365.25);

    /// <summary>The length of the year the showers repeat with, in standard days.</summary>
    public double YearDays { get; }

    /// <summary>Whether there are no showers at all.</summary>
    public bool IsEmpty => _streams.Count == 0;

    /// <summary>
    /// Finds the showers <paramref name="body"/> sees: from every comet circling the same star
    /// as its year's orbit (see <see cref="BodyClock.YearOrbitOf"/>).
    /// </summary>
    public static MeteorShowerTimeline For(IReadOnlyList<Body> bodies, Body body)
    {
        if (!body.HasSurface || YearPath(bodies, body) is not var (star, path, year, sizeKm))
        {
            return None;
        }

        var streams = new List<Stream>();
        foreach (Body comet in bodies.Where(b =>
            b.Kind == BodyKind.Comet && b.Orbit?.ParentId == star.Id))
        {
            streams.AddRange(FindStreams(path, year, sizeKm * StreamWidthShare, comet));
        }

        return new MeteorShowerTimeline(streams, year);
    }

    /// <summary>
    /// The showers under way at any point between two times, in order of their peaks.
    /// </summary>
    public List<MeteorShower> Between(double fromDays, double toDays)
    {
        var showers = new List<MeteorShower>();
        foreach (Stream stream in _streams)
        {
            long first = (long)Math.Floor((fromDays - stream.AfterDays - stream.PeakInYear)
                / YearDays);
            long last = (long)Math.Ceiling((toDays + stream.BeforeDays - stream.PeakInYear)
                / YearDays);
            for (long year = first; year <= last; year++)
            {
                double peak = stream.PeakInYear + year * YearDays;
                var shower = new MeteorShower(stream.CometId, peak - stream.BeforeDays, peak,
                    peak + stream.AfterDays, stream.PeakPerHour);
                if (shower.EndDays > fromDays && shower.StartDays < toDays)
                {
                    showers.Add(shower);
                }
            }
        }

        return [.. showers.OrderBy(shower => shower.PeakDays)];
    }

    /// <summary>
    /// The strongest shower under way at <paramref name="timeDays"/>, or null if none is.
    /// </summary>
    public MeteorShower? ActiveAt(double timeDays)
    {
        return Between(timeDays, timeDays)
            .Where(shower => shower.IsActiveAt(timeDays))
            .MaxBy(shower => shower.PerHourAt(timeDays));
    }

    // The star, where the body's year's orbit puts it relative to that star over time, the
    // year's length, and the orbit's size. In a planet-centered system the star circles the
    // planet, so the planet is on the opposite side.
    private static (Body Star, Func<double, Vector3D> Path, double Year, double SizeKm)?
        YearPath(IReadOnlyList<Body> bodies, Body body)
    {
        if (BodyClock.YearOrbitOf(bodies, body) is not { Orbit: Orbit orbit } yearBody)
        {
            return null;
        }

        if (yearBody.Kind == BodyKind.Star)
        {
            return (yearBody, time => OrbitMath.OffsetFromParent(orbit, time) * -1,
                orbit.PeriodDays, orbit.DistanceKm);
        }

        Body star = bodies.First(b => b.Id == orbit.ParentId);
        return (star, time => OrbitMath.OffsetFromParent(orbit, time), orbit.PeriodDays,
            orbit.DistanceKm);
    }

    private static IEnumerable<Stream> FindStreams(
        Func<double, Vector3D> path, double year, double widthKm, Body comet)
    {
        double step = year / YearSamples;
        Vector3D[] yearPoints = [.. Enumerable.Range(0, YearSamples).Select(i => path(i * step))];
        CometPath cometPath = CometPath.Near(comet.Orbit!, yearPoints, widthKm);
        double DistanceAt(double time) => cometPath.DistanceTo(path(time));

        double[] distances = [.. yearPoints.Select(cometPath.DistanceTo)];
        for (int i = 0; i < YearSamples; i++)
        {
            double before = distances[(i + YearSamples - 1) % YearSamples];
            double after = distances[(i + 1) % YearSamples];
            if (distances[i] >= widthKm || distances[i] > before || distances[i] >= after)
            {
                continue;
            }

            double peak = TimeSearch.Extreme(DistanceAt, (i - 1) * step, (i + 1) * step,
                peak: false, Precision);
            double closest = DistanceAt(peak);
            double start = Edge(DistanceAt, peak, -step, widthKm, year);
            double end = Edge(DistanceAt, peak, step, widthKm, year);
            yield return new Stream(comet.Id, ((peak % year) + year) % year, peak - start,
                end - peak, PerHourPerKm * comet.RadiusKm * (1 - closest / widthKm));
        }
    }

    // When the planet leaves the dust, stepping from the peak one way (a negative step goes
    // back to the start). Within half a year at most, if the orbits run alongside each other.
    private static double Edge(
        Func<double, double> distanceAt, double peak, double step, double widthKm, double year)
    {
        double inside = peak;
        for (int i = 1; i <= YearSamples / 2; i++)
        {
            double time = peak + i * step;
            if (distanceAt(time) >= widthKm)
            {
                double low = Math.Min(inside, time);
                double high = Math.Max(inside, time);
                return TimeSearch.Crossing(t => distanceAt(t) - widthKm, low, high,
                    rising: step > 0);
            }

            inside = time;
        }

        return peak + Math.Sign(step) * year / 2;
    }

    // A shower's place in the year: its peak (days into the year from time 0), how long before
    // and after the peak it lasts, and its strength.
    private sealed record Stream(
        Guid CometId, double PeakInYear, double BeforeDays, double AfterDays, double PeakPerHour);

    // A comet's orbit as short straight pieces, keeping only those near enough to the planet's
    // path to matter (most of a comet's orbit is far out).
    private sealed class CometPath
    {
        private readonly List<(Vector3D From, Vector3D To)> _pieces;

        private CometPath(List<(Vector3D From, Vector3D To)> pieces)
        {
            _pieces = pieces;
        }

        public static CometPath Near(Orbit orbit, Vector3D[] yearPoints, double widthKm)
        {
            double nearest = yearPoints.Min(point => point.Length) - widthKm;
            double farthest = yearPoints.Max(point => point.Length) + widthKm;
            Vector3D[] points = [.. OrbitMath.EvenlySpacedTimes(orbit, CometSamples)
                .Select(time => OrbitMath.OffsetFromParent(orbit, time))];
            var pieces = new List<(Vector3D, Vector3D)>();
            for (int i = 0; i < points.Length; i++)
            {
                Vector3D from = points[i];
                Vector3D to = points[(i + 1) % points.Length];
                double pieceLength = (to - from).Length;
                if (Math.Min(from.Length, to.Length) - pieceLength <= farthest
                    && Math.Max(from.Length, to.Length) + pieceLength >= nearest)
                {
                    pieces.Add((from, to));
                }
            }

            return new CometPath(pieces);
        }

        public double DistanceTo(Vector3D point)
        {
            double nearest = double.MaxValue;
            foreach ((Vector3D from, Vector3D to) in _pieces)
            {
                Vector3D along = to - from;
                double lengthSquared = along.Dot(along);
                double share = lengthSquared == 0
                    ? 0
                    : Math.Clamp((point - from).Dot(along) / lengthSquared, 0, 1);
                nearest = Math.Min(nearest, (point - (from + along * share)).Length);
            }

            return nearest;
        }
    }
}
