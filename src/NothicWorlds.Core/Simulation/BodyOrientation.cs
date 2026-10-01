using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Which way a body's spin axis points (VISION.md CAL-03). The axis starts straight up (+Y,
/// north) and leans by the axial tilt toward the tilt direction, measured like orbit angles:
/// direction θ is (cos θ, 0, −sin θ). Drawing and the season calculations both use this, so
/// what's shown always matches the seasons.
/// </summary>
public static class BodyOrientation
{
    /// <summary>The unit vector the body's north pole points along.</summary>
    public static Vector3D NorthPole(Body body)
    {
        double tilt = double.DegreesToRadians(body.AxialTiltDegrees);
        Vector3D lean = LeanDirection(body);
        return new Vector3D(0, Math.Cos(tilt), 0) + lean * Math.Sin(tilt);
    }

    /// <summary>
    /// The horizontal direction the north pole leans toward (unit length, in the reference
    /// plane).
    /// </summary>
    public static Vector3D LeanDirection(Body body)
    {
        double direction = double.DegreesToRadians(body.AxialTiltDirectionDegrees);
        return new Vector3D(Math.Cos(direction), 0, -Math.Sin(direction));
    }
}
