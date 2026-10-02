namespace NothicWorlds.Core.Model;

/// <summary>
/// Rules that tie a world's journal, timelines, and events together (VISION.md LORE-02,
/// LORE-03): every link and reference points at something that exists, and nothing is listed
/// twice.
/// </summary>
public static class LoreRules
{
    /// <summary>The most journal entries a world can hold.</summary>
    public const int MaxEntries = 10_000;

    /// <summary>The most timelines a world can hold.</summary>
    public const int MaxTimelines = 100;

    /// <summary>The most timeline events a world can hold.</summary>
    public const int MaxEvents = 10_000;

    /// <summary>What's wrong with the world's lore, or null if nothing.</summary>
    public static string? Problem(World world)
    {
        if (world.Journal.Count > MaxEntries || world.Timelines.Count > MaxTimelines
            || world.Events.Count > MaxEvents)
        {
            return $"a world can hold up to {MaxEntries:N0} journal entries, " +
                $"{MaxTimelines} timelines, and {MaxEvents:N0} events";
        }

        string? own = world.Journal.Select(e => e.Problem())
            .Concat(world.Timelines.Select(t => t.Problem()))
            .Concat(world.Events.Select(e => e.Problem()))
            .FirstOrDefault(problem => problem is not null);
        if (own is not null)
        {
            return own;
        }

        if (HasDuplicates(world.Journal.Select(e => e.Id))
            || HasDuplicates(world.Timelines.Select(t => t.Id))
            || HasDuplicates(world.Events.Select(e => e.Id)))
        {
            return "two journal entries, timelines, or events share an ID";
        }

        var entries = world.Journal.Select(e => e.Id).ToHashSet();
        var timelines = world.Timelines.Select(t => t.Id).ToHashSet();
        var bodies = world.Bodies.Select(b => b.Id).ToHashSet();
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
        return locations.Any(l => l is not null && !bodies.Contains(l.BodyId))
            ? "a journal entry or event is placed on a body that doesn't exist"
            : null;
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
