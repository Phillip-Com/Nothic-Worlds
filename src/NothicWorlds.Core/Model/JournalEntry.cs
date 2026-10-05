namespace NothicWorlds.Core.Model;

/// <summary>
/// One journal entry (VISION.md LORE-02): a titled piece of writing about the world, optionally
/// placed somewhere. Timeline events link to entries (see <see cref="TimelineEvent.EntryIds"/>).
/// Immutable; change an entry by replacing it.
/// </summary>
public sealed record JournalEntry
{
    /// <summary>The longest a title can be, in characters.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>The longest an entry's text can be, in characters (a short book).</summary>
    public const int MaxTextLength = 200_000;

    /// <summary>Stable identity, used by links.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The entry's title.</summary>
    public required string Title { get; init; }

    /// <summary>The entry's text (plain text; lines are kept).</summary>
    public string Text { get; init; } = "";

    /// <summary>
    /// What the entry is about (a character, a faction), for its box in relationship
    /// diagrams (VISION.md LORE-04); null for plain writing.
    /// </summary>
    public LoreKind? Kind { get; init; }

    /// <summary>Where it's about, or null for nowhere in particular.</summary>
    public LoreLocation? Location { get; init; }

    /// <summary>When the entry was written (real time, for sorting).</summary>
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>When the entry was last changed (real time, for sorting).</summary>
    public DateTimeOffset EditedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>What's wrong with this entry on its own, or null if nothing.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Title) || Title.Length > MaxTitleLength)
        {
            return $"a journal entry needs a title of up to {MaxTitleLength} characters";
        }

        if (Kind is LoreKind kind && !Enum.IsDefined(kind))
        {
            return "a journal entry has an unknown kind";
        }

        return Text is null || Text.Length > MaxTextLength
            ? $"a journal entry's text can be up to {MaxTextLength:N0} characters"
            : null;
    }
}
