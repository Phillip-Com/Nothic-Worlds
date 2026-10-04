using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// An asteroid from a belt passing close to a planet or moon, or hitting it (VISION.md EVT-02).
/// </summary>
/// <param name="Kind">A close pass or an impact.</param>
/// <param name="BeltId">The belt it came from.</param>
/// <param name="TimeDays">When, in standard days on the world's clock.</param>
/// <param name="SizeMeters">The asteroid's size (its diameter), in meters.</param>
/// <param name="DistanceKm">
/// How close it passes, in km from the body's center (0 for an impact).
/// </param>
/// <param name="Spot">Where it hits, for an impact; null for a close pass.</param>
public sealed record AsteroidEvent(
    AsteroidEventKind Kind,
    Guid BeltId,
    double TimeDays,
    double SizeMeters,
    double DistanceKm,
    GeoCoordinate? Spot);
