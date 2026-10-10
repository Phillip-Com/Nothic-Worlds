namespace NothicWorlds.Core.Geometry;

/// <summary>
/// A first-person rangefinder (VISION.md REN-06; owner's choice: under the mouse and at the
/// middle of the view): how far a line of sight from the eye runs before it meets the ground,
/// found by stepping out along it, in steps that grow with the distance, then narrowing in on
/// where it crossed. Over a globe the line runs straight while the ground curves away under
/// it; over a flat world's face the ground stays flat.
/// </summary>
public static class GroundRange
{
    /// <summary>The first and shortest step out along the line, in meters.</summary>
    public const double FirstStepMeters = 0.5;

    // Each step's length as a share of the distance already covered: ground narrower than
    // this far away can be stepped over, which at that distance is a few pixels at most.
    private const double StepShare = 0.02;

    // Halvings to narrow in on the crossing: the first step over it is at most a few km at
    // the longest reach, and this brings that to well under a millimeter.
    private const int Halvings = 32;

    /// <summary>Where a line of sight met the ground.</summary>
    /// <param name="Meters">How far along the line, from the eye.</param>
    /// <param name="AcrossMeters">How far over the ground, from under the eye.</param>
    /// <param name="HeightMeters">How high the ground is there, above the base.</param>
    public readonly record struct Hit(double Meters, double AcrossMeters, double HeightMeters);

    /// <summary>
    /// Where a line of sight meets the ground, or null if it doesn't within
    /// <paramref name="reachMeters"/>.
    /// </summary>
    /// <param name="eyeMeters">The eye's height above the base, in meters.</param>
    /// <param name="elevation">The line's angle above level, in radians.</param>
    /// <param name="radiusMeters">
    /// The globe's radius, in meters, or <see cref="double.PositiveInfinity"/> over a flat
    /// face.
    /// </param>
    /// <param name="reachMeters">How far along the line to look, in meters.</param>
    /// <param name="groundMeters">
    /// The ground's height above the base, in meters, for a distance over the ground from
    /// under the eye along the line's bearing.
    /// </param>
    public static Hit? Find(double eyeMeters, double elevation, double radiusMeters,
        double reachMeters, Func<double, double> groundMeters)
    {
        ArgumentNullException.ThrowIfNull(groundMeters);
        if (!double.IsFinite(eyeMeters) || !double.IsFinite(elevation) || !(reachMeters > 0))
        {
            return null;
        }

        double previous = 0;
        double meters = Math.Min(FirstStepMeters, reachMeters);
        while (true)
        {
            if (Above(meters) <= 0)
            {
                return Narrow(previous, meters);
            }

            if (meters >= reachMeters)
            {
                return null;
            }

            previous = meters;
            meters = Math.Min(meters + Math.Max(FirstStepMeters, meters * StepShare),
                reachMeters);
        }

        // How far a point `meters` along the line is above the ground under it.
        double Above(double meters)
        {
            (double across, double height) = Point(eyeMeters, elevation, radiusMeters, meters);
            return height - groundMeters(across);
        }

        // Halves the step that crossed the ground until it's pinned down.
        Hit Narrow(double over, double under)
        {
            for (int i = 0; i < Halvings; i++)
            {
                double middle = (over + under) / 2;
                if (Above(middle) <= 0)
                {
                    under = middle;
                }
                else
                {
                    over = middle;
                }
            }

            (double across, _) = Point(eyeMeters, elevation, radiusMeters, under);
            return new Hit(under, across, groundMeters(across));
        }
    }

    /// <summary>
    /// Where a point <paramref name="meters"/> along a line of sight is: how far over the
    /// ground from under the eye, and how high above the base, both in meters.
    /// </summary>
    public static (double AcrossMeters, double HeightMeters) Point(double eyeMeters,
        double elevation, double radiusMeters, double meters)
    {
        double level = meters * Math.Cos(elevation);
        double rise = meters * Math.Sin(elevation);
        if (double.IsPositiveInfinity(radiusMeters))
        {
            return (level, eyeMeters + rise);
        }

        // In the plane through the globe's middle, the eye, and the line.
        double outward = radiusMeters + eyeMeters + rise;
        double fromMiddle = Math.Sqrt(level * level + outward * outward);
        return (radiusMeters * Math.Atan2(level, outward), fromMiddle - radiusMeters);
    }
}
