namespace NothicWorlds.Core.Model;

/// <summary>
/// Rules for a world's rivers and lakes (VISION.md BOD-11): each is valid on its own, none is
/// listed twice, and each lies on a planet or moon that exists.
/// </summary>
public static class WaterRules
{
    /// <summary>The most rivers a world can hold.</summary>
    public const int MaxRivers = 2_000;

    /// <summary>The most lakes a world can hold.</summary>
    public const int MaxLakes = 1_000;

    /// <summary>What's wrong with the world's rivers and lakes, or null if nothing.</summary>
    public static string? Problem(World world)
    {
        if (world.Rivers.Count > MaxRivers || world.Lakes.Count > MaxLakes)
        {
            return $"a world can hold up to {MaxRivers:N0} rivers and {MaxLakes:N0} lakes";
        }

        string? own = world.Rivers.Select(r => r.Problem())
            .Concat(world.Lakes.Select(l => l.Problem()))
            .FirstOrDefault(problem => problem is not null);
        if (own is not null)
        {
            return own;
        }

        if (world.Rivers.Select(r => r.Id).Distinct().Count() != world.Rivers.Count
            || world.Lakes.Select(l => l.Id).Distinct().Count() != world.Lakes.Count)
        {
            return "two rivers or two lakes share an ID";
        }

        var surfaces = world.Bodies.Where(b => b.HasSurface).Select(b => b.Id).ToHashSet();
        return world.Rivers.Any(r => !surfaces.Contains(r.BodyId))
            || world.Lakes.Any(l => !surfaces.Contains(l.BodyId))
            ? "a river or lake is on a body that doesn't exist (or a star or comet)"
            : null;
    }
}
