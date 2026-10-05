using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A body as seen in the sky from a spot on another (VISION.md REN-06; see
/// <see cref="SkyView"/>).
/// </summary>
/// <param name="BodyId">Which body.</param>
/// <param name="Name">Its name.</param>
/// <param name="Kind">What kind of body it is.</param>
/// <param name="AltitudeDegrees">
/// How high it stands above the horizon, in degrees (negative: below it).
/// </param>
/// <param name="AzimuthDegrees">
/// Its compass direction, in degrees clockwise from north (90 is east).
/// </param>
/// <param name="AngularDiameterDegrees">How wide it looks, in degrees (true size).</param>
/// <param name="DistanceKm">How far away it is, in km.</param>
/// <param name="LitFraction">
/// How much of its face is lit, 0 (new) to 1 (full); 1 for a star, which shines itself.
/// </param>
/// <param name="East">Its direction's share east, in the spot's local frame (unit vector).</param>
/// <param name="North">Its direction's share north.</param>
/// <param name="Up">Its direction's share up.</param>
public sealed record SkyBody(
    Guid BodyId,
    string Name,
    BodyKind Kind,
    double AltitudeDegrees,
    double AzimuthDegrees,
    double AngularDiameterDegrees,
    double DistanceKm,
    double LitFraction,
    double East,
    double North,
    double Up);
