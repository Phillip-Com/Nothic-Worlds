namespace NothicWorlds.Core.Model;

/// <summary>
/// One named timeline (VISION.md LORE-03), e.g. "The Empire" or "House Vael", shown as a lane
/// in the timeline strip. Every <see cref="TimelineEvent"/> belongs to one. Immutable; change a
/// timeline by replacing it.
/// </summary>
public sealed record Timeline
{
    /// <summary>The longest a timeline's name can be, in characters.</summary>
    public const int MaxNameLength = 100;

    /// <summary>Stable identity, used by its events.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The timeline's name.</summary>
    public required string Name { get; init; }

    /// <summary>The color its lane and events are drawn in.</summary>
    public RgbColor Color { get; init; } = new(0x8C, 0xB4, 0xE6);

    /// <summary>True if its lane is hidden from the strip (its events are kept).</summary>
    public bool Hidden { get; init; }

    /// <summary>What's wrong with this timeline on its own, or null if nothing.</summary>
    public string? Problem()
    {
        return string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength
            ? $"a timeline needs a name of up to {MaxNameLength} characters"
            : null;
    }
}
