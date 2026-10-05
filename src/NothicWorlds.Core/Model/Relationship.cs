namespace NothicWorlds.Core.Model;

/// <summary>
/// A tie between two journal entries (VISION.md LORE-04): a parent and child, a marriage, an
/// alliance, a war. Stored once in the world and shown in every diagram that holds both ends
/// (owner's choice). It can start and end at moments on the world clock (owner's choice), so
/// diagrams show the ties as they stand at the clock's time. Immutable; change one by
/// replacing it.
/// </summary>
public sealed record Relationship
{
    /// <summary>The longest a label can be, in characters.</summary>
    public const int MaxLabelLength = 100;

    /// <summary>Stable identity.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The entry the tie runs from (the parent, the member, the ruler).</summary>
    public required Guid FromEntryId { get; init; }

    /// <summary>The entry the tie runs to.</summary>
    public required Guid ToEntryId { get; init; }

    /// <summary>What kind of tie it is.</summary>
    public required RelationshipKind Kind { get; init; }

    /// <summary>
    /// The user's words for it: required for <see cref="RelationshipKind.Other"/> ("sworn
    /// enemy of"), optional for the rest (empty for none).
    /// </summary>
    public string Label { get; init; } = "";

    /// <summary>When it began, in the world's standard days; null for always.</summary>
    public double? StartDays { get; init; }

    /// <summary>When it ended, in the world's standard days; null for never.</summary>
    public double? EndDays { get; init; }

    /// <summary>
    /// True if it runs both ways alike (married, siblings, allies, rivals, at war).
    /// </summary>
    public bool IsMutual => Kind is RelationshipKind.MarriedTo or RelationshipKind.SiblingOf
        or RelationshipKind.AllyOf or RelationshipKind.RivalOf or RelationshipKind.AtWarWith;

    /// <summary>
    /// True if the tie stands at <paramref name="timeDays"/>: on or after its start, and on
    /// or before its end.
    /// </summary>
    public bool StandsAt(double timeDays) =>
        (StartDays is not double start || timeDays >= start)
        && (EndDays is not double end || timeDays <= end);

    /// <summary>What's wrong with this relationship on its own, or null if nothing.</summary>
    public string? Problem()
    {
        if (FromEntryId == ToEntryId)
        {
            return "a relationship needs two different journal entries";
        }

        if (!Enum.IsDefined(Kind))
        {
            return "a relationship has an unknown kind";
        }

        if (Label is null || Label.Length > MaxLabelLength
            || (Kind == RelationshipKind.Other && string.IsNullOrWhiteSpace(Label)))
        {
            return $"a relationship's label can be up to {MaxLabelLength} characters, " +
                "and an 'other' relationship needs one";
        }

        bool badStart = StartDays is double start && !double.IsFinite(start);
        bool badEnd = EndDays is double end
            && (!double.IsFinite(end) || (StartDays is double from && end < from));
        return badStart || badEnd ? "a relationship can't end before it starts" : null;
    }
}
