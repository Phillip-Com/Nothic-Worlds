namespace NothicWorlds.Core.Model;

/// <summary>
/// How a terrain type affects the weather (VISION.md WTH-03; owner's choice: a short fixed list,
/// picked per terrain type). See <c>Simulation.ClimateYear</c> for the effects.
/// </summary>
public enum ClimateKind
{
    /// <summary>Plains, fields, tundra, and the like: no special effect (the default).</summary>
    OpenLand,

    /// <summary>Seas and lakes: soften the seasons and the day/night swing nearby.</summary>
    Water,

    /// <summary>Woods: a slightly smaller day/night swing.</summary>
    Forest,

    /// <summary>Dry ground: a much bigger day/night swing, a little warmer.</summary>
    Desert,

    /// <summary>Swamps and marshes: a smaller day/night swing.</summary>
    Wetland,

    /// <summary>High ground: colder, with a bigger day/night swing.</summary>
    Mountains,

    /// <summary>Ice sheets: colder (they reflect most sunlight).</summary>
    Ice,
}
