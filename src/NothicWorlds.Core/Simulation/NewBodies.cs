using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Sensible starting values for bodies the user adds (VISION.md BOD-01): Sun-, Earth-, and
/// Moon-like sizes, each new body orbiting beyond its parent's existing ones, and a starting
/// period that looks natural (the user can change everything afterwards).
/// </summary>
public static class NewBodies
{
    private const double SunRadiusKm = 696_000;
    private const double EarthOrbitKm = 149_600_000;
    private const double MoonOrbitKm = 384_400;
    private const double MoonPeriodDays = 27.3;
    private const double CompanionStarOrbitKm = 5e9;

    // A new comet comes in to this share of its star's innermost planet's distance, on an orbit
    // this elongated (it goes out 9 times as far), so it crosses that planet's orbit twice.
    private const double CometClosestShare = 0.6;
    private const double CometEccentricity = 0.8;

    // Each new orbit is this much wider than the widest one around the same parent.
    private const double SpacingFactor = 1.6;

    // Siblings start this far apart around their orbits (the golden angle), so they don't line
    // up however many are added.
    private const double StartAngleStepDegrees = 137.5;

    /// <summary>A new Earth-like planet orbiting <paramref name="parent"/>.</summary>
    public static Body Planet(IReadOnlyList<Body> bodies, Body parent)
    {
        double distance = NextDistance(
            bodies, parent, parent.Kind == BodyKind.Star ? EarthOrbitKm : MoonOrbitKm);
        return new Body
        {
            Name = NextName(bodies, "Planet"),
            Kind = BodyKind.Planet,
            Orbit = OrbitAround(bodies, parent, distance, StartingPeriod(distance, parent)),
        };
    }

    /// <summary>
    /// A new Moon-like moon orbiting <paramref name="parent"/>. Like our Moon, it turns once
    /// per orbit, always showing its parent the same face.
    /// </summary>
    public static Body Moon(IReadOnlyList<Body> bodies, Body parent)
    {
        double distance = NextDistance(bodies, parent, MoonOrbitKm);
        double period = StartingPeriod(distance, parent);
        return new Body
        {
            Name = NextName(bodies, "Moon"),
            Kind = BodyKind.Moon,
            Appearance = BodyAppearance.DefaultFor(BodyKind.Moon),
            RadiusKm = 1737.4,
            DayLengthHours = period * 24,
            Orbit = OrbitAround(bodies, parent, distance, period),
        };
    }

    /// <summary>
    /// A new star: a companion orbiting <paramref name="parent"/> far out, or, with no parent,
    /// one at the system's center.
    /// </summary>
    public static Body Star(IReadOnlyList<Body> bodies, Body? parent)
    {
        var star = new Body
        {
            Name = NextName(bodies, "Star"),
            Kind = BodyKind.Star,
            RadiusKm = SunRadiusKm * 0.7,
            DayLengthHours = 500,
        };
        if (parent is not null)
        {
            // Well beyond its planets, whichever is farther.
            double distance = Math.Max(
                NextDistance(bodies, parent, CompanionStarOrbitKm), CompanionStarOrbitKm);
            star.Orbit = OrbitAround(bodies, parent, distance, StartingPeriod(distance, parent));
        }

        return star;
    }

    /// <summary>
    /// A new comet circling <paramref name="star"/>: a few km across, on a flat, elongated orbit
    /// that dips inside its innermost planet's orbit (or the Earth's distance, with no planets),
    /// so it crosses that orbit on the way in and out. It starts on its way in, a little before
    /// its closest approach, near enough to the star to have a tail.
    /// </summary>
    public static Body Comet(IReadOnlyList<Body> bodies, Body star)
    {
        double innermost = SystemHierarchy.ChildrenOf(bodies, star.Id)
            .Where(body => body.Kind == BodyKind.Planet)
            .Select(body => body.Orbit!.DistanceKm)
            .DefaultIfEmpty(EarthOrbitKm)
            .Min();
        double distance = innermost * CometClosestShare / (1 - CometEccentricity);
        Orbit orbit = OrbitAround(bodies, star, distance, StartingPeriod(distance, star));
        double closestApproach = orbit.StartAngleDegrees;
        return new Body
        {
            Name = NextName(bodies, "Comet"),
            Kind = BodyKind.Comet,
            Appearance = BodyAppearance.DefaultFor(BodyKind.Comet),
            RadiusKm = 5,
            DayLengthHours = 12,
            Orbit = orbit with
            {
                Eccentricity = CometEccentricity,
                ClosestApproachDegrees = closestApproach,
                StartAngleDegrees = (closestApproach + 330) % 360,
            },
        };
    }

    // "Planet 2", "Moon 1", ...: the first number not already used.
    private static string NextName(IReadOnlyList<Body> bodies, string prefix)
    {
        int number = 1;
        while (bodies.Any(body => body.Name == $"{prefix} {number}"))
        {
            number++;
        }

        return $"{prefix} {number}";
    }

    private static double NextDistance(IReadOnlyList<Body> bodies, Body parent, double first)
    {
        double widest = SystemHierarchy.ChildrenOf(bodies, parent.Id)
            .Select(child => child.Orbit!.DistanceKm)
            .DefaultIfEmpty(0)
            .Max();
        return widest > 0 ? widest * SpacingFactor : Math.Max(first, parent.RadiusKm * 4);
    }

    private static Orbit OrbitAround(
        IReadOnlyList<Body> bodies, Body parent, double distanceKm, double periodDays)
    {
        int siblings = SystemHierarchy.ChildrenOf(bodies, parent.Id).Count();
        return new Orbit
        {
            ParentId = parent.Id,
            DistanceKm = Math.Min(distanceKm, Orbit.MaxDistanceKm),
            PeriodDays = Math.Clamp(periodDays, 0.01, Orbit.MaxPeriodDays),
            StartAngleDegrees = siblings * StartAngleStepDegrees % 360,
        };
    }

    // Periods grow with distance as in real systems (period² ∝ distance³), starting from the
    // Earth around the Sun (for star parents) or the Moon around the Earth (for others), and
    // treating a bigger parent as heavier. Only a starting value; the user sets the real one.
    private static double StartingPeriod(double distanceKm, Body parent)
    {
        (double period, double distance, double radius) = parent.Kind == BodyKind.Star
            ? (365.25, EarthOrbitKm, SunRadiusKm)
            : (MoonPeriodDays, MoonOrbitKm, 6371.0);
        return period * Math.Pow(distanceKm / distance, 1.5)
            * Math.Pow(radius / parent.RadiusKm, 1.5);
    }
}
