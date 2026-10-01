using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The rules for which body orbits which (VISION.md BOD-01, SIM-01). Any body can circle any
/// other (a moon around a planet, a second sun around the first), as long as every chain of
/// parents ends at a body without an orbit, which sits at the system's center.
/// </summary>
public static class SystemHierarchy
{
    /// <summary>
    /// What's wrong with how the bodies orbit each other, or null if it's usable: an orbit
    /// around a body that isn't there, a body orbiting itself, or a loop (A around B around A).
    /// </summary>
    public static string? Problem(IReadOnlyList<Body> bodies)
    {
        var byId = new Dictionary<Guid, Body>();
        foreach (Body body in bodies)
        {
            if (!byId.TryAdd(body.Id, body))
            {
                return "two bodies have the same ID";
            }
        }

        foreach (Body body in bodies)
        {
            var seen = new HashSet<Guid> { body.Id };
            for (Body current = body; current.Orbit is Orbit orbit;)
            {
                if (!byId.TryGetValue(orbit.ParentId, out Body? parent))
                {
                    return $"“{current.Name}” orbits a body that isn't in the world";
                }

                if (!seen.Add(parent.Id))
                {
                    return $"“{body.Name}” is in a loop of bodies orbiting each other";
                }

                current = parent;
            }
        }

        return null;
    }

    /// <summary>The bodies that orbit <paramref name="parent"/> directly, in list order.</summary>
    public static IEnumerable<Body> ChildrenOf(IReadOnlyList<Body> bodies, Guid parent)
    {
        return bodies.Where(body => body.Orbit?.ParentId == parent);
    }
}
