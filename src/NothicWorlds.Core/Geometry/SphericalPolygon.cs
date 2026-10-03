namespace NothicWorlds.Core.Geometry;

/// <summary>
/// An outline on a sphere (VISION.md LORE-01: region outlines): corner points joined by the
/// shortest paths between them (great-circle arcs). Works in double precision, on the same
/// axes as <see cref="SphericalCoordinates"/>.
/// </summary>
/// <remarks>
/// "Inside" is worked out on a flat projection that touches the sphere at the outline's center
/// (gnomonic: it keeps every great-circle edge straight, so the test is exact). That only works
/// for outlines within half the sphere, so <see cref="FitsInHemisphere"/> is required of every
/// region. An outline that crosses itself counts spots inside by the even-odd rule.
/// </remarks>
public static class SphericalPolygon
{
    // The farthest a corner may be from the outline's center: just short of a quarter turn,
    // so the projection stays well behaved.
    private const double MaxCornerDegrees = 85.0;

    /// <summary>
    /// The unit direction to a coordinate (a double-precision
    /// <see cref="SphericalCoordinates.ToDirection"/>).
    /// </summary>
    public static Vector3D ToUnit(GeoCoordinate coordinate)
    {
        double latitude = double.DegreesToRadians(coordinate.LatitudeDegrees);
        double longitude = double.DegreesToRadians(coordinate.LongitudeDegrees);
        double cosLatitude = Math.Cos(latitude);
        return new Vector3D(cosLatitude * Math.Sin(longitude), Math.Sin(latitude),
            cosLatitude * Math.Cos(longitude));
    }

    /// <summary>The coordinate a direction points at (it needn't be unit length).</summary>
    public static GeoCoordinate FromUnit(Vector3D direction)
    {
        double length = direction.Length;
        double latitude = Math.Asin(Math.Clamp(direction.Y / length, -1.0, 1.0));
        double longitude = Math.Atan2(direction.X, direction.Z);
        return new GeoCoordinate(
            double.RadiansToDegrees(latitude), double.RadiansToDegrees(longitude));
    }

    /// <summary>
    /// The outline's center: the direction of its corners' average, or null if they balance
    /// out (spread evenly around the sphere).
    /// </summary>
    public static GeoCoordinate? Center(IReadOnlyList<GeoCoordinate> corners)
    {
        Vector3D sum = Vector3D.Zero;
        foreach (GeoCoordinate corner in corners)
        {
            sum += ToUnit(corner);
        }

        return sum.Length < 1e-9 ? null : FromUnit(sum);
    }

    /// <summary>True if every corner is well within a quarter turn of the center.</summary>
    public static bool FitsInHemisphere(IReadOnlyList<GeoCoordinate> corners)
    {
        if (corners.Count == 0 || Center(corners) is not GeoCoordinate center)
        {
            return false;
        }

        Vector3D middle = ToUnit(center);
        double minCos = Math.Cos(double.DegreesToRadians(MaxCornerDegrees));
        return corners.All(corner => ToUnit(corner).Dot(middle) > minCos);
    }

    /// <summary>
    /// True if a spot is inside the outline. Outlines that don't fit in a hemisphere contain
    /// nothing.
    /// </summary>
    public static bool Contains(IReadOnlyList<GeoCoordinate> corners, GeoCoordinate spot)
    {
        if (corners.Count < 3 || !FitsInHemisphere(corners)
            || Center(corners) is not GeoCoordinate center)
        {
            return false;
        }

        var plane = new TangentPlane(ToUnit(center));
        if (plane.Project(ToUnit(spot)) is not (double x, double y))
        {
            return false;  // On the far half of the sphere.
        }

        // Even-odd ray casting along +x on the projection.
        bool inside = false;
        (double X, double Y) previous = plane.Project(ToUnit(corners[^1]))!.Value;
        foreach (GeoCoordinate corner in corners)
        {
            (double X, double Y) current = plane.Project(ToUnit(corner))!.Value;
            if ((current.Y > y) != (previous.Y > y))
            {
                double crossX = current.X
                    + (y - current.Y) * (previous.X - current.X) / (previous.Y - current.Y);
                if (crossX > x)
                {
                    inside = !inside;
                }
            }

            previous = current;
        }

        return inside;
    }

    /// <summary>
    /// The closed outline as points along its great-circle edges, no more than
    /// <paramref name="maxStepDegrees"/> apart (for drawing it). Ends back at the first corner.
    /// </summary>
    public static List<GeoCoordinate> EdgePath(
        IReadOnlyList<GeoCoordinate> corners, double maxStepDegrees)
    {
        var path = new List<GeoCoordinate>();
        for (int i = 0; i < corners.Count; i++)
        {
            Vector3D from = ToUnit(corners[i]);
            Vector3D to = ToUnit(corners[(i + 1) % corners.Count]);
            double angle = Math.Acos(Math.Clamp(from.Dot(to), -1.0, 1.0));
            int steps = Math.Max(1, (int)Math.Ceiling(double.RadiansToDegrees(angle)
                / Math.Max(maxStepDegrees, 1e-3)));
            for (int step = 0; step < steps; step++)
            {
                path.Add(FromUnit(Slerp(from, to, angle, (double)step / steps)));
            }
        }

        if (corners.Count > 0)
        {
            path.Add(corners[0]);
        }

        return path;
    }

    // The point a fraction of the way along the great circle from one unit vector to another.
    private static Vector3D Slerp(Vector3D from, Vector3D to, double angle, double fraction)
    {
        if (angle < 1e-12)
        {
            return from;
        }

        double sin = Math.Sin(angle);
        return from * (Math.Sin((1 - fraction) * angle) / sin)
            + to * (Math.Sin(fraction * angle) / sin);
    }

    // A flat plane touching the unit sphere at a point, onto which directions are projected
    // from the center (straight lines stay straight).
    private readonly struct TangentPlane
    {
        private readonly Vector3D _normal;
        private readonly Vector3D _east;
        private readonly Vector3D _north;

        public TangentPlane(Vector3D normal)
        {
            _normal = normal;
            Vector3D helper = Math.Abs(normal.Y) < 0.9 ? new Vector3D(0, 1, 0) : new(1, 0, 0);
            _east = Normalize(Cross(helper, normal));
            _north = Cross(normal, _east);
        }

        // Where a direction meets the plane, in plane coordinates; null if it points away.
        public (double X, double Y)? Project(Vector3D direction)
        {
            double along = direction.Dot(_normal);
            if (along <= 1e-9)
            {
                return null;
            }

            Vector3D onPlane = direction * (1 / along);
            return (onPlane.Dot(_east), onPlane.Dot(_north));
        }

        private static Vector3D Cross(Vector3D a, Vector3D b) => new(
            a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        private static Vector3D Normalize(Vector3D v) => v * (1 / v.Length);
    }
}
