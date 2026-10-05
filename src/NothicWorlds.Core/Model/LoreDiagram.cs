namespace NothicWorlds.Core.Model;

/// <summary>
/// A named relationship diagram (VISION.md LORE-04; owner's choice: any number of diagrams,
/// entries placed on each by hand, the relationships shared): which journal entries it shows
/// and where. The lines between them come from the world's <see cref="Relationship"/>s.
/// Immutable; change one by replacing it.
/// </summary>
public sealed record LoreDiagram
{
    /// <summary>The longest a name can be, in characters.</summary>
    public const int MaxNameLength = 100;

    /// <summary>The most entries one diagram can show.</summary>
    public const int MaxPlacements = 1_000;

    /// <summary>How far from the middle a box can sit, in diagram units, either way.</summary>
    public const double MaxCoordinate = 1_000_000;

    /// <summary>Stable identity.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The diagram's name ("House Arren", "The Northern Alliance").</summary>
    public required string Name { get; init; }

    /// <summary>The entries shown and where, each at most once, in drawing order.</summary>
    public IReadOnlyList<DiagramPlacement> Placements { get; init; } = [];

    /// <summary>What's wrong with this diagram on its own, or null if nothing.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            return $"a diagram needs a name of up to {MaxNameLength} characters";
        }

        if (Placements is null || Placements.Count > MaxPlacements)
        {
            return $"a diagram can show up to {MaxPlacements:N0} entries";
        }

        if (Placements.Any(p => p is null || !InRange(p.X) || !InRange(p.Y)))
        {
            return "a diagram has an entry placed out of bounds";
        }

        return Placements.Select(p => p.EntryId).Distinct().Count() != Placements.Count
            ? "a diagram shows the same journal entry twice"
            : null;
    }

    /// <summary>Diagrams are equal when every part matches, including the placements.</summary>
    public bool Equals(LoreDiagram? other)
    {
        return other is not null
            && Id == other.Id
            && Name == other.Name
            && Placements.SequenceEqual(other.Placements);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Id, Name, Placements.Count);

    private static bool InRange(double value) =>
        double.IsFinite(value) && Math.Abs(value) <= MaxCoordinate;
}
