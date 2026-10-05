using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>One storm at a moment of live weather (VISION.md WTH-02).</summary>
/// <param name="Kind">What kind of storm it is.</param>
/// <param name="Center">Where its middle is now.</param>
/// <param name="RadiusKm">How far its clouds reach, about, in km.</param>
/// <param name="Strength">How strong it is now, 0 to 1: it grows and fades over its life.</param>
/// <param name="BornDays">When it formed, in standard days.</param>
/// <param name="LifeDays">How long it lasts, in standard days.</param>
public sealed record Storm(
    StormKind Kind,
    GeoCoordinate Center,
    double RadiusKm,
    double Strength,
    double BornDays,
    double LifeDays);
