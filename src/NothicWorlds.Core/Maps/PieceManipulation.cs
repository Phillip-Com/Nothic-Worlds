using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Maps;

/// <summary>
/// The math behind handling pieces directly on the globe (VISION.md MAP-02): finding the piece
/// under the mouse, and turning a drag into a move, a turn, or a resize. Each drag is computed
/// from where it started (not step by step), so long drags don't drift.
/// </summary>
public static class PieceManipulation
{
    /// <summary>
    /// The topmost piece whose cut-out shape covers <paramref name="point"/>, or null. Clicks on
    /// the transparent part of a freeform piece's box go through to what's beneath.
    /// </summary>
    /// <param name="pieces">The planet's pieces, bottom to top.</param>
    /// <param name="point">The position on the globe.</param>
    /// <param name="lookupFor">
    /// The baked lookup of each warped piece (see <see cref="PieceWarp.BakeLookup"/>), if the
    /// caller keeps them; otherwise they're baked as needed.
    /// </param>
    public static MapPiece? PieceAt(
        IReadOnlyList<MapPiece> pieces,
        GeoCoordinate point,
        Func<MapPiece, WarpLookup?>? lookupFor = null)
    {
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            MapPiece piece = pieces[i];
            WarpLookup? lookup = piece.WarpedPoints is null
                ? null
                : lookupFor?.Invoke(piece) ?? PieceWarp.For(piece)!.BakeLookup();
            if (UnwarpedBoxPosition(piece, lookup, point) is ImagePoint box
                && piece.Outline.Contains(ToImage(piece.Outline, box)))
            {
                return piece;
            }
        }

