using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Realms: planets and moons hung on a world tree's great branches (VISION.md BOD-02; owner's
/// choice: a realm keeps everything a world has, and rides the turning tree).
/// </summary>
/// <remarks>
/// A hung realm is given a locked orbit around its tree, worked out from its branch tip: around
/// the trunk at the tip's distance from it, lifted to the tip's height, tilted with the tree,
/// and taking one of the tree's turns to go round (its year). So it rides exactly where the
/// drawn tip is, and everything built on orbits (positions, seasons, eclipses, events) works
/// for it unchanged. <see cref="Apply"/> keeps those orbits up to date after any edit, the way
/// <see cref="CalendarFitting"/> keeps fitted calendars.
/// </remarks>
public static class Realms
{
    /// <summary>
    /// The orbit a realm on <paramref name="branch"/> of <paramref name="tree"/> follows.
    /// </summary>
    public static Orbit OrbitOnBranch(Body tree, int branch) =>
        OrbitAt(tree, WorldTreeShape.Grow(tree.Tree!).BranchTips[branch]);

    /// <summary>
    /// Gives every hung realm the orbit its branch sets. Returns true if any orbit changed.
    /// The realms must be valid (see <see cref="Problem"/>).
    /// </summary>
    public static bool Apply(IReadOnlyList<Body> bodies)
    {
        bool changed = false;
        var grown = new Dictionary<Guid, GrownTree>();
        var byId = bodies.ToDictionary(body => body.Id);
        foreach (Body realm in bodies)
        {
            if (realm.Branch is not int branch || realm.Orbit is not Orbit current
                || !byId.TryGetValue(current.ParentId, out Body? tree) || tree.Tree is null)
            {
                continue;
            }

            if (!grown.TryGetValue(tree.Id, out GrownTree? shape))
            {
                shape = WorldTreeShape.Grow(tree.Tree);
                grown[tree.Id] = shape;
            }

            Orbit orbit = OrbitAt(tree, shape.BranchTips[branch]);
            if (orbit != current)
            {
                realm.Orbit = orbit;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// What's wrong with the realms, or null if they're usable: a realm must be a planet or
    /// moon circling a world tree, on one of its branches, one realm to a branch.
    /// </summary>
    public static string? Problem(IReadOnlyList<Body> bodies)
    {
        var byId = bodies.ToDictionary(body => body.Id);
        var taken = new HashSet<(Guid, int)>();
        foreach (Body realm in bodies)
        {
            if (realm.Branch is not int branch)
            {
                continue;
            }

            if (!realm.HasSurface || realm.Orbit is not Orbit orbit
                || !byId.TryGetValue(orbit.ParentId, out Body? tree)
                || tree.Tree is not WorldTreeLook look)
            {
                return $"“{realm.Name}” hangs on a branch, but only planets and moons can, " +
                    "on a world tree they circle";
            }

            if (branch < 0 || branch >= look.Branches)
            {
                return $"“{realm.Name}” hangs on branch {branch + 1}, but {tree.Name} has " +
                    $"only {look.Branches}";
            }

            if (!taken.Add((tree.Id, branch)))
            {
                return $"two realms hang on branch {branch + 1} of {tree.Name}";
            }
        }

        return null;
    }

    // Round the trunk at the tip's distance, lifted to its height, tilted with the tree, and
    // starting where the tip is: the same rotations the tree is drawn with (its spin about its
    // axis, then its lean), so the realm stays on the tip as the tree turns.
    private static Orbit OrbitAt(Body tree, Vector3D tip)
    {
        double across = Math.Sqrt(tip.X * tip.X + tip.Z * tip.Z);
        double angle = double.RadiansToDegrees(Math.Atan2(-tip.Z, tip.X));
        return new Orbit
        {
            ParentId = tree.Id,
            DistanceKm = Math.Max(across, 1e-6) * tree.RadiusKm,
            PeriodDays = tree.DayLengthHours / 24,
            StartAngleDegrees = (angle + 360) % 360,
            TiltDegrees = tree.AxialTiltDegrees,
            TiltDirectionDegrees = (tree.AxialTiltDirectionDegrees + 90) % 360,
            HeightKm = tip.Y * tree.RadiusKm,
        };
    }
}
