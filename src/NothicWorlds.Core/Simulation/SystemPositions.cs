using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Where every body in a star system is at a given time (VISION.md SIM-01, SIM-02), in true
/// km from the system's center. A body without an orbit sits at the center; each other body is
/// its parent's position plus its own orbit's offset.
/// </summary>
public static class SystemPositions
{
    /// <summary>Every body's position at <paramref name="timeDays"/>, by ID.</summary>
    /// <exception cref="ArgumentException">
    /// The bodies break the rules in <see cref="SystemHierarchy.Problem"/>.
    /// </exception>
    public static Dictionary<Guid, Vector3D> At(IReadOnlyList<Body> bodies, double timeDays)
    {
        if (SystemHierarchy.Problem(bodies) is string problem)
        {
            throw new ArgumentException($"The star system is invalid: {problem}.", nameof(bodies));
        }

        var byId = bodies.ToDictionary(body => body.Id);
        var positions = new Dictionary<Guid, Vector3D>();
        foreach (Body body in bodies)
        {
            PositionOf(body, byId, positions, timeDays);
        }

        return positions;
    }

    // Works up the chain of parents, remembering each answer so every body is computed once.
    private static Vector3D PositionOf(
        Body body,
        Dictionary<Guid, Body> byId,
        Dictionary<Guid, Vector3D> positions,
        double timeDays)
    {
        if (positions.TryGetValue(body.Id, out Vector3D known))
        {
            return known;
        }

        Vector3D position = body.Orbit is Orbit orbit
            ? PositionOf(byId[orbit.ParentId], byId, positions, timeDays)
                + OrbitMath.OffsetFromParent(orbit, timeDays)
            : Vector3D.Zero;
        positions[body.Id] = position;
        return position;
    }
}
