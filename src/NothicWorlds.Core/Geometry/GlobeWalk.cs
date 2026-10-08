namespace NothicWorlds.Core.Geometry;

/// <summary>
/// Moving over a globe (VISION.md REN-06, REN-08): from a spot, on a bearing, by an angle of
/// arc along the great circle, as walking and the minimap's travel do. Spots are unit
/// directions from the globe's middle, +Y north (see <see cref="SphericalCoordinates"/>).
/// </summary>
public static class GlobeWalk
{
    /// <summary>
    /// East and north along the ground at a spot (unit vectors); at a pole, where every way is
    /// south (or north), +X stands in for east.
    /// </summary>
    public static (Vector3D East, Vector3D North) Tangents(Vector3D up)
    {
        var east = new Vector3D(up.Z, 0, -up.X);
        if (east.Length < 1e-9)
        {
            east = new Vector3D(1, 0, 0);
        }

        east *= 1 / east.Length;
        var north = new Vector3D(
            up.Y * east.Z - up.Z * east.Y,
            up.Z * east.X - up.X * east.Z,
            up.X * east.Y - up.Y * east.X);
        return (east, north);
    }

    /// <summary>
    /// Where a walk of <paramref name="angle"/> radians of arc from <paramref name="spot"/>
    /// ends, heading <paramref name="bearing"/> radians clockwise from north.
    /// </summary>
    public static Vector3D Walk(Vector3D spot, double bearing, double angle)
    {
        (Vector3D east, Vector3D north) = Tangents(spot);
        Vector3D along = north * Math.Cos(bearing) + east * Math.Sin(bearing);
        Vector3D end = spot * Math.Cos(angle) + along * Math.Sin(angle);
        return end * (1 / end.Length);
    }

    /// <summary>
    /// Where a point seen straight down from above <paramref name="spot"/> (as on a map drawn
    /// that way) lies, <paramref name="distance"/> globe radii out from the middle on
    /// <paramref name="bearing"/>: an arc of asin(distance), clamped at the globe's edge.
    /// </summary>
    public static Vector3D FromOverhead(Vector3D spot, double bearing, double distance) =>
        Walk(spot, bearing, Math.Asin(Math.Clamp(distance, 0, 1)));
}
