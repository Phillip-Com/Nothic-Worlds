using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// Rules that tie a world's regions, journal, timelines, and events together (VISION.md
/// LORE-01, LORE-02, LORE-03): every link and reference points at something that exists, and
/// nothing is listed twice.
/// </summary>
public static class LoreRules
{
    /// <summary>The most journal entries a world can hold.</summary>
    public const int MaxEntries = 10_000;

    /// <summary>The most timelines a world can hold.</summary>
    public const int MaxTimelines = 100;

    /// <summary>The most timeline events a world can hold.</summary>
    public const int MaxEvents = 10_000;

    /// <summary>The most regions a world can hold.</summary>
    public const int MaxRegions = 5_000;

    /// <summary>What's wrong with the world's lore, or null if nothing.</summary>
    public static string? Problem(World world)
    {
        if (world.Journal.Count > MaxEntries || world.Timelines.Count > MaxTimelines
            || world.Events.Count > MaxEvents || world.Regions.Count > MaxRegions)
        {
            return $"a world can hold up to {MaxEntries:N0} journal entries, " +
                $"{MaxTimelines} timelines, {MaxEvents:N0} events, and {MaxRegions:N0} regions";
        }

        string? own = world.Regions.Select(r => r.Problem())
            .Concat(world.Journal.Select(e => e.Problem()))
            .Concat(world.Timelines.Select(t => t.Problem()))
            .Concat(world.Events.Select(e => e.Problem()))
            .FirstOrDefault(problem => problem is not null);
        if (own is not null)
        {
            return own;
        }

        if (HasDuplicates(world.Regions.Select(r => r.Id))
            || HasDuplicates(world.Journal.Select(e => e.Id))
            || HasDuplicates(world.Timelines.Select(t => t.Id))
            || HasDuplicates(world.Events.Select(e => e.Id)))
        {
            return "two regions, journal entries, timelines, or events share an ID";
        }

        var entries = world.Journal.Select(e => e.Id).ToHashSet();
        var timelines = world.Timelines.Select(t => t.Id).ToHashSet();
        var bodies = world.Bodies.Select(b => b.Id).ToHashSet();
        var surfaces = world.Bodies.Where(b => b.Kind != BodyKind.Star)
            .Select(b => b.Id).ToHashSet();
        if (world.Regions.Any(r => !surfaces.Contains(r.BodyId)))
        {
            return "a region is on a body that doesn't exist (or a star)";
        }

        var regionBodies = world.Regions.ToDictionary(r => r.Id, r => r.BodyId);
        foreach (TimelineEvent timelineEvent in world.Events)
        {
            if (!timelines.Contains(timelineEvent.TimelineId))
            {
                return $"the event '{timelineEvent.Title}' is on a timeline that doesn't exist";
            }

            if (!timelineEvent.EntryIds.All(entries.Contains))
            {
                return $"the event '{timelineEvent.Title}' links to a missing journal entry";
            }
        }

        IEnumerable<LoreLocation?> locations = world.Journal.Select(e => e.Location)
            .Concat(world.Events.Select(e => e.Location));
        if (locations.Any(l => l is not null && !bodies.Contains(l.BodyId)))
        {
            return "a journal entry or event is placed on a body that doesn't exist";
        }

        return locations.Any(l => l?.RegionId is Guid region
                && (!regionBodies.TryGetValue(region, out Guid body) || body != l.BodyId))
            ? "a journal entry or event is placed in a region that isn't on its body"
            : null;
    }

    /// <summary>The regions on a body that contain a spot, in drawing order.</summary>
    public static IEnumerable<Region> RegionsAt(World world, Guid bodyId, GeoCoordinate spot)
    {
        return world.Regions.Where(r => r.BodyId == bodyId && r.Contains(spot));
    }

    /// <summary>The journal entries and events placed in a region.</summary>
    public static (IEnumerable<JournalEntry> Entries, IEnumerable<TimelineEvent> Events)
        PlacedIn(World world, Guid regionId)
    {
        return (world.Journal.Where(e => e.Location?.RegionId == regionId),
            world.Events.Where(e => e.Location?.RegionId == regionId));
    }

    /// <summary>The events that link to a journal entry, in the world's order.</summary>
    public static IEnumerable<TimelineEvent> EventsLinkedTo(World world, Guid entryId)
    {
        return world.Events.Where(e => e.EntryIds.Contains(entryId));
    }

    private static bool HasDuplicates(IEnumerable<Guid> ids)
    {
        var seen = new HashSet<Guid>();
        return ids.Any(id => !seen.Add(id));
    }
}