        return null;
    }

    // Where a globe position falls in the piece's unwarped box, or null if it's off the piece.
    private static ImagePoint? UnwarpedBoxPosition(
        MapPiece piece, WarpLookup? lookup, GeoCoordinate point)
    {
        MapImagePosition box = PieceProjection.For(piece).ToBoxPosition(point);
        if (lookup is not null)
        {
            return lookup.Sample(new ImagePoint(box.U, box.V));
        }

        return box.IsOutsideMap ? null : new ImagePoint(box.U, box.V);
    }

    private static ImagePoint ToImage(PieceOutline outline, ImagePoint box)
    {
        return new ImagePoint(
            outline.MinU + box.U * (outline.MaxU - outline.MinU),
            outline.MinV + box.V * (outline.MaxV - outline.MinV));
    }

    /// <summary>
    /// Slides a piece so the point grabbed ends up at <paramref name="target"/>, rolling it
    /// along the globe without twisting (like sliding a sticker across a ball). Returns the new
    /// center and rotation.
    /// </summary>
    /// <param name="center">The piece's center when the drag started.</param>
    /// <param name="rotationDegrees">The piece's rotation when the drag started.</param>
    /// <param name="grabbed">Where the drag started on the globe.</param>
    /// <param name="target">Where the mouse is now on the globe.</param>
    public static (GeoCoordinate Center, double RotationDegrees) Move(
        GeoCoordinate center, double rotationDegrees, GeoCoordinate grabbed, GeoCoordinate target)
    {
        Vec from = Vec.From(grabbed);
        Vec to = Vec.From(target);
        Vec axis = from.Cross(to);
        double sinAngle = axis.Length;
        if (sinAngle < 1e-12)
        {
            return (center, rotationDegrees);  // No movement (or exactly opposite; ambiguous).
        }

        axis /= sinAngle;
        double angle = Math.Atan2(sinAngle, from.Dot(to));

        // Carry the center and the piece's "up" direction around the same axis, then read the
        // new rotation off the transported "up".
        Vec oldCenter = Vec.From(center);
        Vec up = UpDirection(oldCenter, rotationDegrees);
        Vec newCenter = oldCenter.RotatedAround(axis, angle);
        Vec newUp = up.RotatedAround(axis, angle);
        (Vec east, Vec north) = LocalAxes(newCenter);
        double rotation = double.RadiansToDegrees(Math.Atan2(newUp.Dot(east), newUp.Dot(north)));
        return (newCenter.ToCoordinate(), NormalizeRotation(rotation));
    }

    /// <summary>
    /// Turns a piece around its center by how far the mouse has swept around it since the drag
    /// started. Returns the new rotation, 0 to 360.
    /// </summary>
    public static double Rotate(
        GeoCoordinate center, double rotationDegrees, GeoCoordinate grabbed, GeoCoordinate target)
    {
        double swept = SphericalCoordinates.BearingDegrees(center, target)
            - SphericalCoordinates.BearingDegrees(center, grabbed);
        return NormalizeRotation(rotationDegrees + swept);
    }

    /// <summary>
    /// Resizes a piece around its center, keeping its proportions, in step with the mouse's
    /// distance from the center: dragging a corner twice as far out doubles the width. The
    /// result is kept within the allowed piece sizes.
    /// </summary>
    public static double Resize(
        GeoCoordinate center, double widthDegrees, GeoCoordinate grabbed, GeoCoordinate target)
    {
        double startDistance = SphericalCoordinates.ArcDegrees(center, grabbed);
        if (startDistance < 1e-9)
        {
            return widthDegrees;
        }

        double scale = SphericalCoordinates.ArcDegrees(center, target) / startDistance;
        return Math.Clamp(widthDegrees * scale,
            PieceProjection.MinimumWidthDegrees, PieceProjection.MaximumWidthDegrees);
    }

    /// <summary>Wraps any angle into 0 (inclusive) to 360 (exclusive).</summary>
    public static double NormalizeRotation(double degrees)
    {
        double wrapped = degrees % 360.0;
        wrapped = wrapped < 0 ? wrapped + 360.0 : wrapped;
        return wrapped >= 360.0 ? 0.0 : wrapped;  // A tiny negative can round up to 360.
    }

    // The direction the piece's top faces, at its center: north turned clockwise.
    private static Vec UpDirection(Vec center, double rotationDegrees)
    {
        (Vec east, Vec north) = LocalAxes(center);
        double rotation = double.DegreesToRadians(rotationDegrees);
        return north * Math.Cos(rotation) + east * Math.Sin(rotation);
    }

    // East and north at a point, matching PieceProjection (at a pole, "east" is longitude 0's).
    private static (Vec East, Vec North) LocalAxes(Vec point)
    {
        GeoCoordinate at = point.ToCoordinate();
        double lat = double.DegreesToRadians(at.LatitudeDegrees);
        double lon = double.DegreesToRadians(at.LongitudeDegrees);
        var east = new Vec(Math.Cos(lon), 0.0, -Math.Sin(lon));
        var north = new Vec(
            -Math.Sin(lat) * Math.Sin(lon), Math.Cos(lat), -Math.Sin(lat) * Math.Cos(lon));
        return (east, north);
    }

    // A full-precision 3D vector, in the axes of SphericalCoordinates.
    private readonly record struct Vec(double X, double Y, double Z)
    {
        public double Length => Math.Sqrt(Dot(this));

        public static Vec From(GeoCoordinate coordinate)
        {
            double lat = double.DegreesToRadians(coordinate.LatitudeDegrees);
            double lon = double.DegreesToRadians(coordinate.LongitudeDegrees);
            return new Vec(
                Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat), Math.Cos(lat) * Math.Cos(lon));
        }

        public static Vec operator *(Vec v, double s) => new(v.X * s, v.Y * s, v.Z * s);

        public static Vec operator /(Vec v, double s) => new(v.X / s, v.Y / s, v.Z / s);

        public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public double Dot(Vec other) => X * other.X + Y * other.Y + Z * other.Z;

        public Vec Cross(Vec o) => new(Y * o.Z - Z * o.Y, Z * o.X - X * o.Z, X * o.Y - Y * o.X);

        // Rodrigues' rotation formula (axis must be unit length).
        public Vec RotatedAround(Vec axis, double angle)
        {
            double cos = Math.Cos(angle);
            return this * cos + axis.Cross(this) * Math.Sin(angle)
                + axis * (axis.Dot(this) * (1 - cos));
        }

        public GeoCoordinate ToCoordinate()
        {
            double latitude = Math.Asin(Math.Clamp(Y / Length, -1.0, 1.0));
            double longitude = Math.Atan2(X, Z);
            return new GeoCoordinate(
                double.RadiansToDegrees(latitude), double.RadiansToDegrees(longitude));
        }
    }
}
