using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Asteroids from the belts coming by a planet or moon (VISION.md EVT-02; owner's choice: worked
/// out from the belts, the same every time for a given world, and impacts only listed).
/// </summary>
/// <remarks>
/// <para>A belt's rocks aren't tracked one by one, so its events are drawn by chance, at a rate
/// set by the system: a body whose year's orbit runs through a dense belt has many close passes,
/// fewer the farther its orbit stays from the belt, and very rarely an impact (more likely for
/// a bigger body). A moon shares its planet's orbit, and so its belt.</para>
/// <para>The chances are rolled with a fixed recipe (<see cref="SeededRandom"/>) seeded by the
/// body, the belt, and the year (counted from the world's time 0), so the same world always has
/// the same events, and only edits change them.</para>
/// </remarks>
public static class AsteroidEvents
{
    /// <summary>
    /// Close passes a year for a body whose orbit lies inside a belt of full density.
    /// </summary>
    public const double PassesPerYear = 24;

    /// <summary>The chance that a close pass is an impact, for an Earth-sized body.</summary>
    public const double ImpactShare = 0.004;

    // Outside a belt, its pull fades over this share of the belt's width.
    private const double ReachShare = 0.25;

    // Points along the orbit to measure how much of it lies near a belt.
    private const int OrbitSamples = 64;

    private const double EarthRadiusKm = 6371;

    /// <summary>The events between two times, in order.</summary>
    public static List<AsteroidEvent> Between(
        IReadOnlyList<Body> bodies, Body body, double fromDays, double toDays)
    {
        var events = new List<AsteroidEvent>();
        if (!body.HasSurface || BodyClock.YearOrbitOf(bodies, body) is not { Orbit: Orbit orbit }
            || toDays <= fromDays)
        {
            return events;
        }

        // The star whose belts the year's orbit runs through (the orbit's other end).
        Body star = bodies.First(b => b.Id == orbit.ParentId) is { GivesLight: true } parent
            ? parent
            : BodyClock.YearOrbitOf(bodies, body)!;
        double year = orbit.PeriodDays;
        foreach (AsteroidBelt belt in star.Belts)
        {
            double passesPerYear = PassesPerYear * belt.Density * Exposure(orbit, belt);
            if (passesPerYear < 1e-6)
            {
                continue;
            }

            long first = (long)Math.Floor(fromDays / year);
            long last = (long)Math.Floor(toDays / year);
            for (long index = first; index <= last; index++)
            {
                events.AddRange(YearOf(body, belt, index, year, passesPerYear)
                    .Where(e => e.TimeDays >= fromDays && e.TimeDays < toDays));
            }
        }

        return [.. events.OrderBy(e => e.TimeDays)];
    }

    /// <summary>
    /// How much a belt's rocks cross an orbit, from 0 to 1: the time-weighted share of the
    /// orbit inside the belt, counting nearby stretches outside it for less.
    /// </summary>
    public static double Exposure(Orbit orbit, AsteroidBelt belt)
    {
        double reach = (belt.OuterKm - belt.InnerKm) * ReachShare;
        double total = 0;
        for (int i = 0; i < OrbitSamples; i++)
        {
            double distance = OrbitMath.OffsetFromParent(
                orbit, orbit.PeriodDays * i / OrbitSamples).Length;
            double gap = Math.Max(0, Math.Max(belt.InnerKm - distance, distance - belt.OuterKm));
            total += Math.Exp(-gap / reach);
        }

        return total / OrbitSamples;
    }

    // One year's events from one belt.
    private static IEnumerable<AsteroidEvent> YearOf(
        Body body, AsteroidBelt belt, long index, double year, double passesPerYear)
    {
        // Seeded exactly as when asteroid events were first added, so worlds keep their dates.
        var dice = SeededRandom.FromState(
            SeededRandom.Mix(BitConverter.ToUInt64(body.Id.ToByteArray(), 0))
            ^ SeededRandom.Mix(BitConverter.ToUInt64(belt.Id.ToByteArray(), 8))
            ^ SeededRandom.Mix(unchecked((ulong)index + 0x9E3779B97F4A7C15)));
        double impactShare = Math.Min(1,
            ImpactShare * Math.Pow(body.RadiusKm / EarthRadiusKm, 2));
        int passes = dice.Poisson(passesPerYear);
        for (int i = 0; i < passes; i++)
        {
            double time = (index + dice.Next()) * year;
            if (dice.Next() < impactShare)
            {
                // Anywhere on the surface, evenly.
                double latitude = double.RadiansToDegrees(Math.Asin(dice.Next() * 2 - 1));
                double longitude = dice.Next() * 360 - 180;
                yield return new AsteroidEvent(AsteroidEventKind.Impact, belt.Id, time,
                    Size(dice.Next(), 10, 2000), 0, new GeoCoordinate(latitude, longitude));
            }
            else
            {
                // From about 3 to 1,000 of the body's radii away, near passes rarer.
                double distance = body.RadiusKm * Math.Pow(10, 0.5 + 2.5 * dice.Next());
                yield return new AsteroidEvent(AsteroidEventKind.ClosePass, belt.Id, time,
                    Size(dice.Next(), 15, 5000), distance, null);
            }
        }
    }

    // Sizes follow a power law, as real asteroids' do: most are small, a few are large.
    private static double Size(double roll, double smallest, double largest) =>
        Math.Min(largest, smallest * Math.Pow(Math.Max(roll, 1e-9), -0.7));
}
