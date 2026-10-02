using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Finds the eclipses a body takes part in (VISION.md EVT-01), from where the bodies are on
/// their orbits. Owner's choice: the selected body's own eclipses, which are
/// <list type="bullet">
/// <item>solar: one of its moons passes in front of the star, as seen from the body;</item>
/// <item>lunar: one of its moons passes through the body's shadow;</item>
/// <item>for a moon, the same eclipses its planet sees with it: lunar when the moon itself
/// passes through the planet's shadow, solar when it passes in front of the star as seen from
/// the planet (owner's request).</item>
/// </list>
/// Deterministic, like everything in the simulation.
/// </summary>
/// <remarks>
/// <para>Every eclipse is a body passing through another's shadow. The star's light past the
/// blocking body makes two cones: the <b>umbra</b>, where the star is completely hidden (it
/// narrows to a point; beyond it is the <b>antumbra</b>, where the blocker looks smaller than
/// the star and leaves a ring), and the wider <b>penumbra</b>, where the star is partly
/// hidden. The search steps through time measuring how far the shadowed body is from the
/// shadow's center, finds each close pass, and refines it exactly.</para>
/// <para>Solar eclipses count if the shadow touches the body anywhere, as for Earth, where a
/// solar eclipse is seen only along a narrow path.</para>
/// </remarks>
public static class Eclipses
{
    // Points checked per trip of the faster motion (usually a moon's orbit). Each pass by the
    // shadow is one dip in the distance from it, found between the points, then refined
    // exactly if it can reach the shadow.
    private const int SamplesPerPeriod = 60;

    // At most this many points per pair of bodies, so a tiny orbital period can't stall the
    // search (passes may then be missed, for moons circling in minutes).
    private const int MaxSamples = 1_000_000;

    // How closely (as a share of a step) a pass is first pinned down, to see whether it can
    // reach the shadow at all.
    private const double CoarseShare = 0.02;

    // How far apart (in shadow widths) a body counts as "nowhere near" the shadow, e.g. on the
    // star's side of the blocker.
    private const double FarAway = 1e9;

    /// <summary>
    /// Every eclipse of <paramref name="body"/> (see the class summary) whose peak falls
    /// between two times, in order. Empty for a star, or for a body without a star.
    /// </summary>
    /// <exception cref="ArgumentException">The bodies' orbits are invalid.</exception>
    public static List<Eclipse> Between(
        IReadOnlyList<Body> bodies, Body body, double fromDays, double toDays)
    {
        var eclipses = new List<Eclipse>();
        if (body.Kind == BodyKind.Star || !(toDays > fromDays)
            || Seasons.StarFor(bodies, body) is not Body star)
        {
            return eclipses;
        }

        if (SystemHierarchy.Problem(bodies) is string problem)
        {
            throw new ArgumentException($"The star system is invalid: {problem}.", nameof(bodies));
        }

        var byId = bodies.ToDictionary(b => b.Id);
        double yearDays = BodyClock.YearDays(bodies, body);
        foreach ((Body blocker, Body shadowed, EclipseKind kind) in PairsFor(bodies, body, byId))
        {
            var shadow = new Shadow(star, blocker, shadowed, byId);
            double period = (blocker.Orbit?.ParentId == shadowed.Id ? blocker : shadowed)
                .Orbit!.PeriodDays;
            double step = Math.Max(Math.Min(period, yearDays) / SamplesPerPeriod,
                (toDays - fromDays) / MaxSamples);
            eclipses.AddRange(Search(shadow, kind, step, fromDays, toDays));
        }

        return [.. eclipses.OrderBy(e => e.PeakDays)];
    }

    // The (blocker, shadowed) pairs whose eclipses belong to the body.
    private static IEnumerable<(Body Blocker, Body Shadowed, EclipseKind Kind)> PairsFor(
        IReadOnlyList<Body> bodies, Body body, Dictionary<Guid, Body> byId)
    {
        foreach (Body moon in SystemHierarchy.ChildrenOf(bodies, body.Id))
        {
            if (moon.Kind != BodyKind.Star)
            {
                yield return (moon, body, EclipseKind.Solar);
                yield return (body, moon, EclipseKind.Lunar);
            }
        }

        if (body.Orbit is Orbit orbit
            && byId[orbit.ParentId] is { Kind: not BodyKind.Star } parent)
        {
            yield return (parent, body, EclipseKind.Lunar);
            yield return (body, parent, EclipseKind.Solar);
        }
    }

