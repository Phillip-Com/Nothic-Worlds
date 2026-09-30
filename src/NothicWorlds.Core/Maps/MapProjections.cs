using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Maps;

/// <summary>
/// Converts between globe positions and positions in a map image for each
/// <see cref="MapProjection"/>. In every projection, the image's width spans longitude -180 to 180.
/// </summary>
/// <remarks>
/// <c>godot/Rendering/planet.gdshader</c> does the same math per pixel. Keep the two in sync.
/// </remarks>
public static class MapProjections
{
    /// <summary>
    /// The farthest latitude (north and south) that a Mercator map of the given shape reaches.
    /// For example, a 2:1 map reaches about 66.5°, and a 1:1 map about 85°.
    /// </summary>
    /// <param name="aspectRatio">Image width divided by height.</param>
    /// <exception cref="ArgumentOutOfRangeException">The aspect ratio isn't positive.</exception>
    public static double MercatorLatitudeLimit(double aspectRatio)
    {
        ValidateAspectRatio(aspectRatio);

        // The width spans 2π in Mercator units, so half the height spans π / aspectRatio.
        return double.RadiansToDegrees(Math.Atan(Math.Sinh(Math.PI / aspectRatio)));
    }

    /// <summary>
    /// Returns where a globe position falls in a map image. Positions beyond the map's coverage
    /// return the nearest edge, flagged with <see cref="MapImagePosition.IsOutsideMap"/>.
    /// </summary>
    /// <param name="coordinate">The position on the globe.</param>
    /// <param name="projection">How the map image is laid out.</param>
    /// <param name="aspectRatio">Image width divided by height.</param>
    /// <exception cref="ArgumentOutOfRangeException">The aspect ratio isn't positive.</exception>
    public static MapImagePosition ToImagePosition(
        GeoCoordinate coordinate, MapProjection projection, double aspectRatio)
    {
        ValidateAspectRatio(aspectRatio);

        double u = coordinate.LongitudeDegrees / 360.0 + 0.5;
        double v = projection switch
        {
            MapProjection.Mercator => MercatorV(coordinate.LatitudeDegrees, aspectRatio),
            _ => 0.5 - coordinate.LatitudeDegrees / 180.0,
        };

        bool isOutside = v < 0.0 || v > 1.0;
        return new MapImagePosition(u, Math.Clamp(v, 0.0, 1.0), isOutside);
    }

    private static double MercatorV(double latitudeDegrees, double aspectRatio)
    {
        // Mercator's vertical position grows without limit toward the poles (infinite at 90°).
        // Clamping is fine because anything past the limit is outside the map anyway.
        double latitude = double.DegreesToRadians(Math.Clamp(latitudeDegrees, -89.9, 89.9));
        double y = Math.Log(Math.Tan(Math.PI / 4.0 + latitude / 2.0));

        // y is in the same units as longitude in radians, which spans 2π across the image width.
        return 0.5 - y / (2.0 * Math.PI) * aspectRatio;
    }

    private static void ValidateAspectRatio(double aspectRatio)
    {
        if (!double.IsFinite(aspectRatio) || aspectRatio <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(aspectRatio), aspectRatio, "Aspect ratio must be a positive number.");
        }
    }
}
