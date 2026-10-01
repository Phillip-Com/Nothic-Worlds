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

    /// <summary>
    /// Everything that orbits <paramref name="ancestor"/>, directly or through others (its
    /// moons, their moons, ...), in list order. Not the body itself.
    /// </summary>
    public static List<Body> DescendantsOf(IReadOnlyList<Body> bodies, Guid ancestor)
    {
        var found = new HashSet<Guid> { ancestor };
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (Body body in bodies)
            {
                if (body.Orbit is Orbit orbit && found.Contains(orbit.ParentId)
                    && found.Add(body.Id))
                {
                    grew = true;
                }
            }
        }

        return [.. bodies.Where(body => body.Id != ancestor && found.Contains(body.Id))];
    }

    /// <summary>
    /// The orbit changes that make <paramref name="centerId"/> the center of its system (owner
    /// decision: "swap places, keep motion"). The chosen body stops orbiting; each body it was
    /// orbiting, up to the old center, flips to orbit the one below it on the same path. Every
    /// other body keeps its orbit. Relative positions never change: only which body sits still.
    /// </summary>
    /// <returns>
    /// The new orbit (or null for none) of each body that changes. Empty if the body is already
    /// at the center.
    /// </returns>
    public static Dictionary<Guid, Orbit?> MakeCenter(IReadOnlyList<Body> bodies, Guid centerId)
    {
        var byId = bodies.ToDictionary(body => body.Id);
        var changes = new Dictionary<Guid, Orbit?>();
        if (!byId.TryGetValue(centerId, out Body? center) || center.Orbit is null)
        {
            return changes;
        }

        changes[centerId] = null;
        Body below = center;
        while (below.Orbit is Orbit orbit && byId.TryGetValue(orbit.ParentId, out Body? above)
            && !changes.ContainsKey(above.Id))
        {
            // The parent now circles the child: the same path, seen from the other end.
            changes[above.Id] = Reversed(orbit, below.Id);
            below = above;
        }

        return changes;
    }

    // The same orbit seen from the other body: every position negated. For any orbit that's a
    // half turn within its own plane, so the start and closest-approach directions move 180°.
    private static Orbit Reversed(Orbit orbit, Guid newParent)
    {
        return orbit with
        {
            ParentId = newParent,
            StartAngleDegrees = (orbit.StartAngleDegrees + 180) % 360,
            ClosestApproachDegrees = (orbit.ClosestApproachDegrees + 180) % 360,
        };
    }

    /// <summary>The bodies that orbit <paramref name="parent"/> directly, in list order.</summary>
    public static IEnumerable<Body> ChildrenOf(IReadOnlyList<Body> bodies, Guid parent)
    {
        return bodies.Where(body => body.Orbit?.ParentId == parent);
    }
}
