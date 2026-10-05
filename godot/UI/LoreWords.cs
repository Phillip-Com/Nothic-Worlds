using Godot;
using NothicWorlds.Core.Model;

namespace NothicWorlds.UI;

/// <summary>
/// Words and colors for entry kinds and relationship kinds (VISION.md LORE-04), shared by the
/// diagram page, the relationship editor, and the Journal panel so they all say the same.
/// </summary>
public static class LoreWords
{
    /// <summary>Every relationship kind, in the order the editor offers them.</summary>
    public static readonly RelationshipKind[] RelationshipKinds =
        [.. Enum.GetValues<RelationshipKind>()];

    /// <summary>Every entry kind, in the order the editor offers them.</summary>
    public static readonly LoreKind[] EntryKinds = [.. Enum.GetValues<LoreKind>()];

    /// <summary>A relationship kind as words: "parent of", "at war with".</summary>
    public static string Kind(RelationshipKind kind) => kind switch
    {
        RelationshipKind.ParentOf => "parent of",
        RelationshipKind.MarriedTo => "married to",
        RelationshipKind.SiblingOf => "sibling of",
        RelationshipKind.AllyOf => "ally of",
        RelationshipKind.RivalOf => "rival of",
        RelationshipKind.AtWarWith => "at war with",
        RelationshipKind.MemberOf => "member of",
        RelationshipKind.Rules => "rules",
        RelationshipKind.Serves => "serves",
        _ => "other…",
    };

    /// <summary>An entry kind as a word: "Character", "Nation".</summary>
    public static string Kind(LoreKind kind) => kind.ToString();

    /// <summary>
    /// A relationship as it reads: its own words if it has them ("pays tribute to"), else its
    /// kind's ("parent of").
    /// </summary>
    public static string Reads(Relationship relationship) =>
        relationship.Label.Length > 0 ? relationship.Label : Kind(relationship.Kind);

    /// <summary>
    /// A relationship as a sentence between its entries' titles: "Aldric parent of Edran".
    /// </summary>
    public static string Sentence(Relationship relationship, IReadOnlyList<JournalEntry> journal)
    {
        return $"{TitleOf(relationship.FromEntryId, journal)} {Reads(relationship)} " +
            TitleOf(relationship.ToEntryId, journal);
    }

    /// <summary>The line color for a relationship kind.</summary>
    public static Color LineColor(RelationshipKind kind) => kind switch
    {
        RelationshipKind.ParentOf => new Color(0.85f, 0.85f, 0.85f),
        RelationshipKind.MarriedTo => new Color(0.95f, 0.75f, 0.85f),
        RelationshipKind.SiblingOf => new Color(0.7f, 0.7f, 0.75f),
        RelationshipKind.AllyOf => new Color(0.45f, 0.85f, 0.5f),
        RelationshipKind.RivalOf => new Color(0.95f, 0.65f, 0.3f),
        RelationshipKind.AtWarWith => new Color(0.95f, 0.35f, 0.35f),
        RelationshipKind.MemberOf => new Color(0.5f, 0.7f, 0.95f),
        RelationshipKind.Rules => new Color(0.75f, 0.55f, 0.95f),
        RelationshipKind.Serves => new Color(0.4f, 0.8f, 0.8f),
        _ => new Color(0.8f, 0.8f, 0.6f),
    };

    /// <summary>The box color for an entry kind (null: plain writing).</summary>
    public static Color BoxColor(LoreKind? kind) => kind switch
    {
        LoreKind.Character => new Color(0.25f, 0.33f, 0.5f),
        LoreKind.Faction => new Color(0.42f, 0.3f, 0.48f),
        LoreKind.Nation => new Color(0.48f, 0.36f, 0.22f),
        LoreKind.Place => new Color(0.24f, 0.42f, 0.32f),
        LoreKind.Other => new Color(0.35f, 0.35f, 0.38f),
        _ => new Color(0.3f, 0.3f, 0.3f),
    };

    /// <summary>The word shown small on an entry's box: its kind, or "Entry" for none.</summary>
    public static string Mark(LoreKind? kind) => kind switch
    {
        LoreKind.Character => "Character",
        LoreKind.Faction => "Faction",
        LoreKind.Nation => "Nation",
        LoreKind.Place => "Place",
        LoreKind.Other => "Other",
        _ => "Entry",
    };

    private static string TitleOf(Guid entryId, IReadOnlyList<JournalEntry> journal) =>
        journal.FirstOrDefault(e => e.Id == entryId)?.Title ?? "(missing)";
}
