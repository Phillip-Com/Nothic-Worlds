namespace NothicWorlds.Core.Model;

/// <summary>
/// What a journal entry is about (VISION.md LORE-04; owner's choice: entries are the boxes in
/// relationship diagrams, with an optional kind for their look). Entries without one are
/// simply writing.
/// </summary>
public enum LoreKind
{
    /// <summary>A person, creature, or god.</summary>
    Character,

    /// <summary>A group: a house, guild, order, church, or company.</summary>
    Faction,

    /// <summary>A nation, kingdom, or empire.</summary>
    Nation,

    /// <summary>A place: a city, fortress, or landmark.</summary>
    Place,

    /// <summary>Anything else that takes part in relationships.</summary>
    Other,
}
