using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Maps;

/// <summary>
/// Converts globe positions to positions in a map image for each <see cref="MapProjection"/>
/// (VISION.md MAP-01, MAP-03, MAP-04), and knows each projection's expected image shape.
/// </summary>
/// <remarks>
/// <para>Each projection turns latitude/longitude into x/y, which are then scaled to fill the
/// image. Images of a different shape are stretched to fit. The exception is Mercator, whose
/// latitude coverage depends on the image's shape.</para>
/// <para><c>godot/Rendering/planet.gdshader</c> does the same math per pixel. Keep the two in
/// sync.</para>
/// </remarks>
public static class MapProjections
{
    /// <summary>
    /// How far (as a fraction) an image's shape may be from its projection's expected shape and
    /// still count as matching. Globe maps use <see cref="MapImageRules.AspectRatioTolerance"/>.
    /// Templates for atlas and circular maps are often exported at slightly different sizes, so
    /// they get more leeway.
    /// </summary>
    public const double AtlasAspectRatioTolerance = 0.05;

    private const double HalfPi = Math.PI / 2.0;

    // Robinson's projection is defined by a table at every 5° of latitude (0° to 90°):
    // relative parallel length and relative distance from the equator.
    private static readonly double[] _robinsonLength =
    [
        1.0000, 0.9986, 0.9954, 0.9900, 0.9822, 0.9730, 0.9600, 0.9427, 0.9216, 0.8962,
        0.8679, 0.8350, 0.7986, 0.7597, 0.7186, 0.6732, 0.6213, 0.5722, 0.5322,
    ];

    private static readonly double[] _robinsonHeight =
    [
        0.0000, 0.0620, 0.1240, 0.1860, 0.2480, 0.3100, 0.3720, 0.4340, 0.4958, 0.5571,
        0.6176, 0.6769, 0.7346, 0.7903, 0.8435, 0.8936, 0.9394, 0.9761, 1.0000,
    ];

    private const double RobinsonWidthScale = 0.8487;
    private const double RobinsonHeightScale = 1.3523;

    // Winkel tripel's standard parallel: acos(2/π).
    private static readonly double _winkelCosStandardParallel = 2.0 / Math.PI;

    /// <summary>
    /// The image shape (width ÷ height) a projection's maps normally have, or null for Mercator,
    /// where any shape works.
    /// </summary>
    public static double? ExpectedAspectRatio(MapProjection projection)
    {
        return projection switch
        {
            MapProjection.Equirectangular => MapImageRules.SupportedAspectRatio,
            MapProjection.Mercator => null,
            MapProjection.Robinson =>
                RobinsonWidthScale * Math.PI / RobinsonHeightScale,       // ≈ 1.97
            MapProjection.WinkelTripel => (1.0 + HalfPi) / HalfPi,         // ≈ 1.64
            MapProjection.Mollweide => 2.0,
            MapProjection.GallPeters => HalfPi,                             // ≈ 1.57
            MapProjection.Polar => 1.0,
            MapProjection.TwoHemispheres => 2.0,
            _ => throw new ArgumentOutOfRangeException(nameof(projection), projection, null),
        };
    }

    /// <summary>
    /// False for map types that leave part of the globe uncovered (a flat map's polar caps, a
    /// polar map's southern hemisphere). Those parts use the fill color.
    /// </summary>
    public static bool CoversWholeGlobe(MapProjection projection)
    {
        return projection is not (MapProjection.Mercator or MapProjection.Polar);
    }

