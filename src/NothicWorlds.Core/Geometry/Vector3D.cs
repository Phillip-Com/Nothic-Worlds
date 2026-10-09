namespace NothicWorlds.Core.Geometry;

/// <summary>
/// A 3D vector in full (double) precision, for positions across a whole star system, where
/// single precision would lose kilometers. Axes follow <see cref="SphericalCoordinates"/>: +Y is
/// north (up), +X and +Z lie in the system's reference plane.
/// </summary>
public readonly record struct Vector3D(double X, double Y, double Z)
{
    /// <summary>The zero vector.</summary>
    public static Vector3D Zero => default;

    /// <summary>The vector's length.</summary>
    public double Length => Math.Sqrt(Dot(this));

    public static Vector3D operator +(Vector3D a, Vector3D b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3D operator -(Vector3D a, Vector3D b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3D operator *(Vector3D v, double s) => new(v.X * s, v.Y * s, v.Z * s);

    /// <summary>The dot product.</summary>
    public double Dot(Vector3D other) => X * other.X + Y * other.Y + Z * other.Z;

    /// <summary>The cross product (right-handed).</summary>
    public Vector3D Cross(Vector3D other) =>
        new(Y * other.Z - Z * other.Y, Z * other.X - X * other.Z, X * other.Y - Y * other.X);

    /// <summary>
    /// Turns the vector around the +X axis by <paramref name="degrees"/> (right-handed: +Y
    /// toward +Z).
    /// </summary>
    public Vector3D RotatedAroundX(double degrees)
    {
        double angle = double.DegreesToRadians(degrees);
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        return new Vector3D(X, Y * cos - Z * sin, Y * sin + Z * cos);
    }

    /// <summary>
    /// Turns the vector around the +Y (north) axis by <paramref name="degrees"/>: counterclockwise
    /// when seen from the north, the way planets usually orbit and spin.
    /// </summary>
    public Vector3D RotatedAroundY(double degrees)
    {
        double angle = double.DegreesToRadians(degrees);
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        return new Vector3D(X * cos + Z * sin, Y, -X * sin + Z * cos);
    }
}