    // Steps through time, finding each pass close to the shadow's center and keeping those that
    // touch the shadow with their peak in range.
    private static IEnumerable<Eclipse> Search(
        Shadow shadow, EclipseKind kind, double step, double fromDays, double toDays)
    {
        double start = fromDays - step;
        long count = (long)Math.Ceiling((toDays - start) / step) + 2;
        double beforeLast = shadow.Miss(start);
        double last = shadow.Miss(start + step);
        for (long i = 2; i < count; i++)
        {
            double time = start + i * step;
            double miss = shadow.Miss(time);
            // The lowest point of a dip lies within a step of the lowest sample, and can't
            // drop below it by more than the sampled change per step.
            double canDrop = Math.Max(beforeLast - last, miss - last);
            if (last <= beforeLast && last < miss && last < FarAway && last - canDrop < 1
                && PeakOfPass(shadow, time - 2 * step, time, canDrop / step) is double peak
                && peak >= fromDays && peak < toDays)
            {
                yield return Describe(shadow, kind, peak, step);
            }

            beforeLast = last;
            last = miss;
        }
    }

    // The moment a pass between two times comes closest to the shadow's center, if it touches
    // the shadow; null if it misses. Pinned down roughly first (most passes miss), then exactly.
    // `steepest` bounds how fast the miss ratio changes per day near the pass (doubled below,
    // for safety).
    private static double? PeakOfPass(Shadow shadow, double low, double high, double steepest)
    {
        double within = (high - low) / 2 * CoarseShare;
        double near = TimeSearch.Extreme(shadow.Miss, low, high, peak: false, precision: within);
        if (shadow.Miss(near) - 2 * steepest * within >= 1)
        {
            return null;
        }

        double peak = TimeSearch.Extreme(shadow.Miss, near - within, near + within, peak: false);
        return shadow.Miss(peak) < 1 ? peak : null;
    }

    // Everything about one eclipse, given its peak.
    private static Eclipse Describe(Shadow shadow, EclipseKind kind, double peak, double step)
    {
        // Walk out to times outside the shadow (capped, in case a body never leaves it).
        double before = peak - step;
        for (int i = 0; i < SamplesPerPeriod && shadow.Miss(before) < 1; i++)
        {
            before -= step;
        }

        double after = peak + step;
        for (int i = 0; i < SamplesPerPeriod && shadow.Miss(after) < 1; i++)
        {
            after += step;
        }

        double Edge(double time) => shadow.Miss(time) - 1;
        double startDays = TimeSearch.Crossing(Edge, before, peak, rising: false);
        double endDays = TimeSearch.Crossing(Edge, peak, after, rising: true);

        ShadowGeometry at = shadow.At(peak);
        EclipseType type = TypeOf(kind, at, shadow.ShadowedRadius);
        double coverage = kind == EclipseKind.Solar
            ? StarCoverage(type, at, shadow)
            : UmbraCoverage(at, shadow.ShadowedRadius);
        return new Eclipse(kind, type, shadow.BlockerId, shadow.ShadowedId, startDays, peak,
            endDays, coverage);
    }

    private static EclipseType TypeOf(EclipseKind kind, ShadowGeometry at, double radius)
    {
        double offset = at.Offset;
        if (at.Umbra <= 0)
        {
            // Past the umbra's point: only the ring-shaped antumbra can reach the body.
            if (offset < -at.Umbra + radius)
            {
                return EclipseType.Annular;
            }

            return kind == EclipseKind.Solar ? EclipseType.Partial : EclipseType.Penumbral;
        }

        if (kind == EclipseKind.Solar)
        {
            return offset < at.Umbra + radius ? EclipseType.Total : EclipseType.Partial;
        }

        if (offset + radius <= at.Umbra)
        {
            return EclipseType.Total;
        }

        return offset - radius < at.Umbra ? EclipseType.Partial : EclipseType.Penumbral;
    }

    // The share of the star's disk hidden, seen from the spot on the body nearest the shadow's
    // center.
    private static double StarCoverage(EclipseType type, ShadowGeometry at, Shadow shadow)
    {
        if (type == EclipseType.Total)
        {
            return 1;
        }

        double spotOffset = Math.Max(0, at.Offset - shadow.ShadowedRadius);
        double starDistance = at.StarDistance + at.Behind;
        double starSize = Math.Asin(Math.Min(1, shadow.StarRadius / starDistance));
        double blockerSize = Math.Asin(Math.Min(1, shadow.BlockerRadius / at.Behind));
        double apart = Math.Atan(spotOffset / at.Behind) - Math.Atan(spotOffset / starDistance);
        double starArea = Math.PI * starSize * starSize;
        double covered = OverlapArea(starSize, blockerSize, apart) / starArea;
        return Math.Clamp(covered, 0, 1);
    }

