using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Interop;
using NothicWorlds.Rendering;

namespace NothicWorlds.Controls;

/// <summary>
/// Converts between the screen and a planet's surface: which latitude/longitude is under the
/// mouse, and where a surface point appears on screen. The planet is a unit sphere (or a flat
/// world's disc, VISION.md BOD-02; see <see cref="GlobeShape"/>) in its own space; its node's
/// transform places, turns, and sizes it, so the latitude/longitude found are the body's own,
/// however it's spinning or tilted.
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
        BodyShape shape = ShapeOf(planet);
        Vector3? point = shape == BodyShape.FlatDisc
            ? FacePointHit(origin, direction, nearestWhenMissed)
            : SpherePointHit(origin, direction, nearestWhenMissed);
        return point is Vector3 hit
            ? SphericalCoordinates.FromDirection(GlobeShape.DirectionAt(shape, hit).ToNumerics())
            : null;
    }

    /// <summary>
    /// Where a surface point appears on screen, or null if it's hidden (on the far side of the
    /// planet, or behind the camera).
    /// </summary>
    public static Vector2? ScreenPositionOf(
        PlanetCamera camera, Node3D planet, GeoCoordinate coordinate)
    {
        BodyShape shape = ShapeOf(planet);
        Vector3 local = GlobeShape.SurfacePoint(
            shape, SphericalCoordinates.ToDirection(coordinate).ToVector3D());
        Vector3 world = planet.GlobalTransform * local;
        Vector3 outward = planet.GlobalTransform.Basis * GlobeShape.Outward(shape, local);
        bool facesCamera = outward.Dot(camera.GlobalPosition - world) > 0;
        return facesCamera && !camera.IsPositionBehind(world)
            ? camera.UnprojectPosition(world)
            : null;
    }

    private static BodyShape ShapeOf(Node3D planet) =>
        planet is PlanetSurface surface ? surface.Shape : BodyShape.Sphere;

    // Ray–sphere intersection: `along` is the ray's closest approach to the center.
    private static Vector3? SpherePointHit(Vector3 origin, Vector3 direction, bool nearest)
    {
        float along = -origin.Dot(direction);
        Vector3 closest = origin + direction * along;
        const float radius = 1.0f;  // In the planet's own space.
        float missSquared = closest.LengthSquared();
        if (missSquared <= radius * radius)
        {
            return origin + direction * (along - Mathf.Sqrt(radius * radius - missSquared));
        }

        return nearest && closest.LengthSquared() > 0 ? closest : null;
    }

    // Ray–face intersection on a flat world: the top face is only seen from above. A miss past
    // the rim gives the rim point in that direction.
    private static Vector3? FacePointHit(Vector3 origin, Vector3 direction, bool nearest)
    {
        float faceHeight = (float)FlatDisc.HalfThickness;
        if (origin.Y <= faceHeight || direction.Y >= 0)
        {
            return null;
        }

        Vector3 point = origin + direction * ((faceHeight - origin.Y) / direction.Y);
        float across = new Vector2(point.X, point.Z).Length();
        if (across <= FlatDisc.Radius)
        {
            return point;
        }

        return nearest
            ? new Vector3(point.X / across * (float)FlatDisc.Radius, faceHeight,
                point.Z / across * (float)FlatDisc.Radius)
            : null;
    }
}
