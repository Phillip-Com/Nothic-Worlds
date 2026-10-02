using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The orbits linking one body to the center of its system, for working out that body's
/// position at many times quickly (searches for seasons and eclipses step through thousands of
/// times). It gives the same positions as <see cref="SystemPositions"/>, without computing
/// every other body at each step.
/// </summary>
public sealed class OrbitChain
{
    private readonly List<Orbit> _orbits;

    private OrbitChain(List<Orbit> orbits)
    {
        _orbits = orbits;
    }

    /// <summary>The chain from a body up to the center. The hierarchy must be valid.</summary>
    /// <param name="body">The body.</param>
    /// <param name="byId">Every body in the system, by ID.</param>
    public static OrbitChain Of(Body body, IReadOnlyDictionary<Guid, Body> byId)
    {
        var orbits = new List<Orbit>();
        var seen = new HashSet<Guid>();
        for (Body current = body; current.Orbit is Orbit orbit; current = byId[orbit.ParentId])
        {
            if (!seen.Add(current.Id))
            {
                throw new ArgumentException("The bodies' orbits form a loop.", nameof(body));
            }

            orbits.Add(orbit);
        }

        return new OrbitChain(orbits);
    }

    /// <summary>The body's position at a time, in km from the system's center.</summary>
    public Vector3D PositionAt(double timeDays)
    {
        Vector3D position = Vector3D.Zero;
        foreach (Orbit orbit in _orbits)
        {
            position += OrbitMath.OffsetFromParent(orbit, timeDays);
        }

        return position;
    }
}
