using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Where real gravity would let orbits stay steady (VISION.md SIM-04). Orbits in a world are
/// designed and never drift, so this is only a guide: zones, reaches, warnings, and the period
/// gravity would give. It uses the standard rules of thumb:
/// <list type="bullet">
/// <item>the <b>Roche limit</b>: closer than this, tides tear a loose moon apart (rings live
/// inside it);</item>
/// <item>the <b>Hill sphere</b>: how far a body's own pull beats the pull of what it circles;
/// moons stay steady out to about half of it (about 0.7 for moons going backwards);</item>
/// <item>two bodies circling the same parent stay clear of each other when their orbits are at
/// least 2√3 "mutual Hill radii" apart.</item>
/// </list>
/// </summary>
public static class OrbitStability
{
    /// <summary>The density assumed for a moon not yet made, in g/cm³: rock and ice.</summary>
    public const double TypicalMoonDensity = 3.0;

    private const double GravityConstant = 6.674e-11;  // m³ / (kg s²)
    private const double SecondsPerDay = 86_400;
    private const double FluidRocheFactor = 2.44;
    private const double ProgradeHillFraction = 0.5;
    private const double RetrogradeHillFraction = 0.7;
    private const double HillSpacing = 3.4641016151377544;  // 2√3

    /// <summary>
    /// The Roche limit around <paramref name="primary"/> for a body of the given density, in km
    /// from its center: closer than this, its tides would pull such a body apart.
    /// </summary>
    public static double RocheLimitKm(Body primary, double satelliteDensity) =>
        FluidRocheFactor * primary.RadiusKm
            * Math.Cbrt(BodyMass.Density(primary) / satelliteDensity);

    /// <summary>
    /// How far a body's own pull beats the pull of the heavier body it circles (or that circles
    /// it, in a system centered on a planet), in km: its Hill sphere. Infinity when nothing
    /// heavier is near, as for a system's main star.
    /// </summary>
    public static double HillRadiusKm(IReadOnlyList<Body> bodies, Body body) =>
        HillSphere(bodies, body).RadiusKm;

    /// <summary>
    /// Where a new moon of <paramref name="body"/> could circle it steadily: from its Roche
    /// limit (for a moon of rock and ice) out to about half its Hill sphere. Null if there's no
    /// room (the body is too close to something heavier).
    /// </summary>
    public static OrbitZone? MoonZone(IReadOnlyList<Body> bodies, Body body)
    {
        double inner = Math.Max(ReachKm(body), RocheLimitKm(body, TypicalMoonDensity));
        double outer = ProgradeHillFraction * HillRadiusKm(bodies, body);
        return outer > inner ? new OrbitZone(inner, outer) : null;
    }

    /// <summary>
    /// How far the pull of each body circling <paramref name="parent"/> reaches, so the gaps
    /// between them show where another body could fit. Bodies heavier than the parent (a star
    /// circling a planet at the center) are left out: the parent circles them, really.
    /// </summary>
    public static IReadOnlyList<NeighborReach> Reaches(IReadOnlyList<Body> bodies, Body parent)
    {
        double parentKg = BodyMass.Kg(parent);
        var reaches = new List<NeighborReach>();
        foreach (Body body in bodies)
        {
            if (body.Orbit is Orbit orbit && orbit.ParentId == parent.Id
                && BodyMass.Kg(body) <= parentKg)
            {
                double reach = HillSpacing * orbit.DistanceKm
                    * Math.Cbrt(BodyMass.Kg(body) / (3 * parentKg));
                reaches.Add(new NeighborReach(body.Id, Math.Max(0, Nearest(orbit) - reach),
                    Farthest(orbit) + reach));
            }
        }

        return reaches;
    }

    /// <summary>
    /// How long one trip round would take at the body's orbit size under real gravity
    /// (Kepler's third law), in days. Null for a body that doesn't orbit, or a realm, whose
    /// branch carries it round.
    /// </summary>
    public static double? NaturalPeriodDays(IReadOnlyList<Body> bodies, Body body)
    {
        if (body.Orbit is not Orbit orbit || body.Branch is not null
            || bodies.FirstOrDefault(b => b.Id == orbit.ParentId) is not Body parent)
        {
            return null;
        }

        double distanceM = orbit.DistanceKm * 1000;
        double totalKg = BodyMass.Kg(parent) + BodyMass.Kg(body);
        return 2 * Math.PI
            * Math.Sqrt(distanceM * distanceM * distanceM / (GravityConstant * totalKg))
            / SecondsPerDay;
    }

    /// <summary>
    /// Every orbit in the system that real gravity wouldn't keep steady: bodies that run into
    /// or pass inside the Roche limit of what they circle, moons that stray too far to be held,
    /// and neighbors too close together. Realms are held by their branches, so two realms are
    /// never warned about each other.
    /// </summary>
    public static IReadOnlyList<OrbitWarning> Warnings(IReadOnlyList<Body> bodies)
    {
        var byId = bodies.ToDictionary(b => b.Id);
        var warnings = new List<OrbitWarning>();
        foreach (Body body in bodies)
        {
            if (body.Orbit is Orbit orbit && byId.GetValueOrDefault(orbit.ParentId) is Body parent)
            {
                AddTooCloseWarning(warnings, body, parent, orbit);
                AddTooFarWarning(warnings, bodies, body, parent, orbit);
            }
        }

        AddNeighborWarnings(warnings, bodies);
        return warnings;
    }

