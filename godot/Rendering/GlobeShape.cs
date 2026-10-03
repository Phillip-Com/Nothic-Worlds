using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Rendering;

/// <summary>
/// Where a latitude/longitude sits on a globe of either shape (VISION.md BOD-02), in the
/// globe's own space: on a unit sphere, or on a flat world's top face (see
/// <see cref="FlatDisc"/>). Everything placed on a surface goes through here.
/// </summary>
public static class GlobeShape
{
    /// <summary>
    /// The point on the surface for a globe direction (from <see cref="SphericalCoordinates"/>
    /// or <see cref="SphericalPolygon"/>), lifted by <paramref name="lift"/> (1 is right on the
    /// surface; 1.003 is 0.3% of the radius above it).
    /// </summary>
    public static Vector3 SurfacePoint(BodyShape shape, Vector3D direction, float lift = 1.0f)
    {
        if (shape != BodyShape.FlatDisc)
        {
            Vector3D unit = direction * (1.0 / direction.Length);
            return new Vector3((float)unit.X, (float)unit.Y, (float)unit.Z) * lift;
        }

        Vector3D top = FlatDisc.TopPointFor(direction);
        return new Vector3((float)top.X, (float)top.Y + (lift - 1.0f), (float)top.Z);
    }

    /// <summary>Which way is up (away from the surface) at a surface point.</summary>
    public static Vector3 Outward(BodyShape shape, Vector3 surfacePoint) =>
        shape == BodyShape.FlatDisc ? Vector3.Up : surfacePoint.Normalized();

    /// <summary>
    /// The globe direction for a point on the surface (the inverse of
    /// <see cref="SurfacePoint"/>, ignoring lift).
    /// </summary>
    public static Vector3D DirectionAt(BodyShape shape, Vector3 surfacePoint)
    {
        var point = new Vector3D(surfacePoint.X, surfacePoint.Y, surfacePoint.Z);
        return shape == BodyShape.FlatDisc ? FlatDisc.DirectionFor(point) : point;
    }
}
