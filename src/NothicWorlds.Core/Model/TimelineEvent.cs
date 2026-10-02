namespace NothicWorlds.Core.Model;

/// <summary>
/// One event on a timeline (VISION.md LORE-03): something that happened at a moment, or over a
/// span (a war, a reign, a journey), optionally placed somewhere, and linked to any number of
/// journal entries (owner's choice: entries and events are separate things, linked many to
/// many; the links are stored here, on the event). Immutable; change an event by replacing it.
/// </summary>
public sealed record TimelineEvent
{
    /// <summary>The longest a title can be, in characters.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>The longest a description can be, in characters.</summary>
    public const int MaxDescriptionLength = 20_000;

    /// <summary>Stable identity.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The timeline it belongs to.</summary>
    public required Guid TimelineId { get; init; }

    /// <summary>The event's title.</summary>
    public required string Title { get; init; }

    /// <summary>A short description (plain text).</summary>
    public string Description { get; init; } = "";

    /// <summary>When it happens (or begins), in the world's standard days.</summary>
    public required double StartDays { get; init; }

    /// <summary>When it ends, for an event that lasts; null for a moment.</summary>
    public double? EndDays { get; init; }

    /// <summary>Where it happens, or null for nowhere in particular.</summary>
    public LoreLocation? Location { get; init; }

    /// <summary>The journal entries it links to, each at most once.</summary>
    public IReadOnlyList<Guid> EntryIds { get; init; } = [];

    /// <summary>What's wrong with this event on its own, or null if nothing.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Title) || Title.Length > MaxTitleLength)
        {
            return $"a timeline event needs a title of up to {MaxTitleLength} characters";
        }

        if (Description is null || Description.Length > MaxDescriptionLength)
        {
            return $"an event's description can be up to {MaxDescriptionLength:N0} characters";
        }

        if (!double.IsFinite(StartDays)
            || (EndDays is double end && (!double.IsFinite(end) || end < StartDays)))
        {
            return "an event's end can't come before its start";
        }

        return EntryIds is null || EntryIds.Distinct().Count() != EntryIds.Count
            ? "an event links to the same journal entry twice"
            : null;
    }

    /// <summary>Events are equal when every part matches, including the links.</summary>
    public bool Equals(TimelineEvent? other)
    {
        return other is not null
            && Id == other.Id
            && TimelineId == other.TimelineId
            && Title == other.Title
            && Description == other.Description
            && StartDays.Equals(other.StartDays)
            && Nullable.Equals(EndDays, other.EndDays)
            && Equals(Location, other.Location)
            && EntryIds.SequenceEqual(other.EntryIds);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(Id, TimelineId, Title, StartDays, EndDays, EntryIds.Count);
    }
}
