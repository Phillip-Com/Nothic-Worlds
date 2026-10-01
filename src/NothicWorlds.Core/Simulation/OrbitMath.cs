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

    /// <summary>
    /// Times (in days, from 0 up to one period) to sample one full trip around an orbit so the
    /// points are spread evenly along the curve, not evenly in time. On an elongated orbit the
    /// body rushes through its closest approach; even time steps would leave only a few points
    /// there, joined by straight lines the body doesn't follow.
    /// </summary>
    public static IReadOnlyList<double> EvenlySpacedTimes(Orbit orbit, int samples)
    {
        // Step evenly in eccentric anomaly (the angle around the orbit's ellipse), starting at
        // where the body is at time 0, and convert each step back to a time.
        double e = orbit.Eccentricity;
        double startMean = double.DegreesToRadians(
            orbit.StartAngleDegrees - orbit.ClosestApproachDegrees);
        double startEccentric = SolveKepler(startMean, e);
        double startMeanWrapped = startEccentric - e * Math.Sin(startEccentric);
        var times = new double[samples];
        for (int i = 0; i < samples; i++)
        {
            double eccentric = startEccentric + 2 * Math.PI * i / samples;
            double mean = eccentric - e * Math.Sin(eccentric);
            times[i] = (mean - startMeanWrapped) / (2 * Math.PI) * orbit.PeriodDays;
        }

        return times;
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
