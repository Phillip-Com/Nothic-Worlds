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
    /// Turns a direction on the body (its own frame: +Y north, longitude 0 toward +Z, as on
    /// its map) into the system's frame at a time: spun by the day, then leaned by the axial
    /// tilt. The same turn the 3D view gives the globe, so what's drawn and what's worked out
    /// here agree.
    /// </summary>
    public static Vector3D ToSystem(Body body, double timeDays, Vector3D local)
    {
        double spin = double.DegreesToRadians(BodyClock.SpinDegrees(body, timeDays));
        Vector3D spun = RotateAround(local, new Vector3D(0, 1, 0), spin);
        double tilt = double.DegreesToRadians(body.AxialTiltDegrees);
        if (tilt == 0)
        {
            return spun;
        }

        // The tilt turns about up × lean, which carries up toward the lean direction.
        Vector3D lean = LeanDirection(body);
        var axis = new Vector3D(lean.Z, 0, -lean.X);
        return RotateAround(spun, axis * (1 / axis.Length), tilt);
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

    // Rodrigues' rotation: v turned by `angle` radians about the unit `axis`, counterclockwise
    // looking down the axis toward the origin (the right-hand rule).
    private static Vector3D RotateAround(Vector3D v, Vector3D axis, double angle)
    {
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        var cross = new Vector3D(
            axis.Y * v.Z - axis.Z * v.Y,
            axis.Z * v.X - axis.X * v.Z,
            axis.X * v.Y - axis.Y * v.X);
        return v * cos + cross * sin + axis * (axis.Dot(v) * (1 - cos));
    }
}