    // The share of the moon's width inside the umbra.
    private static double UmbraCoverage(ShadowGeometry at, double radius)
    {
        return at.Umbra <= 0
            ? 0
            : Math.Clamp((at.Umbra - (at.Offset - radius)) / (2 * radius), 0, 1);
    }

    // The area where two disks overlap, given their radii and the distance between centers.
    private static double OverlapArea(double r1, double r2, double apart)
    {
        if (apart >= r1 + r2)
        {
            return 0;
        }

        if (apart <= Math.Abs(r1 - r2))
        {
            double smaller = Math.Min(r1, r2);
            return Math.PI * smaller * smaller;
        }

        double part1 = r1 * r1 * Math.Acos(
            Math.Clamp((apart * apart + r1 * r1 - r2 * r2) / (2 * apart * r1), -1, 1));
        double part2 = r2 * r2 * Math.Acos(
            Math.Clamp((apart * apart + r2 * r2 - r1 * r1) / (2 * apart * r2), -1, 1));
        double triangle = 0.5 * Math.Sqrt(Math.Max(0,
            (-apart + r1 + r2) * (apart + r1 - r2) * (apart - r1 + r2) * (apart + r1 + r2)));
        return part1 + part2 - triangle;
    }

    // Where the shadowed body sits in the blocker's shadow at a moment (all in km).
    // Offset: from the shadow's center line. Behind: along the line, past the blocker.
    // Penumbra, Umbra: the shadows' radii there (Umbra below 0 is the antumbra's radius).
    private readonly record struct ShadowGeometry(
        double Offset, double Behind, double Penumbra, double Umbra, double StarDistance);

    // One blocker's shadow (cast by the star) and the body it may fall on.
    private sealed class Shadow
    {
        private readonly OrbitChain _star;
        private readonly OrbitChain _blocker;
        private readonly OrbitChain _shadowed;

        public Shadow(Body star, Body blocker, Body shadowed, Dictionary<Guid, Body> byId)
        {
            _star = OrbitChain.Of(star, byId);
            _blocker = OrbitChain.Of(blocker, byId);
            _shadowed = OrbitChain.Of(shadowed, byId);
            StarRadius = star.RadiusKm;
            BlockerRadius = blocker.RadiusKm;
            ShadowedRadius = shadowed.RadiusKm;
            BlockerId = blocker.Id;
            ShadowedId = shadowed.Id;
        }

        public double StarRadius { get; }

        public double BlockerRadius { get; }

        public double ShadowedRadius { get; }

        public Guid BlockerId { get; }

        public Guid ShadowedId { get; }

        // How far the shadowed body is from touching the penumbra, as a ratio: below 1 it's in
        // the shadow, 0 is dead center. FarAway on the star's side of the blocker.
        public double Miss(double timeDays)
        {
            ShadowGeometry at = At(timeDays);
            return at.Behind > 0 ? at.Offset / (at.Penumbra + ShadowedRadius) : FarAway;
        }

        public ShadowGeometry At(double timeDays)
        {
            Vector3D star = _star.PositionAt(timeDays);
            Vector3D blocker = _blocker.PositionAt(timeDays);
            Vector3D fromStar = blocker - star;
            double starDistance = fromStar.Length;
            Vector3D direction = fromStar * (1 / starDistance);
            Vector3D fromBlocker = _shadowed.PositionAt(timeDays) - blocker;
            double behind = fromBlocker.Dot(direction);
            double offset = (fromBlocker - direction * behind).Length;

            // The cones' edges run along lines touching both the star and the blocker.
            double outer = Math.Min(0.999999, (StarRadius + BlockerRadius) / starDistance);
            double inner = Math.Clamp((StarRadius - BlockerRadius) / starDistance,
                -0.999999, 0.999999);
            double penumbra = Cone(outer, behind, widening: true);
            double umbra = Cone(inner, behind, widening: false);
            return new ShadowGeometry(offset, behind, penumbra, umbra, starDistance);
        }

        // A shadow cone's radius at a distance behind the blocker, given the sine of its
        // half-angle.
        private double Cone(double sine, double behind, bool widening)
        {
            double cosine = Math.Sqrt(1 - sine * sine);
            double spread = behind * sine / cosine;
            return BlockerRadius / cosine + (widening ? spread : -spread);
        }
    }
}
