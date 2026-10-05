namespace NothicWorlds.Core.Model;

/// <summary>
/// How two lore entries are related (VISION.md LORE-04; owner's choice: a set of kinds the
/// diagrams know how to draw, plus Other with the user's own words). Some run one way ("from"
/// is the parent, the member, the ruler, the servant); the rest are mutual (see
/// <see cref="Relationship.IsMutual"/>).
/// </summary>
public enum RelationshipKind
{
    /// <summary>"From" is a parent of "to". Family trees are laid out from these.</summary>
    ParentOf,

    /// <summary>Married or partnered. Mutual.</summary>
    MarriedTo,

    /// <summary>Brothers and sisters. Mutual.</summary>
    SiblingOf,

    /// <summary>Allies. Mutual.</summary>
    AllyOf,

    /// <summary>Rivals. Mutual.</summary>
    RivalOf,

    /// <summary>At war. Mutual.</summary>
    AtWarWith,

    /// <summary>"From" belongs to "to" (a knight to an order, a city to a nation).</summary>
    MemberOf,

    /// <summary>"From" rules "to".</summary>
    Rules,

    /// <summary>"From" serves "to".</summary>
    Serves,

    /// <summary>
    /// Any other tie, in the relationship's own words, running from "from" to "to".
    /// </summary>
    Other,
}
