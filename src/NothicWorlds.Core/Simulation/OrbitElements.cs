using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Turns where a body is and how it's moving relative to its parent into the designed orbit
/// that matches (its "osculating" orbit), for keeping physics mode's paths (VISION.md SIM-03).
/// The inverse of <see cref="OrbitMath.OffsetFromParent"/>: the orbit found puts the body at
/// the same place, moving the same way, at the same time.
/// </summary>
public static class OrbitElements
{
    // Below these an orbit counts as round or flat, and the direction that would be undefined
    // (where it comes closest, which way it tilts) is kept from the design.
    private const double RoundEccentricity = 1e-6;
    private const double FlatTiltDegrees = 1e-6;

    /// <summary>
    /// The orbit a body is on, from its offset from its parent (km) and velocity relative to it
    /// (km per day) at <paramref name="timeDays"/>.
    /// </summary>
    /// <param name="offset">The body's position relative to its parent, in km.</param>
    /// <param name="velocity">Its velocity relative to the parent, in km per day.</param>
    /// <param name="gravity">
    /// G times the two bodies' total mass, in km³ per day² (how strongly they pull together).
    /// </param>
    /// <param name="timeDays">The time on the world clock.</param>
    /// <param name="design">
    /// The body's designed orbit: its parent, and the directions kept when the new orbit is
    /// round or flat.
    /// </param>
    /// <returns>The orbit, or why there isn't one (the body is escaping, say).</returns>
    public static (Orbit? Orbit, string? Problem) FromMotion(Vector3D offset, Vector3D velocity,
        double gravity, double timeDays, Orbit design)
    {
        double distance = offset.Length;
        Vector3D spin = Cross(offset, velocity);
        if (distance == 0 || spin.Length == 0)
        {
            return (null, "it's falling straight in or out");
        }

        double size = 1 / (2 / distance - velocity.Dot(velocity) / gravity);
        Vector3D toClosest = Cross(velocity, spin) * (1 / gravity) - offset * (1 / distance);
        double eccentricity = toClosest.Length;
        if (size <= 0 || eccentricity >= 1)
        {
            return (null, "it's escaping");
        }

        if (eccentricity > Orbit.MaxEccentricity)
        {
            return (null, $"its orbit is too elongated to keep (eccentricity {eccentricity:0.##})");
        }

        (double tilt, double tiltDirection) = Tilt(spin, design);
        bool round = eccentricity < RoundEccentricity;
        double closest = round
            ? design.ClosestApproachDegrees
            : AngleOf(Untilt(toClosest, tilt, tiltDirection)) - 90 + tiltDirection;
        if (round)
        {
            eccentricity = 0;
        }

        double period = 2 * Math.PI * Math.Sqrt(size * size * size / gravity);
        double meanAnomaly = MeanAnomaly(
            Untilt(offset, tilt, tiltDirection).RotatedAroundY(tiltDirection - closest),
            size, eccentricity);
        double start = closest + double.RadiansToDegrees(
            meanAnomaly - 2 * Math.PI * (timeDays / period % 1.0));
        Orbit orbit = design with
        {
            DistanceKm = size,
            PeriodDays = period,
            Eccentricity = eccentricity,
            ClosestApproachDegrees = Wrapped(closest),
            TiltDegrees = tilt,
            TiltDirectionDegrees = Wrapped(tiltDirection),
            StartAngleDegrees = Wrapped(start),
            HeightKm = 0,
        };
        return orbit.Problem() is string problem ? (null, problem) : (orbit, null);
    }

    // How far the orbit's plane is tilted, and which way, from the direction it spins about
    // (the orbit's north). A flat orbit keeps its designed tilt direction.
    private static (double Tilt, double Direction) Tilt(Vector3D spin, Orbit design)
    {
        Vector3D north = spin * (1 / spin.Length);
        double tilt = double.RadiansToDegrees(Math.Acos(Math.Clamp(north.Y, -1, 1)));
        bool flat = tilt < FlatTiltDegrees || tilt > 180 - FlatTiltDegrees;
        // Tilting +Y around +X leans it toward +Z (angle 0), so the turn that follows is the
        // angle of the orbit's north.
        return flat
            ? (tilt < 90 ? 0 : 180, design.TiltDirectionDegrees)
            : (tilt, AngleOf(north));
    }

    // Undoes the tilt and its direction, back into the frame where the orbit lies flat.
    private static Vector3D Untilt(Vector3D vector, double tilt, double tiltDirection) =>
        vector.RotatedAroundY(-tiltDirection).RotatedAroundX(-tilt);

    // Where along the ellipse a point in its own flat frame is, as a mean anomaly (radians):
    // the frame has the closest approach toward +X and the motion heading toward -Z.
    private static double MeanAnomaly(Vector3D flat, double size, double eccentricity)
    {
        double across = size * Math.Sqrt(1 - eccentricity * eccentricity);
        double eccentricAnomaly = Math.Atan2(-flat.Z / across, flat.X / size + eccentricity);
        return eccentricAnomaly - eccentricity * Math.Sin(eccentricAnomaly);
    }

    // The angle of a vector around +Y, in degrees, as Vector3D.RotatedAroundY turns it.
    private static double AngleOf(Vector3D vector) =>
        double.RadiansToDegrees(Math.Atan2(vector.X, vector.Z));

    private static double Wrapped(double degrees) => ((degrees % 360) + 360) % 360;

    private static Vector3D Cross(Vector3D a, Vector3D b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}
