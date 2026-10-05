namespace NothicWorlds.Core.Geometry;

/// <summary>
/// Walking on a flat world (VISION.md REN-06; owner's choice: walking over the rim goes on down
/// the edge and onto the underside). The surface is one continuous walk: the top face, the rim
/// around it, and the bare underside. North always leads back toward the top's center (the
/// north pole): on the top it points to the center, on the rim it points up the wall, and on
/// the underside it points out toward the rim, so heading south from the top's center goes out
/// to the rim, down it, and across the underside to its center. East is the same way round on
/// every face.
/// </summary>
/// <remarks>
/// Sizes are in the matching globe's radii, in the body's own space, like
/// <see cref="FlatDisc"/>. Walks are taken in short steps, each in the frame where it starts,
/// so a walk on a steady bearing curves as the frame turns, as it does on a globe.
/// </remarks>
public static class FlatWalk
{
    // The longest step taken at once, in globe radii: short enough that a frame barely turns.
    private const double MaxStep = 0.002;

    private static double RimTop => FlatDisc.HalfThickness;

    /// <summary>The spot on the top face over a point of it (its height is ignored).</summary>
    public static FlatSpot OnTop(Vector3D topPoint) => new(FlatFace.Top, topPoint.X, topPoint.Z);

    /// <summary>
    /// Where a spot is in the body's own space, <paramref name="above"/> globe radii off the
    /// surface (straight out from its face).
    /// </summary>
    public static Vector3D Point(FlatSpot spot, double above = 0)
    {
        return spot.Face switch
        {
            FlatFace.Top => new Vector3D(spot.A, RimTop + above, spot.B),
            FlatFace.Bottom => new Vector3D(spot.A, -RimTop - above, spot.B),
            _ => new Vector3D(Math.Sin(spot.A) * (FlatDisc.Radius + above), spot.B,
                Math.Cos(spot.A) * (FlatDisc.Radius + above)),
        };
    }

    /// <summary>
    /// East, north, and up at a spot, in the body's own space (unit vectors, east × north =
    /// up). At a face's very center, the spot's angle is taken as 0.
    /// </summary>
    public static (Vector3D East, Vector3D North, Vector3D Up) Frame(FlatSpot spot)
    {
        double around = Around(spot);
        var east = new Vector3D(Math.Cos(around), 0, -Math.Sin(around));
        var outward = new Vector3D(Math.Sin(around), 0, Math.Cos(around));
        return spot.Face switch
        {
            FlatFace.Top => (east, outward * -1, new Vector3D(0, 1, 0)),
            FlatFace.Bottom => (east, outward, new Vector3D(0, -1, 0)),
            _ => (east, new Vector3D(0, 1, 0), outward),
        };
    }

    /// <summary>
    /// The globe direction a spot's map point stands for, or null off the top face (the rim
    /// and underside are bare rock, with no map, terrain, or weather).
    /// </summary>
    public static Vector3D? MapDirection(FlatSpot spot) => spot.Face == FlatFace.Top
        ? FlatDisc.DirectionFor(Point(spot))
        : null;

    /// <summary>
    /// Walks <paramref name="distance"/> globe radii from a spot on a bearing
    /// (<paramref name="bearing"/> radians clockwise from north), over the rim if it comes to
    /// it, and returns where it ends.
    /// </summary>
    public static FlatSpot Walk(FlatSpot spot, double bearing, double distance)
    {
        if (!double.IsFinite(distance) || !double.IsFinite(bearing))
        {
            throw new ArgumentException("A walk needs a finite bearing and distance.");
        }

        double left = Math.Abs(distance);
        double sign = Math.Sign(distance);
        while (left > 0)
        {
            double step = Math.Min(left, MaxStep);
            (spot, double leftOver) = Step(spot, bearing, sign * step);
            left -= step - leftOver;
        }

        return spot;
    }

    // One short step in the frame where it starts. If it reaches the face's edge, it stops
    // there, moves onto the next face, and returns the distance still to go.
    private static (FlatSpot Spot, double LeftOver) Step(FlatSpot spot, double bearing,
        double step)
    {
        (Vector3D east, Vector3D north, _) = Frame(spot);
        Vector3D along = (north * Math.Cos(bearing) + east * Math.Sin(bearing)) * step;
        if (spot.Face == FlatFace.Rim)
        {
            double around = spot.A + along.Dot(east) / FlatDisc.Radius;
            double height = spot.B + along.Y;
            if (height > RimTop || height < -RimTop)
            {
                double edge = height > RimTop ? RimTop : -RimTop;
                double fraction = (edge - spot.B) / along.Y;
                double reached = spot.A + fraction * along.Dot(east) / FlatDisc.Radius;
                var rimEdge = new Vector3D(Math.Sin(reached), 0, Math.Cos(reached))
                    * FlatDisc.Radius;
                FlatFace face = height > RimTop ? FlatFace.Top : FlatFace.Bottom;
                return (new FlatSpot(face, rimEdge.X, rimEdge.Z), Math.Abs(step) * (1 - fraction));
            }

            return (new FlatSpot(FlatFace.Rim, around, height), 0);
        }

        double x = spot.A + along.X, z = spot.B + along.Z;
        if (x * x + z * z <= FlatDisc.Radius * FlatDisc.Radius)
        {
            return (spot with { A = x, B = z }, 0);
        }

        // Over the edge: stop where the step meets the rim, and carry on down (or up) it.
        double fractionToRim = FractionToCircle(spot.A, spot.B, along.X, along.Z);
        double rimAngle = Math.Atan2(spot.A + along.X * fractionToRim,
            spot.B + along.Z * fractionToRim);
        double rimHeight = spot.Face == FlatFace.Top ? RimTop : -RimTop;
        return (new FlatSpot(FlatFace.Rim, rimAngle, rimHeight),
            Math.Abs(step) * (1 - fractionToRim));
    }

    // How far along a step from (x, z) by (dx, dz) it meets the rim's circle (0 to 1).
    private static double FractionToCircle(double x, double z, double dx, double dz)
    {
        double a = dx * dx + dz * dz;
        double b = 2 * (x * dx + z * dz);
        double c = x * x + z * z - FlatDisc.Radius * FlatDisc.Radius;
        double root = Math.Sqrt(Math.Max(b * b - 4 * a * c, 0));
        return Math.Clamp((-b + root) / (2 * a), 0, 1);
    }

    // The angle around the disc's axis (clockwise from +Z seen from above, as longitude runs).
    private static double Around(FlatSpot spot) => spot.Face == FlatFace.Rim
        ? spot.A
        : spot.A == 0 && spot.B == 0 ? 0 : Math.Atan2(spot.A, spot.B);
}