    /// <summary>
    /// True if an image of the given shape matches what the projection expects (within
    /// tolerance). Mismatched images still work, but they look stretched.
    /// </summary>
    /// <param name="projection">How the map image is laid out.</param>
    /// <param name="aspectRatio">Image width divided by height.</param>
    /// <exception cref="ArgumentOutOfRangeException">The aspect ratio isn't positive.</exception>
    public static bool ShapeMatches(MapProjection projection, double aspectRatio)
    {
        ValidateAspectRatio(aspectRatio);

        if (ExpectedAspectRatio(projection) is not double expected)
        {
            return true;
        }

        double tolerance = projection == MapProjection.Equirectangular
            ? MapImageRules.AspectRatioTolerance
            : AtlasAspectRatioTolerance;
        return Math.Abs(aspectRatio / expected - 1.0) <= tolerance;
    }

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
    /// Returns where a globe position falls in a map image. Positions the map doesn't cover
    /// (a flat map's polar caps, a polar map's southern hemisphere) return the nearest edge,
    /// flagged with <see cref="MapImagePosition.IsOutsideMap"/>.
    /// </summary>
    /// <param name="coordinate">The position on the globe.</param>
    /// <param name="projection">How the map image is laid out.</param>
    /// <param name="aspectRatio">Image width divided by height. Only Mercator uses it.</param>
    /// <exception cref="ArgumentOutOfRangeException">The aspect ratio isn't positive.</exception>
    public static MapImagePosition ToImagePosition(
        GeoCoordinate coordinate, MapProjection projection, double aspectRatio)
    {
        ValidateAspectRatio(aspectRatio);

        double latitude = double.DegreesToRadians(coordinate.LatitudeDegrees);
        double longitude = double.DegreesToRadians(coordinate.LongitudeDegrees);

        return projection switch
        {
            MapProjection.Equirectangular => FromBox(longitude, latitude, Math.PI, HalfPi),
            MapProjection.Mercator => Mercator(latitude, longitude, aspectRatio),
            MapProjection.Robinson => Robinson(latitude, longitude),
            MapProjection.WinkelTripel => WinkelTripel(latitude, longitude),
            MapProjection.Mollweide => Mollweide(latitude, longitude),
            MapProjection.GallPeters => FromBox(
                longitude / Math.Sqrt(2.0),
                Math.Sqrt(2.0) * Math.Sin(latitude),
                Math.PI / Math.Sqrt(2.0),
                Math.Sqrt(2.0)),
            MapProjection.Polar => Polar(latitude, longitude),
            MapProjection.TwoHemispheres => TwoHemispheres(latitude, longitude),
            _ => throw new ArgumentOutOfRangeException(nameof(projection), projection, null),
        };
    }

    // Scales projected (x, y), centered on (0, 0) and spanning ±halfWidth × ±halfHeight, to fill
    // the image.
    private static MapImagePosition FromBox(double x, double y, double halfWidth, double halfHeight)
    {
        double u = 0.5 + x / (2.0 * halfWidth);
        double v = 0.5 - y / (2.0 * halfHeight);
        return new MapImagePosition(u, v, false);
    }

    private static MapImagePosition Mercator(double latitude, double longitude, double aspectRatio)
    {
        // Mercator grows without limit toward the poles (infinite at 90°). Clamping is fine
        // because anything that far is outside the map anyway.
        double limited = Math.Clamp(latitude, -HalfPi * 0.999, HalfPi * 0.999);
        double y = Math.Log(Math.Tan(Math.PI / 4.0 + limited / 2.0));

        // The width spans 2π, so the height spans 2π / aspectRatio.
        double u = 0.5 + longitude / (2.0 * Math.PI);
        double v = 0.5 - y / (2.0 * Math.PI) * aspectRatio;
        bool isOutside = v < 0.0 || v > 1.0;
        return new MapImagePosition(u, Math.Clamp(v, 0.0, 1.0), isOutside);
    }

    private static MapImagePosition Robinson(double latitude, double longitude)
    {
        // Linear interpolation between the 5° table entries.
        double index = Math.Min(Math.Abs(double.RadiansToDegrees(latitude)) / 5.0, 18.0);
        int lower = Math.Min((int)index, 17);
        double t = index - lower;
        double length = Lerp(_robinsonLength[lower], _robinsonLength[lower + 1], t);
        double height = Lerp(_robinsonHeight[lower], _robinsonHeight[lower + 1], t);

        double x = RobinsonWidthScale * length * longitude;
        double y = RobinsonHeightScale * height * Math.Sign(latitude);
        return FromBox(x, y, RobinsonWidthScale * Math.PI, RobinsonHeightScale);
    }

