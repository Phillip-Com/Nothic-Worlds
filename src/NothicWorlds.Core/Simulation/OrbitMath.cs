using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Where a body is along its designed orbit at a given time (VISION.md SIM-01). Deterministic:
/// the same orbit and time always give the same position, and any time can be computed directly,
/// so jumping to year 500 is as quick as the next frame.
/// </summary>
/// <remarks>
/// The body moves through equal angles of its orbit in equal times on average, speeding up
/// near its parent on an elongated orbit, as real orbits do (Kepler's equation). The orbit is
/// laid out in the reference plane (Y = 0), then turned by its closest-approach direction,
/// tilted, and turned by its tilt direction. docs/world-format.md describes the same math.
/// </remarks>
public static class OrbitMath
{
    /// <summary>
    /// Where the body is relative to its parent at <paramref name="timeDays"/>, in km.
    /// </summary>
    public static Vector3D OffsetFromParent(Orbit orbit, double timeDays)
    {
        double meanAnomaly = double.DegreesToRadians(
            orbit.StartAngleDegrees - orbit.ClosestApproachDegrees)
            + 2 * Math.PI * (timeDays / orbit.PeriodDays % 1.0);
        double eccentricAnomaly = SolveKepler(meanAnomaly, orbit.Eccentricity);

        // Position in the orbit's own plane: x toward the closest approach, y 90° ahead.
        double e = orbit.Eccentricity;
        double x = orbit.DistanceKm * (Math.Cos(eccentricAnomaly) - e);
        double y = orbit.DistanceKm * Math.Sqrt(1 - e * e) * Math.Sin(eccentricAnomaly);

        // In the reference plane, 90° ahead of +X (counterclockwise from the north) is -Z.
        var inPlane = new Vector3D(x, 0, -y);
        return inPlane
            .RotatedAroundY(orbit.ClosestApproachDegrees - orbit.TiltDirectionDegrees)
            .RotatedAroundX(orbit.TiltDegrees)
            .RotatedAroundY(orbit.TiltDirectionDegrees);
    }

    // Solves Kepler's equation, M = E - e·sin(E), for E (Newton's method). Converges in a few
    // steps for every eccentricity allowed (up to 0.95).
    private static double SolveKepler(double meanAnomaly, double eccentricity)
    {
        double m = Math.IEEERemainder(meanAnomaly, 2 * Math.PI);
        double e = eccentricity;
        double guess = e < 0.8 ? m : Math.PI * Math.Sign(m == 0 ? 1 : m);
        for (int i = 0; i < 30; i++)
        {
            double step = (guess - e * Math.Sin(guess) - m) / (1 - e * Math.Cos(guess));
            guess -= step;
            if (Math.Abs(step) < 1e-14)
            {
                break;
            }
        }

        return guess;
    }
}
