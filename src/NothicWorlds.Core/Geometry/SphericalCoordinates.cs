using System.Numerics;

namespace NothicWorlds.Core.Geometry;

/// <summary>
/// Converts between latitude/longitude and 3D directions from a sphere's center.
/// </summary>
/// <remarks>
/// Axis convention (matches Godot, and must match <c>godot/Rendering/planet.gdshader</c>):
/// +Y points to the north pole, longitude 0 faces +Z, and longitude 90° east faces +X.
/// </remarks>
public static class SphericalCoordinates
{
    /// <summary>
    /// Returns the unit-length direction from the sphere's center to a coordinate.
    /// </summary>
    public static Vector3 ToDirection(GeoCoordinate coordinate)
    {
        double latitude = double.DegreesToRadians(coordinate.LatitudeDegrees);
        double longitude = double.DegreesToRadians(coordinate.LongitudeDegrees);
        double cosLatitude = Math.Cos(latitude);

        return new Vector3(
            (float)(cosLatitude * Math.Sin(longitude)),
            (float)Math.Sin(latitude),
            (float)(cosLatitude * Math.Cos(longitude)));
    }

    /// <summary>
    /// Returns the coordinate a direction points at. The direction doesn't need to be unit
    /// length. At the exact poles, longitude is undefined and 0 is returned.
    /// </summary>
    /// <exception cref="ArgumentException">The direction is zero-length or not finite.</exception>
    public static GeoCoordinate FromDirection(Vector3 direction)
    {
        double length = direction.Length();
        if (length == 0.0 || !double.IsFinite(length))
        {
            throw new ArgumentException(
                "Direction must be a finite, non-zero vector.", nameof(direction));
        }

        double latitude = Math.Asin(Math.Clamp(direction.Y / length, -1.0, 1.0));
        double longitude = Math.Atan2(direction.X, direction.Z);

        return new GeoCoordinate(
            double.RadiansToDegrees(latitude), double.RadiansToDegrees(longitude));
    }

    /// <summary>Wraps any longitude into the range [-180, 180).</summary>
    /// <exception cref="ArgumentException">The value is NaN or infinite.</exception>
    public static double WrapLongitude(double longitudeDegrees)
    {
        if (!double.IsFinite(longitudeDegrees))
        {
            throw new ArgumentException(
                "Longitude must be a finite number.", nameof(longitudeDegrees));
        }

        double wrapped = (longitudeDegrees + 180.0) % 360.0;
        if (wrapped < 0.0)
        {
            wrapped += 360.0;
        }

        // Adding 360 to a tiny negative remainder can round up to exactly 360.
        if (wrapped >= 360.0)
        {
            wrapped -= 360.0;
        }

        return wrapped - 180.0;
    }

    /// <summary>
    /// The angle between two points as seen from the sphere's center (the great-circle distance
    /// on a unit sphere), in degrees: 0 to 180.
    /// </summary>
    public static double ArcDegrees(GeoCoordinate from, GeoCoordinate to)
    {
        double lat1 = double.DegreesToRadians(from.LatitudeDegrees);
        double lat2 = double.DegreesToRadians(to.LatitudeDegrees);
        double deltaLon = double.DegreesToRadians(
            LongitudeDelta(from.LongitudeDegrees, to.LongitudeDegrees));

        // Haversine formula: accurate for short and long distances alike.
        double h = Math.Pow(Math.Sin((lat2 - lat1) / 2), 2)
            + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(deltaLon / 2), 2);
        return double.RadiansToDegrees(2 * Math.Asin(Math.Min(1.0, Math.Sqrt(h))));
    }

    /// <summary>
    /// The compass direction to set off in from <paramref name="from"/> to reach
    /// <paramref name="to"/> along a great circle, in degrees clockwise from north: 0 to 360.
    /// Returns 0 when the points coincide.
    /// </summary>
    public static double BearingDegrees(GeoCoordinate from, GeoCoordinate to)
    {
        double lat1 = double.DegreesToRadians(from.LatitudeDegrees);
        double lat2 = double.DegreesToRadians(to.LatitudeDegrees);
        double deltaLon = double.DegreesToRadians(
            LongitudeDelta(from.LongitudeDegrees, to.LongitudeDegrees));

        double east = Math.Sin(deltaLon) * Math.Cos(lat2);
        double north = Math.Cos(lat1) * Math.Sin(lat2)
            - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(deltaLon);
        double bearing = double.RadiansToDegrees(Math.Atan2(east, north));
        return bearing < 0 ? bearing + 360.0 : bearing;
    }

    /// <summary>
    /// Returns the shortest signed change in longitude from <paramref name="fromDegrees"/> to
    /// <paramref name="toDegrees"/>, in [-180, 180). For example, 170 → -170 is +20 (east
    /// across the date line), not -340.
    /// </summary>
    public static double LongitudeDelta(double fromDegrees, double toDegrees)
    {
        return WrapLongitude(toDegrees - fromDegrees);
    }
}
