using System.Numerics;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Maps;

/// <summary>
/// Lays a flat map piece onto the globe like a sticker (VISION.md MAP-02), and back. Distances
/// from the piece's center stay true (an azimuthal equidistant projection around the center),
/// so small pieces look exactly as drawn and large ones stretch gently toward their edges.
/// </summary>
/// <remarks>
/// Positions are within the piece's bounding box: (0, 0) top-left to (1, 1) bottom-right.
/// <c>godot/Rendering/planet.gdshader</c> does the forward direction per pixel, using
/// <see cref="CenterDirection"/>, <see cref="EastDirection"/> and <see cref="NorthDirection"/>.
/// Keep the two in sync.
/// </remarks>
public sealed class PieceProjection
{
    /// <summary>The smallest a piece can be, in degrees across.</summary>
    public const double MinimumWidthDegrees = 0.1;

    /// <summary>The largest a piece can be: half the globe across.</summary>
    public const double MaximumWidthDegrees = 180.0;

    private readonly (double X, double Y, double Z) _center;
    private readonly (double X, double Y, double Z) _east;
    private readonly (double X, double Y, double Z) _north;
    private readonly double _cosRotation;
    private readonly double _sinRotation;
    private readonly double _width;
    private readonly double _height;

    /// <param name="center">Where the center of the piece's bounding box sits.</param>
    /// <param name="rotationDegrees">Clockwise turn from "up = north".</param>
    /// <param name="widthDegrees">Arc spanned left to right.</param>
    /// <param name="boxAspectRatio">The bounding box's width divided by its height.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size or angle is invalid.</exception>
    public PieceProjection(
        GeoCoordinate center, double rotationDegrees, double widthDegrees, double boxAspectRatio)
    {
        if (!double.IsFinite(widthDegrees)
            || widthDegrees is < MinimumWidthDegrees or > MaximumWidthDegrees)
        {
            throw new ArgumentOutOfRangeException(nameof(widthDegrees), widthDegrees,
                $"Width must be {MinimumWidthDegrees}° to {MaximumWidthDegrees}°.");
        }

        if (!double.IsFinite(boxAspectRatio) || boxAspectRatio <= 0
            || !double.IsFinite(rotationDegrees))
        {
            throw new ArgumentOutOfRangeException(
                nameof(boxAspectRatio), "Aspect ratio and rotation must be valid numbers.");
        }

        double lat = double.DegreesToRadians(center.LatitudeDegrees);
        double lon = double.DegreesToRadians(center.LongitudeDegrees);
        double rotation = double.DegreesToRadians(rotationDegrees);

        // Unit vectors at the center, in the axes of SphericalCoordinates: straight out, east,
        // and north.
        _center = (Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat), Math.Cos(lat) * Math.Cos(lon));
        _east = (Math.Cos(lon), 0.0, -Math.Sin(lon));
        _north = (-Math.Sin(lat) * Math.Sin(lon), Math.Cos(lat), -Math.Sin(lat) * Math.Cos(lon));
        _cosRotation = Math.Cos(rotation);
        _sinRotation = Math.Sin(rotation);
        _width = double.DegreesToRadians(widthDegrees);
        _height = _width / boxAspectRatio;
    }

    /// <summary>The piece's height across, in degrees of arc.</summary>
    public double HeightDegrees => double.RadiansToDegrees(_height);

    /// <summary>The piece's width across, in radians of arc (for the shader).</summary>
    public double WidthRadians => _width;

    /// <summary>The piece's height across, in radians of arc (for the shader).</summary>
    public double HeightRadians => _height;

    /// <summary>Cosine of the clockwise rotation (for the shader).</summary>
    public double CosRotation => _cosRotation;

    /// <summary>Sine of the clockwise rotation (for the shader).</summary>
    public double SinRotation => _sinRotation;

    /// <summary>
    /// How far the piece's corners reach from its center, in radians of arc. Distances are true
    /// in this projection, so nothing of the piece lies farther away than this.
    /// </summary>
    public double ReachRadians => Math.Sqrt(_width * _width + _height * _height) / 2.0;

    /// <summary>Unit vector from the globe's center to the piece's center.</summary>
    public Vector3 CenterDirection => ToVector(_center);

    /// <summary>Unit vector pointing east at the piece's center.</summary>
    public Vector3 EastDirection => ToVector(_east);

    /// <summary>Unit vector pointing north at the piece's center.</summary>
    public Vector3 NorthDirection => ToVector(_north);

    /// <summary>Creates the projection for a placed piece.</summary>
    public static PieceProjection For(MapPiece piece)
    {
        return new PieceProjection(
            piece.Center, piece.RotationDegrees, piece.WidthDegrees, piece.Outline.BoxAspectRatio);
    }

    /// <summary>
    /// Where a globe position falls within the piece's bounding box. Positions outside the box
    /// are flagged with <see cref="MapImagePosition.IsOutsideMap"/> (U and V are then not clamped).
    /// </summary>
    public MapImagePosition ToBoxPosition(GeoCoordinate coordinate)
    {
        // Full precision (SphericalCoordinates gives single-precision vectors).
        double lat = double.DegreesToRadians(coordinate.LatitudeDegrees);
        double lon = double.DegreesToRadians(coordinate.LongitudeDegrees);
        (double X, double Y, double Z) p =
            (Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat), Math.Cos(lat) * Math.Cos(lon));

        double cosDistance = Dot(p, _center);
        double east = Dot(p, _east);
        double north = Dot(p, _north);
        double sideways = Math.Sqrt(east * east + north * north);  // = sin(distance)

        // atan2 of sine and cosine stays accurate at every distance; acos(cosine) alone loses
        // precision close to the center, which misplaces small pieces noticeably.
        double distance = Math.Atan2(sideways, cosDistance);

        // Local position in radians of arc: east (x) and north (y) of the center.
        double x = sideways < 1e-12 ? 0.0 : distance * east / sideways;
        double y = sideways < 1e-12 ? 0.0 : distance * north / sideways;

        // Undo the piece's clockwise rotation to get its own right (xp) and up (yp).
        double xp = x * _cosRotation - y * _sinRotation;
        double yp = x * _sinRotation + y * _cosRotation;

        double u = 0.5 + xp / _width;
        double v = 0.5 - yp / _height;
        bool outside = u is < 0 or > 1 || v is < 0 or > 1 || distance > Math.PI - 1e-9;
        return new MapImagePosition(u, v, outside);
    }

    /// <summary>
    /// The globe position of a point in the piece's bounding box (e.g. a corner).
    /// </summary>
    public GeoCoordinate FromBoxPosition(double u, double v)
    {
        double xp = (u - 0.5) * _width;
        double yp = (0.5 - v) * _height;
        double x = xp * _cosRotation + yp * _sinRotation;
        double y = -xp * _sinRotation + yp * _cosRotation;

        double distance = Math.Sqrt(x * x + y * y);
        if (distance < 1e-12)
        {
            distance = 1e-12;  // At the center; the formula below then returns the center.
        }

        double s = Math.Sin(distance) / distance;
        double c = Math.Cos(distance);
        double px = c * _center.X + s * (x * _east.X + y * _north.X);
        double py = c * _center.Y + s * (x * _east.Y + y * _north.Y);
        double pz = c * _center.Z + s * (x * _east.Z + y * _north.Z);

        // Full-precision latitude/longitude (SphericalCoordinates works in single precision).
        double latitude = Math.Asin(Math.Clamp(py, -1.0, 1.0));
        double longitude = Math.Atan2(px, pz);
        return new GeoCoordinate(
            double.RadiansToDegrees(latitude), double.RadiansToDegrees(longitude));
    }

    private static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b)
    {
        return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    }

    private static Vector3 ToVector((double X, double Y, double Z) v)
    {
        return new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
