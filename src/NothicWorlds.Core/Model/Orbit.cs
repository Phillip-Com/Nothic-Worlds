namespace NothicWorlds.Core.Model;

/// <summary>
/// A designed ("on-rails") orbit (VISION.md SIM-01): the path one body follows around another,
/// exactly as the user set it, forever. Nothing here is derived from physics: the period is
/// whatever the user wants. Immutable; change an orbit by replacing it.
/// </summary>
/// <remarks>
/// Most orbits only need <see cref="DistanceKm"/>, <see cref="PeriodDays"/>, and
/// <see cref="StartAngleDegrees"/>: a flat circle. The rest are optional extras for an
/// elongated (elliptical) or tilted orbit. The math is in <c>Simulation.OrbitMath</c> and
/// docs/world-format.md.
/// </remarks>
public sealed record Orbit
{
    /// <summary>The longest allowed orbit (about 67,000 times the Earth–Sun distance).</summary>
    public const double MaxDistanceKm = 1e13;

    /// <summary>The longest allowed period, in days.</summary>
    public const double MaxPeriodDays = 1e9;

    /// <summary>The most elongated an orbit can be (0 is a circle; 1 would never close).</summary>
    public const double MaxEccentricity = 0.95;

    /// <summary>The body this one circles.</summary>
    public required Guid ParentId { get; init; }

    /// <summary>
    /// The orbit's size: its average distance from the parent (for a circle, the radius). In km,
    /// center to center.
    /// </summary>
    public required double DistanceKm { get; init; }

    /// <summary>How long one trip around takes, in standard (24-hour) days.</summary>
    public required double PeriodDays { get; init; }

    /// <summary>
    /// Where along the orbit the body is at time 0, in degrees: 0 is the reference direction
    /// (+X), counting counterclockwise seen from the north.
    /// </summary>
    public double StartAngleDegrees { get; init; }

    /// <summary>
    /// Optional: how elongated the orbit is. 0 (the default) is a circle; up to 0.95.
    /// </summary>
    public double Eccentricity { get; init; }

    /// <summary>
    /// Optional: which way the elongated orbit points, in degrees (where the body comes closest
    /// to its parent, measured like <see cref="StartAngleDegrees"/>).
    /// </summary>
    public double ClosestApproachDegrees { get; init; }

    /// <summary>
    /// Optional: how far the orbit is tilted from the parent's reference plane, in degrees:
    /// 0 (the default) is flat, 90 passes over the poles, and over 90 runs backwards.
    /// </summary>
    public double TiltDegrees { get; init; }

    /// <summary>
    /// Optional: which way the tilt leans, in degrees: the direction where the body rises
    /// through the reference plane heading north.
    /// </summary>
    public double TiltDirectionDegrees { get; init; }

    /// <summary>What's wrong with this orbit, or null if it's usable.</summary>
    public string? Problem()
    {
        if (!double.IsFinite(DistanceKm) || DistanceKm <= 0 || DistanceKm > MaxDistanceKm)
        {
            return $"an orbit's distance must be above 0 and at most {MaxDistanceKm:g} km";
        }

        if (!double.IsFinite(PeriodDays) || PeriodDays <= 0 || PeriodDays > MaxPeriodDays)
        {
            return $"an orbit's period must be above 0 and at most {MaxPeriodDays:g} days";
        }

        if (!double.IsFinite(Eccentricity) || Eccentricity < 0 || Eccentricity > MaxEccentricity)
        {
            return $"an orbit's elongation (eccentricity) must be 0 to {MaxEccentricity}";
        }

        if (!double.IsFinite(TiltDegrees) || TiltDegrees < 0 || TiltDegrees > 180)
        {
            return "an orbit's tilt must be 0° to 180°";
        }

        return double.IsFinite(StartAngleDegrees) && double.IsFinite(ClosestApproachDegrees)
            && double.IsFinite(TiltDirectionDegrees)
            ? null
            : "an orbit's angles must be numbers";
    }
}
