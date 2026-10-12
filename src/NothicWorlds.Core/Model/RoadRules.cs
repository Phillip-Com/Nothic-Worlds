namespace NothicWorlds.Core.Model;

/// <summary>
/// Rules for a world's roads (VISION.md BOD-13): each is valid on its own, none is listed
/// twice, and each lies on a planet or moon that exists.
/// </summary>
public static class RoadRules
{
    /// <summary>The most roads a world can hold.</summary>
    public const int MaxRoads = 2_000;

    /// <summary>What's wrong with the world's roads, or null if nothing.</summary>
    public static string? Problem(World world)
    {
        if (world.Roads.Count > MaxRoads)
        {
            return $"a world can hold up to {MaxRoads:N0} roads";
        }

        if (world.Roads.Select(r => r.Problem()).FirstOrDefault(p => p is not null)
            is string own)
        {
            return own;
        }

        if (world.Roads.Select(r => r.Id).Distinct().Count() != world.Roads.Count)
        {
            return "two roads share an ID";
        }

        var surfaces = world.Bodies.Where(b => b.HasSurface).Select(b => b.Id).ToHashSet();
        return world.Roads.Any(r => !surfaces.Contains(r.BodyId))
            ? "a road is on a body that doesn't exist (or a star or comet)"
            : null;
    }
}
