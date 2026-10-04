using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// A shape added to or cut out of a body (VISION.md BOD-04; owner's design: heights plus shape
/// edits, which make holes through a world and hollow worlds possible). Immutable.
/// </summary>
/// <remarks>
/// <para>A shape is placed at a spot on the surface, with its middle <see cref="DepthKm"/> above
/// (or, negative, below) the body's radius there; its height points straight up from that
/// spot, and its width and length point east and north, turned by
/// <see cref="TurnDegrees"/>. A cylinder 2 radii tall centered a radius down goes right
/// through the world; a sphere centered a radius down hollows it out.</para>
/// <para>Sizes, in km: a sphere's diameter is its <see cref="WidthKm"/>; a box uses all three;
/// a cylinder and a cone (point up) use the width as their base's diameter and the height.
/// Unused sizes are kept as they are, so changing the kind keeps them.</para>
/// </remarks>
/// <param name="Id">Stable identity, kept across saves.</param>
/// <param name="Kind">The shape.</param>
/// <param name="Operation">Whether it adds or cuts.</param>
/// <param name="Spot">Where on the surface it's placed.</param>
/// <param name="DepthKm">How far its middle is above the body's radius (negative: below).</param>
/// <param name="WidthKm">Its width (east-west before the turn), or diameter.</param>
/// <param name="HeightKm">Its height (up from the surface).</param>
/// <param name="LengthKm">Its length (north-south before the turn).</param>
/// <param name="TurnDegrees">How far it's turned about its height, clockwise from north.</param>
public sealed record ShapeEdit(Guid Id, ShapeKind Kind, ShapeOperation Operation,
    GeoCoordinate Spot, double DepthKm, double WidthKm, double HeightKm, double LengthKm,
    double TurnDegrees)
{
    /// <summary>
    /// The most shapes a body can have, which keeps rebuilding its surface quick.
    /// </summary>
    public const int MaxPerBody = 64;

    /// <summary>
    /// The largest a shape's size can be, and how far its middle can be from the surface, in
    /// the body's radii (enough to reach right through it and out again).
    /// </summary>
    public const double MaxRadii = 4;

    /// <summary>
    /// What's wrong with the shape on a body of <paramref name="radiusKm"/>, or null if it's
    /// usable.
    /// </summary>
    public string? Problem(double radiusKm)
    {
        if (!Enum.IsDefined(Kind) || !Enum.IsDefined(Operation))
        {
            return "a shape must be a sphere, box, cylinder, or cone that adds or cuts";
        }

        if (!double.IsFinite(Spot.LatitudeDegrees) || Spot.LatitudeDegrees is < -90 or > 90
            || !double.IsFinite(Spot.LongitudeDegrees) || !double.IsFinite(TurnDegrees))
        {
            return "a shape's place and turn must be numbers (latitude -90° to 90°)";
        }

        double limit = MaxRadii * radiusKm;
        foreach (double size in new[] { WidthKm, HeightKm, LengthKm })
        {
            if (!double.IsFinite(size) || size <= 0 || size > limit)
            {
                return $"a shape's sizes must be above 0 and at most {limit:N0} km here";
            }
        }

        return double.IsFinite(DepthKm) && Math.Abs(DepthKm) <= limit
            ? null
            : $"a shape's middle must be within {limit:N0} km of the surface here";
    }

    /// <summary>
    /// Where the shape sits on a body of <paramref name="radiusKm"/>, in the body's radii.
    /// </summary>
    public ShapeFrame FrameOn(double radiusKm)
    {
        System.Numerics.Vector3 spot = SphericalCoordinates.ToDirection(Spot);
        var up = new Vector3D(spot.X, spot.Y, spot.Z);
        up *= 1 / up.Length;

        // North along the surface (at a pole, where north is everywhere, +Z stands in).
        Vector3D toPole = new Vector3D(0, 1, 0) - up * up.Y;
        Vector3D north = toPole.Length > 1e-9 ? toPole * (1 / toPole.Length) : new(0, 0, 1);
        Vector3D east = Cross(north, up);

        // Turned clockwise seen from above: north toward east.
        double turn = double.DegreesToRadians(TurnDegrees);
        Vector3D along = north * Math.Cos(turn) + east * Math.Sin(turn);
        Vector3D across = east * Math.Cos(turn) - north * Math.Sin(turn);
        return new ShapeFrame(up * (1 + DepthKm / radiusKm), across, up, along);
    }

    private static Vector3D Cross(Vector3D a, Vector3D b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}
