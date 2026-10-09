namespace NothicWorlds.Core.Geometry;

/// <summary>
/// Keeps a first-person eye clear of the ground around it (VISION.md REN-06): how close the
/// nearest ground is, so the camera never cuts away ground in front of it, and whether a step
/// climbs too steeply to walk.
/// </summary>
public static class GroundClearance
{
    /// <summary>The steepest slope that can be walked up, in degrees (owner's choice).</summary>
    public const double SteepestWalkDegrees = 50;

    /// <summary>
    /// How far past a step the ground is looked at before walking, in meters: a body's
    /// half-width, so the eye never walks right up against a cliff face.
    /// </summary>
    public const double BodyRadiusMeters = 0.5;

    /// <summary>How many bearings the ground is looked at along, all the way around.</summary>
    public const int Bearings = 8;

    /// <summary>
    /// How far out the ground around the eye is looked at, as shares of the eye's height
    /// above the ground straight below. Ground farther out than that height is farther than
    /// the ground below, so it can't be the nearest.
    /// </summary>
    public static IReadOnlyList<double> RingShares { get; } = [0.125, 0.25, 0.5, 1];

    /// <summary>
    /// The distance from the eye to the nearest ground, in meters: the ground straight below,
    /// <paramref name="eyeHeight"/> meters down, or any of the <paramref name="ground"/> points,
    /// each given by how far out it is and how far it rises above the ground below.
    /// </summary>
    public static double Nearest(double eyeHeight,
        IEnumerable<(double AcrossMeters, double RiseMeters)> ground)
    {
        double nearest = eyeHeight;
        foreach ((double across, double rise) in ground)
        {
            double below = eyeHeight - rise;
            nearest = Math.Min(nearest, Math.Sqrt(across * across + below * below));
        }

        return nearest;
    }

    /// <summary>
    /// Whether ground rising <paramref name="riseMeters"/> over <paramref name="runMeters"/>
    /// is too steep to walk up. Going down is never too steep.
    /// </summary>
    public static bool TooSteep(double runMeters, double riseMeters) =>
        riseMeters > 0
        && Math.Atan2(riseMeters, runMeters) > double.DegreesToRadians(SteepestWalkDegrees);
}
