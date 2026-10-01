using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Interop;

namespace NothicWorlds.Controls;

/// <summary>
/// Converts between the screen and a planet's surface: which latitude/longitude is under the
/// mouse, and where a surface point appears on screen. The planet is a unit sphere in its own
/// space; its node's transform places, turns, and sizes it, so the latitude/longitude found are
/// the body's own, however it's spinning or tilted.
/// </summary>
public static class GlobePicker
{
    /// <summary>
    /// The surface point under a screen position, or null if that position misses the planet.
    /// With <paramref name="nearestWhenMissed"/>, a miss instead gives the point on the planet's
    /// visible edge nearest the ray, so a drag keeps working past the edge.
    /// </summary>
    public static GeoCoordinate? CoordinateAt(
        PlanetCamera camera, Node3D planet, Vector2 screen, bool nearestWhenMissed = false)
    {
        Transform3D toPlanet = planet.GlobalTransform.AffineInverse();
        Vector3 origin = toPlanet * camera.ProjectRayOrigin(screen);
        Vector3 direction = (toPlanet.Basis * camera.ProjectRayNormal(screen)).Normalized();

        // Ray–sphere intersection: `along` is the ray's closest approach to the center.
        float along = -origin.Dot(direction);
        Vector3 closest = origin + direction * along;
        const float radius = 1.0f;  // In the planet's own space.
        float missSquared = closest.LengthSquared();
        Vector3 point;
        if (missSquared <= radius * radius)
        {
            point = origin + direction * (along - Mathf.Sqrt(radius * radius - missSquared));
        }
        else if (nearestWhenMissed && closest.LengthSquared() > 0)
        {
            point = closest;
        }
        else
        {
            return null;
        }

        return SphericalCoordinates.FromDirection(point.ToNumerics());
    }

    /// <summary>
    /// Where a surface point appears on screen, or null if it's hidden (on the far side of the
    /// planet, or behind the camera).
    /// </summary>
    public static Vector2? ScreenPositionOf(
        PlanetCamera camera, Node3D planet, GeoCoordinate coordinate)
    {
        Vector3 normal = SphericalCoordinates.ToDirection(coordinate).ToGodot();
        Vector3 world = planet.GlobalTransform * normal;  // Unit sphere in the planet's space
        Vector3 outward = world - planet.GlobalPosition;
        bool facesCamera = outward.Dot(camera.GlobalPosition - world) > 0;
        return facesCamera && !camera.IsPositionBehind(world)
            ? camera.UnprojectPosition(world)
            : null;
    }
}
