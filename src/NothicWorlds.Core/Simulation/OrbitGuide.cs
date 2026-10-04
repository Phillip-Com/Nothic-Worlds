using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The rings the stable orbit guide draws for a selected body (VISION.md SIM-04), from
/// <see cref="OrbitStability"/>: around the body, where its moons could circle steadily; and
/// around what it circles, where the body itself could. Each is split into steady stretches
/// and unsteady ones (inside the Roche limit, or within a neighbor's reach).
/// </summary>
public static class OrbitGuide
{
    // With nothing limiting a zone (a system's main star), it's drawn out to half again past
    // the farthest orbit around the body, or this many times its inner edge if there's none.
    private const double OpenZoneMargin = 1.5;
    private const double OpenZoneWithoutOrbits = 20;

    /// <summary>
    /// The guide's rings for <paramref name="selected"/>: around it, then around its parent
    /// (leaving out its own reach, since it's the one that would move). A realm hangs where its
    /// branch puts it, so it gets no rings around its tree; nor does a body circling something
    /// lighter than itself.
    /// </summary>
    public static IReadOnlyList<GuideBand> Bands(IReadOnlyList<Body> bodies, Body selected)
    {
        var bands = new List<GuideBand>();
        AddBandsAround(bands, bodies, selected, movingId: null);
        if (selected.Branch is null && selected.Orbit is Orbit orbit
            && bodies.FirstOrDefault(b => b.Id == orbit.ParentId) is Body parent
            && BodyMass.Kg(parent) >= BodyMass.Kg(selected))
        {
            AddBandsAround(bands, bodies, parent, selected.Id);
        }

        return bands;
    }

    private static void AddBandsAround(
        List<GuideBand> bands, IReadOnlyList<Body> bodies, Body center, Guid? movingId)
    {
        double surface = OrbitStability.ReachKm(center);
        if (OrbitStability.MoonZone(bodies, center) is not OrbitZone zone)
        {
            // No room: everything near it is unsteady, out to where moons would be lost.
            double lost = Math.Max(
                OrbitStability.RocheLimitKm(center, OrbitStability.TypicalMoonDensity),
                OrbitStability.HillRadiusKm(bodies, center) / 2);
            AddBand(bands, center, surface, lost, steady: false);
            return;
        }

        List<NeighborReach> reaches = [.. OrbitStability.Reaches(bodies, center)
            .Where(reach => reach.BodyId != movingId)];
        double end = double.IsInfinity(zone.OuterKm)
            ? OpenZoneEnd(bodies, center, zone, reaches)
            : zone.OuterKm;
        AddBand(bands, center, surface, zone.InnerKm, steady: false);
        double at = zone.InnerKm;
        foreach ((double inner, double outer) in Merged(reaches, zone.InnerKm, end))
        {
            AddBand(bands, center, at, inner, steady: true);
            AddBand(bands, center, inner, outer, steady: false);
            at = outer;
        }

        AddBand(bands, center, at, end, steady: true);
    }

    // How far to draw a zone nothing limits: past every orbit around the body and its reach.
    private static double OpenZoneEnd(IReadOnlyList<Body> bodies, Body center, OrbitZone zone,
        List<NeighborReach> reaches)
    {
        double farthest = bodies
            .Where(b => b.Orbit?.ParentId == center.Id)
            .Select(b => OrbitStability.Farthest(b.Orbit!))
            .DefaultIfEmpty(0)
            .Max();
        double end = farthest > 0
            ? OpenZoneMargin * farthest
            : OpenZoneWithoutOrbits * zone.InnerKm;
        return Math.Max(end, reaches.Select(r => r.OuterKm).DefaultIfEmpty(0).Max());
    }

    // The neighbors' reaches cut to [from, to], with overlapping ones joined, nearest first.
    private static List<(double Inner, double Outer)> Merged(
        List<NeighborReach> reaches, double from, double to)
    {
        var merged = new List<(double Inner, double Outer)>();
        foreach (NeighborReach reach in reaches.OrderBy(r => r.InnerKm))
        {
            double inner = Math.Max(reach.InnerKm, from);
            double outer = Math.Min(reach.OuterKm, to);
            if (outer <= inner)
            {
                continue;
            }

            if (merged.Count > 0 && inner <= merged[^1].Outer)
            {
                merged[^1] = (merged[^1].Inner, Math.Max(merged[^1].Outer, outer));
            }
            else
            {
                merged.Add((inner, outer));
            }
        }

        return merged;
    }

    private static void AddBand(
        List<GuideBand> bands, Body center, double inner, double outer, bool steady)
    {
        if (outer > inner)
        {
            bands.Add(new GuideBand(center.Id, inner, outer, steady));
        }
    }
}
