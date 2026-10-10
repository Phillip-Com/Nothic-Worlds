namespace NothicWorlds.Core.Model;

/// <summary>
/// What the ground of a terrain type looks like up close in the standing view (VISION.md
/// REN-06; owner's choice: picked per terrain type, from a short fixed list). Each kind has its
/// own photo material, tinted toward the type's color. Under water it is the bottom.
/// </summary>
public enum GroundKind
{
    /// <summary>Green grass: plains, meadows, hills.</summary>
    Grass,

    /// <summary>Dry, yellowed grass: fields, steppe, tundra.</summary>
    DryGrass,

    /// <summary>Leaf litter and moss under trees.</summary>
    ForestFloor,

    /// <summary>Sand: deserts, beaches, the sea floor.</summary>
    Sand,

    /// <summary>Wet mud: swamps and marshes.</summary>
    Mud,

    /// <summary>Bare rock.</summary>
    Rock,

    /// <summary>Snow and ice.</summary>
    Snow,

    /// <summary>Gravel and stony soil.</summary>
    Gravel,
}
