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
        PlanetCamera camera, Node3D planet, Vector2 screen, bool nearestWhenMissed = false) =>
        PointAt(camera, planet, screen, nearestWhenMissed) is Vector3 hit
            ? SphericalCoordinates.FromDirection(
                GlobeShape.DirectionAt(ShapeOf(planet), hit).ToNumerics())
            : null;

    /// <summary>
    /// The drawn surface point under a screen position, in the planet's own space (radii), or
    /// null if it misses; see <see cref="CoordinateAt"/>. On a globe carved by shapes
    /// (VISION.md BOD-04) it's the carving's surface, inside holes and hollows too.
    /// </summary>
    public static Vector3? PointAt(
        PlanetCamera camera, Node3D planet, Vector2 screen, bool nearestWhenMissed = false)
    {
        Transform3D toPlanet = planet.GlobalTransform.AffineInverse();
        Vector3 origin = toPlanet * camera.ProjectRayOrigin(screen);
        Vector3 direction = (toPlanet.Basis * camera.ProjectRayNormal(screen)).Normalized();
        if (planet is PlanetSurface { IsCarved: true } carved
            && carved.CarvedHit(origin, direction) is Vector3 inside)
        {
            return inside;
        }

        if (ShapeOf(planet) == BodyShape.FlatDisc)
        {
            return FacePointHit(planet as PlanetSurface, origin, direction, nearestWhenMissed);
        }

        return SculptedPointHit(planet, origin, direction, nearestWhenMissed);
    }

    /// <summary>
    /// Where a surface point appears on screen, or null if it's hidden (on the far side of the
    /// planet, or behind the camera).
    /// </summary>
    public static Vector2? ScreenPositionOf(
        PlanetCamera camera, Node3D planet, GeoCoordinate coordinate)
    {
        BodyShape shape = ShapeOf(planet);
        Vector3D toSpot = SphericalCoordinates.ToDirection(coordinate).ToVector3D();
        Vector3 local = GlobeShape.SurfacePoint(shape, toSpot) * RadiusAt(planet, toSpot);
        Vector3 world = planet.GlobalTransform * local;
        Vector3 outward = planet.GlobalTransform.Basis * GlobeShape.Outward(shape, local);
        bool facesCamera = outward.Dot(camera.GlobalPosition - world) > 0;
        return facesCamera && !camera.IsPositionBehind(world)
            ? camera.UnprojectPosition(world)
            : null;
    }

    private static BodyShape ShapeOf(Node3D planet) =>
        planet is PlanetSurface surface ? surface.Shape : BodyShape.Sphere;

    // How far out the drawn surface is there (sculpted relief, VISION.md BOD-04), in radii.
    private static float RadiusAt(Node3D planet, Vector3D direction) =>
        planet is PlanetSurface surface ? surface.SurfaceRadiusAt(direction) : 1.0f;

    // Where the ray meets the sculpted surface: it meets a sphere of the height found at the
    // last try's spot, a few times over, which settles on the ground's height there.
    private static Vector3? SculptedPointHit(
        Node3D planet, Vector3 origin, Vector3 direction, bool nearest)
    {
        float radius = 1.0f;
        Vector3? point = null;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            point = SpherePointHit(origin, direction, nearest, radius);
            if (point is not Vector3 hit || hit.LengthSquared() == 0)
            {
                break;
            }

            float there = RadiusAt(planet, new Vector3D(hit.X, hit.Y, hit.Z));
            if (Mathf.Abs(there - radius) < 1e-6f)
            {
                break;
            }

            radius = there;
        }

        return point;
    }

    // Ray–sphere intersection (in the planet's own space): `along` is the ray's closest
    // approach to the center.
    private static Vector3? SpherePointHit(
        Vector3 origin, Vector3 direction, bool nearest, float radius)
    {
        float along = -origin.Dot(direction);
        Vector3 closest = origin + direction * along;
        float missSquared = closest.LengthSquared();
        if (missSquared <= radius * radius)
        {
            return origin + direction * (along - Mathf.Sqrt(radius * radius - missSquared));
        }

        return nearest && closest.LengthSquared() > 0 ? closest : null;
    }

    // Ray–face intersection on a flat world: the top face is only seen from above. Where the
    // ground is lifted (VISION.md BOD-10), the ray meets the face at the height of the ground
    // where it last met it, a few times over, which settles on the ground's height. A miss past
    // the rim gives the rim point in that direction.
    private static Vector3? FacePointHit(
        PlanetSurface? surface, Vector3 origin, Vector3 direction, bool nearest)
    {
        float faceHeight = (float)FlatDisc.HalfThickness;
        if (origin.Y <= faceHeight || direction.Y >= 0)
        {
            return null;
        }

        Vector3 point = origin + direction * ((faceHeight - origin.Y) / direction.Y);
        for (int pass = 0; surface is not null && pass < 4; pass++)
        {
            Vector3D at = FlatDisc.DirectionFor(new Vector3D(point.X, point.Y, point.Z));
            float groundHeight = faceHeight + surface.SurfaceRadiusAt(at) - 1;
            if (origin.Y <= groundHeight)
            {
                break;
            }

            point = origin + direction * ((groundHeight - origin.Y) / direction.Y);
        }

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
