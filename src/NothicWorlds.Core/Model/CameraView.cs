namespace NothicWorlds.Core.Model;

/// <summary>
/// Where the camera was looking, saved so a world reopens where the user left off (owner
/// decision). Matches the planet camera's state: direction from the focus point, altitude, and
/// the view-slide offset (VISION.md REN-02).
/// </summary>
/// <param name="LatitudeDegrees">Camera direction from the focus point: latitude.</param>
/// <param name="LongitudeDegrees">Camera direction from the focus point: longitude.</param>
/// <param name="Altitude">Distance from the focus point, in planet radii above the surface.</param>
/// <param name="FocusOffsetX">Focus point's offset from the planet's center (view panning).</param>
/// <param name="FocusOffsetY">Focus point's offset from the planet's center (view panning).</param>
/// <param name="FocusOffsetZ">Focus point's offset from the planet's center (view panning).</param>
public sealed record CameraView(
    double LatitudeDegrees,
    double LongitudeDegrees,
    double Altitude,
    double FocusOffsetX = 0,
    double FocusOffsetY = 0,
    double FocusOffsetZ = 0);