    private static MapImagePosition WinkelTripel(double latitude, double longitude)
    {
        double alpha = Math.Acos(Math.Cos(latitude) * Math.Cos(longitude / 2.0));
        // sinc(α) = sin(α) / α, which approaches 1 as α approaches 0.
        double sinc = alpha < 1e-9 ? 1.0 : Math.Sin(alpha) / alpha;

        double x = 0.5 * (longitude * _winkelCosStandardParallel
            + 2.0 * Math.Cos(latitude) * Math.Sin(longitude / 2.0) / sinc);
        double y = 0.5 * (latitude + Math.Sin(latitude) / sinc);
        return FromBox(x, y, 1.0 + HalfPi, HalfPi);
    }

    private static MapImagePosition Mollweide(double latitude, double longitude)
    {
        double theta = MollweideTheta(latitude);
        double x = 2.0 * Math.Sqrt(2.0) / Math.PI * longitude * Math.Cos(theta);
        double y = Math.Sqrt(2.0) * Math.Sin(theta);
        return FromBox(x, y, 2.0 * Math.Sqrt(2.0), Math.Sqrt(2.0));
    }

    // Solves 2θ + sin(2θ) = π·sin(latitude) with Newton's method.
    private static double MollweideTheta(double latitude)
    {
        if (Math.Abs(latitude) >= HalfPi - 1e-9)
        {
            return Math.Sign(latitude) * HalfPi;
        }

        double target = Math.PI * Math.Sin(latitude);
        double doubleTheta = 2.0 * latitude;
        for (int i = 0; i < 50; i++)
        {
            double step = (doubleTheta + Math.Sin(doubleTheta) - target)
                / (1.0 + Math.Cos(doubleTheta));
            doubleTheta -= step;
            if (Math.Abs(step) < 1e-12)
            {
                break;
            }
        }

        return doubleTheta / 2.0;
    }

    private static MapImagePosition Polar(double latitude, double longitude)
    {
        // Distance from the north pole, scaled so the equator is the circle's edge. Longitude 0
        // points down, and east runs counterclockwise (as seen looking down on the north pole).
        bool isOutside = latitude < 0.0;
        double radius = (HalfPi - Math.Max(latitude, 0.0)) / HalfPi;
        double x = radius * Math.Sin(longitude);
        double y = -radius * Math.Cos(longitude);
        return FromBox(x, y, 1.0, 1.0) with { IsOutsideMap = isOutside };
    }

    private static MapImagePosition TwoHemispheres(double latitude, double longitude)
    {
        // Western hemisphere (longitude below 0) in the left half, eastern in the right.
        bool isWest = longitude < 0.0;
        double center = isWest ? -HalfPi : HalfPi;
        (double x, double y) = AzimuthalEquidistant(latitude, longitude - center);

        // Each circle is half the image wide: x and y are within ±1.
        double u = (isWest ? 0.25 : 0.75) + x * 0.25;
        double v = 0.5 - y * 0.5;
        return new MapImagePosition(u, v, false);
    }

    // Azimuthal equidistant projection centered on the equator at relative longitude 0, scaled
    // so a quarter turn (the hemisphere's edge) has radius 1.
    private static (double X, double Y) AzimuthalEquidistant(
        double latitude, double relativeLongitude)
    {
        double cosDistance = Math.Cos(latitude) * Math.Cos(relativeLongitude);
        double distance = Math.Acos(Math.Clamp(cosDistance, -1.0, 1.0));
        // distance / sin(distance) approaches 1 at the center.
        double scale = distance < 1e-9 ? 1.0 : distance / Math.Sin(distance);

        double x = scale * Math.Cos(latitude) * Math.Sin(relativeLongitude);
        double y = scale * Math.Sin(latitude);
        return (x / HalfPi, y / HalfPi);
    }

    private static double Lerp(double from, double to, double t)
    {
        return from + (to - from) * t;
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
