namespace NothicWorlds.Core.Geometry;

/// <summary>
/// Which way and how far a place is from where the eye stands (VISION.md REN-06: pins on the
/// first-person compass): over a globe along the great circle, as <see cref="GlobeWalk"/>
/// walks, and over a flat world's face in a straight line, as <see cref="FlatWalk"/> does.
/// </summary>
public static class SurfaceBearing
{
    /// <summary>Which way to set off, and how far it is over the ground.</summary>
    /// <param name="BearingDegrees">Degrees clockwise from north, 0 to 360.</param>
    /// <param name="Km">The distance over the ground.</param>
    public readonly record struct Toward(double BearingDegrees, double Km);

    /// <summary>
    /// The way and distance from <paramref name="spot"/> (a unit direction, as
    /// <see cref="SphericalPolygon.ToUnit"/> makes) to <paramref name="target"/> on a globe of
    /// <paramref name="radiusKm"/>. North is <see cref="GlobeWalk.Tangents"/>'s.
    /// </summary>
    public static Toward OnGlobe(Vector3D spot, GeoCoordinate target, double radiusKm)
    {
        Vector3D there = SphericalPolygon.ToUnit(target);
        (Vector3D east, Vector3D north) = GlobeWalk.Tangents(spot);
        double angle = Math.Acos(Math.Clamp(spot.Dot(there), -1, 1));
        return new Toward(Degrees(there.Dot(east), there.Dot(north)), angle * radiusKm);
    }

    /// <summary>
    /// The way and distance from <paramref name="spot"/> to <paramref name="target"/> on a
    /// flat world (whose matching globe has <paramref name="radiusKm"/>), or null off the top
    /// face: places are only ever on the top, and the way there from the rim or underside
    /// bends over the edge.
    /// </summary>
    public static Toward? OnFlat(FlatSpot spot, GeoCoordinate target, double radiusKm)
    {
        if (spot.Face != FlatFace.Top)
        {
            return null;
        }

        Vector3D across = FlatDisc.TopPointFor(SphericalPolygon.ToUnit(target))
            - FlatWalk.Point(spot);
        (Vector3D east, Vector3D north, _) = FlatWalk.Frame(spot);
        return new Toward(Degrees(across.Dot(east), across.Dot(north)),
            across.Length * radiusKm);
    }

    // A bearing in degrees, 0 to 360, from how far a way goes east and north.
    private static double Degrees(double east, double north)
    {
        double degrees = double.RadiansToDegrees(Math.Atan2(east, north));
        return degrees < 0 ? degrees + 360 : degrees;
    }
}
