namespace NothicWorlds.Core.Geometry;

/// <summary>
/// A position on the surface of a sphere, in degrees.
/// Latitude runs from -90 (south pole) to 90 (north pole). Longitude runs from -180 (inclusive)
/// to 180 (exclusive), with positive values to the east.
/// </summary>
public readonly record struct GeoCoordinate
{
    /// <summary>
    /// Creates a coordinate. Latitude is clamped to [-90, 90] and longitude is wrapped into
    /// [-180, 180), so any finite input produces a valid coordinate.
    /// </summary>
    /// <exception cref="ArgumentException">Either value is NaN or infinite.</exception>
    public GeoCoordinate(double latitudeDegrees, double longitudeDegrees)
    {
        if (!double.IsFinite(latitudeDegrees))
        {
            throw new ArgumentException(
                "Latitude must be a finite number.", nameof(latitudeDegrees));
        }

        LatitudeDegrees = Math.Clamp(latitudeDegrees, -90.0, 90.0);
        LongitudeDegrees = SphericalCoordinates.WrapLongitude(longitudeDegrees);
    }

    /// <summary>Degrees north (positive) or south (negative) of the equator.</summary>
    public double LatitudeDegrees { get; }

    /// <summary>Degrees east (positive) or west (negative) of longitude 0.</summary>
    public double LongitudeDegrees { get; }
}
