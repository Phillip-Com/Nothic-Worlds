using NothicWorlds.Core.Model;

namespace NothicWorlds.UI;

/// <summary>
/// Words for the ground kinds a terrain type can have (VISION.md REN-06): their names and what
/// each looks like, for the Terrain panel.
/// </summary>
public static class GroundText
{
    /// <summary>A ground kind's name, e.g. "Forest floor".</summary>
    public static string Name(GroundKind kind) => kind switch
    {
        GroundKind.DryGrass => "Dry grass",
        GroundKind.ForestFloor => "Forest floor",
        GroundKind.Sand => "Sand",
        GroundKind.Mud => "Mud",
        GroundKind.Rock => "Rock",
        GroundKind.Snow => "Snow",
        GroundKind.Gravel => "Gravel",
        _ => "Grass",
    };

    /// <summary>Where a ground kind fits, for its tooltip.</summary>
    public static string Use(GroundKind kind) => kind switch
    {
        GroundKind.DryGrass => "Yellowed grass: fields, steppe, tundra",
        GroundKind.ForestFloor => "Leaf litter, twigs, and moss under trees",
        GroundKind.Sand => "Deserts, beaches, and the sea floor",
        GroundKind.Mud => "Wet ground: swamps and marshes",
        GroundKind.Rock => "Bare rock",
        GroundKind.Snow => "Snow and ice",
        GroundKind.Gravel => "Gravel and stony soil",
        _ => "Green grass: plains, meadows, hills",
    };
}
