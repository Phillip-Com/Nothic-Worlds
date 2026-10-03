using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Session;

namespace NothicWorlds.Controls;

/// <summary>
/// The Zoom to buttons (VISION.md REN-04; owner's choice): glide down into the local view over
/// a region or a pin, selecting its body first if needed.
/// </summary>
public static class ZoomTo
{
    /// <summary>How much ground a pin is shown with, top to bottom of the screen.</summary>
    public const double PinSpanKm = 300;

    // The smallest span a region is shown with, so a tiny one isn't zoomed to the limit.
    private const double MinimumRegionSpanKm = 50;

    /// <summary>Shows a region filling most of the screen.</summary>
    public static async Task RegionAsync(WorldSession session, PlanetCamera camera, Region region)
    {
        if (SphericalPolygon.Center(region.Corners) is not GeoCoordinate centre)
        {
            return;
        }

        double reach = region.Corners.Max(c => SphericalCoordinates.ArcDegrees(centre, c));
        await SpotAsync(session, camera, region.BodyId, centre, 2 * reach,
            MinimumRegionSpanKm);
    }

    /// <summary>Shows a pinned spot with <see cref="PinSpanKm"/> of ground around it.</summary>
    public static Task PinAsync(
        WorldSession session, PlanetCamera camera, Guid bodyId, GeoCoordinate spot)
    {
        return SpotAsync(session, camera, bodyId, spot, 0, PinSpanKm);
    }

    // Selects the body (flying there) if needed, then glides down over the spot, showing at
    // least `spanDegrees` of arc and `minimumKm` of ground.
    private static async Task SpotAsync(WorldSession session, PlanetCamera camera, Guid bodyId,
        GeoCoordinate spot, double spanDegrees, double minimumKm)
    {
        if (session.World.Bodies.FirstOrDefault(b => b.Id == bodyId) is not Body body)
        {
            return;
        }

        if (session.SelectedBodyId != bodyId)
        {
            await session.SelectBodyAsync(bodyId);
        }

        double kmPerDegree = body.RadiusKm * Math.PI / 180;
        camera.FlyToSurface(spot, Math.Max(spanDegrees, minimumKm / kmPerDegree));
    }
}
