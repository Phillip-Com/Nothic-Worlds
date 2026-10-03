namespace NothicWorlds.Core.Geometry;

/// <summary>
/// The layout of a flat world (VISION.md BOD-02; owner's choice: the whole world on top, like
/// the Polar map): a disc whose top face holds every latitude and longitude, with the north
/// pole at the center and the far south around the rim, and bare rock underneath.
/// </summary>
/// <remarks>
/// <para>Sizes are in the matching globe's radii, in the body's own space, where the globe would
/// be a unit sphere with +Y north (see <see cref="SphericalCoordinates"/>). Distance from the
/// center equals the angle from the north pole in radians, so the disc's radius is π and every
/// distance measured from the center is true (an azimuthal equidistant layout). Longitude runs
/// around the center as on a globe: 0 toward +Z and 90° east toward +X.</para>
/// <para>Because the face uses the same latitude and longitude as a globe, maps, terrain, and
/// regions carry over unchanged; only where each point sits differs.</para>
/// </remarks>
public static class FlatDisc
{
    /// <summary>The disc's radius, in the matching globe's radii.</summary>
    public const double Radius = Math.PI;

    /// <summary>Half the disc's thickness: the top face is this far above its middle.</summary>
    public const double HalfThickness = 0.04;

    /// <summary>
    /// Where a point on the top face is, for the direction it would have from a globe's center
    /// (which needn't be unit length). The face is at height <see cref="HalfThickness"/>.
    /// </summary>
    public static Vector3D TopPointFor(Vector3D direction)
    {
        double length = direction.Length;
        if (!(length > 0) || !double.IsFinite(length))
        {
            throw new ArgumentException(
                "Direction must be a finite, non-zero vector.", nameof(direction));
        }

        double fromPole = Math.Acos(Math.Clamp(direction.Y / length, -1.0, 1.0));
        double across = Math.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
        return across == 0
            ? new Vector3D(0, HalfThickness, 0)
            : new Vector3D(direction.X / across * fromPole, HalfThickness,
                direction.Z / across * fromPole);
    }

    /// <summary>
    /// The globe direction (unit length) for a point on the top face; its height is ignored.
    /// Points beyond the rim are treated as being on it.
    /// </summary>
    public static Vector3D DirectionFor(Vector3D topPoint)
    {
        double across = Math.Sqrt(topPoint.X * topPoint.X + topPoint.Z * topPoint.Z);
        double fromPole = Math.Min(across, Radius);
        if (across == 0)
        {
            return new Vector3D(0, 1, 0);
        }

        double sideways = Math.Sin(fromPole) / across;
        return new Vector3D(topPoint.X * sideways, Math.Cos(fromPole), topPoint.Z * sideways);
    }
}