    // A body that runs into what it circles, or passes inside its Roche limit (whichever is
    // lighter gets torn apart).
    private static void AddTooCloseWarning(
        List<OrbitWarning> warnings, Body body, Body parent, Orbit orbit)
    {
        if (body.Branch is not null)
        {
            return;
        }

        bool parentHeavier = BodyMass.Kg(parent) >= BodyMass.Kg(body);
        (Body heavy, Body light) = parentHeavier ? (parent, body) : (body, parent);
        double nearest = Nearest(orbit);
        if (nearest < ReachKm(heavy) + ReachKm(light))
        {
            warnings.Add(new OrbitWarning(body.Id, $"{body.Name}'s orbit runs into {parent.Name}"));
            return;
        }

        double roche = RocheLimitKm(heavy, BodyMass.Density(light));
        if (nearest < roche)
        {
            warnings.Add(new OrbitWarning(body.Id,
                $"{body.Name} comes within {nearest:N0} km of {parent.Name}, inside " +
                $"{heavy.Name}'s Roche limit ({roche:N0} km): its tides would tear " +
                $"{light.Name} apart"));
        }
    }

    // A moon farther out than its planet can hold it against what the planet circles.
    private static void AddTooFarWarning(List<OrbitWarning> warnings, IReadOnlyList<Body> bodies,
        Body body, Body parent, Orbit orbit)
    {
        if (BodyMass.Kg(parent) < BodyMass.Kg(body)
            || HillSphere(bodies, parent) is not (double hill, Body dominant)
            || double.IsInfinity(hill))
        {
            return;
        }

        double fraction = orbit.TiltDegrees > 90 ? RetrogradeHillFraction : ProgradeHillFraction;
        double farthest = Farthest(orbit);
        if (farthest > fraction * hill)
        {
            warnings.Add(new OrbitWarning(body.Id,
                $"{body.Name} strays {farthest:N0} km from {parent.Name}, which can only hold " +
                $"it out to about {fraction * hill:N0} km: {dominant.Name}'s pull would take " +
                "it away"));
        }
    }

    // Two bodies circling the same parent whose orbits come too close (or overlap).
    private static void AddNeighborWarnings(
        List<OrbitWarning> warnings, IReadOnlyList<Body> bodies)
    {
        foreach (Body parent in bodies)
        {
            double parentKg = BodyMass.Kg(parent);
            List<Body> circling = [.. bodies.Where(b => b.Orbit?.ParentId == parent.Id
                && BodyMass.Kg(b) <= parentKg)];
            for (int i = 0; i < circling.Count; i++)
            {
                for (int j = i + 1; j < circling.Count; j++)
                {
                    AddPairWarning(warnings, circling[i], circling[j], parentKg);
                }
            }
        }
    }

    private static void AddPairWarning(
        List<OrbitWarning> warnings, Body first, Body second, double parentKg)
    {
        if (first.Branch is not null && second.Branch is not null)
        {
            return;
        }

        (Body inner, Body outer) = first.Orbit!.DistanceKm <= second.Orbit!.DistanceKm
            ? (first, second)
            : (second, first);
        double gap = Nearest(outer.Orbit!) - Farthest(inner.Orbit!);
        double mutualHill = Math.Cbrt((BodyMass.Kg(inner) + BodyMass.Kg(outer)) / (3 * parentKg))
            * (inner.Orbit!.DistanceKm + outer.Orbit!.DistanceKm) / 2;
        double needed = HillSpacing * mutualHill;
        if (gap >= needed)
        {
            return;
        }

        string message = gap <= 0
            ? $"{inner.Name}'s and {outer.Name}'s orbits overlap: they'd pull each other off " +
                "course"
            : $"{inner.Name} and {outer.Name} come within {gap:N0} km of each other's orbits; " +
                $"about {needed:N0} km would keep them from pulling each other off course";
        warnings.Add(new OrbitWarning(inner.Id, message));
        warnings.Add(new OrbitWarning(outer.Id, message));
    }

    // The body's Hill sphere and the heavier body that limits it: the smallest over what it
    // circles and anything heavier circling it, d (1 - e) ∛(m / 3M) at their closest.
    private static (double RadiusKm, Body? Dominant) HillSphere(
        IReadOnlyList<Body> bodies, Body body)
    {
        double bodyKg = BodyMass.Kg(body);
        (double RadiusKm, Body? Dominant) smallest = (double.PositiveInfinity, null);
        foreach (Body other in bodies)
        {
            Orbit? linking = other.Id == body.Orbit?.ParentId ? body.Orbit
                : other.Orbit?.ParentId == body.Id ? other.Orbit
                : null;
            double otherKg = BodyMass.Kg(other);
            if (linking is null || otherKg <= bodyKg)
            {
                continue;
            }

            double radius = Nearest(linking) * Math.Cbrt(bodyKg / (3 * otherKg));
            if (radius < smallest.RadiusKm)
            {
                smallest = (radius, other);
            }
        }

        return smallest;
    }

    // How far the body's solid part reaches from its center: a flat world's rim is farther
    // out than its radius.
    internal static double ReachKm(Body body) =>
        body.Shape == BodyShape.FlatDisc ? body.RadiusKm * FlatDisc.Radius : body.RadiusKm;

    internal static double Nearest(Orbit orbit) => orbit.DistanceKm * (1 - orbit.Eccentricity);

    internal static double Farthest(Orbit orbit) => orbit.DistanceKm * (1 + orbit.Eccentricity);
}
