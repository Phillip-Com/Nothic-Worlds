using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Geometry;

/// <summary>
/// How far apart two places are on a body's ground, in km: along a globe's surface, or
/// straight across a flat world's face (VISION.md BOD-02), which is the real ground there. On a
/// flat world, distances from the north pole match the globe's, but around the disc they're
/// longer the farther south they are.
/// </summary>
public static class SurfaceDistance
{
    /// <summary>The ground distance between two places, in km.</summary>
    /// <param name="shape">The body's shape.</param>
    /// <param name="radiusKm">The body's radius (for a flat world, its matching globe's).</param>
    public static double Km(BodyShape shape, double radiusKm, GeoCoordinate from, GeoCoordinate to)
    {
        if (shape != BodyShape.FlatDisc)
        {
            return double.DegreesToRadians(SphericalCoordinates.ArcDegrees(from, to)) * radiusKm;
        }

        Vector3D a = FlatDisc.TopPointFor(Direction(from));
        Vector3D b = FlatDisc.TopPointFor(Direction(to));
        return (a - b).Length * radiusKm;
    }

    // SphericalCoordinates.ToDirection's convention, in double precision.
    private static Vector3D Direction(GeoCoordinate coordinate)
    {
        double latitude = double.DegreesToRadians(coordinate.LatitudeDegrees);
        double longitude = double.DegreesToRadians(coordinate.LongitudeDegrees);
        return new Vector3D(Math.Cos(latitude) * Math.Sin(longitude), Math.Sin(latitude),
            Math.Cos(latitude) * Math.Cos(longitude));
    }
}
