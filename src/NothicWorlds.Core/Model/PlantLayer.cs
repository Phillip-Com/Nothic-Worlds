namespace NothicWorlds.Core.Model;

/// <summary>
/// The layers plants are scattered in (<see cref="PlantMix"/>): each is placed on its own, so
/// trees can be seen much farther off than what grows under them.
/// </summary>
public enum PlantLayer
{
    /// <summary>Trees: seen far off, as flat pictures past a distance.</summary>
    Trees,

    /// <summary>Bushes, small plants, stones, stumps, and logs: seen only nearby.</summary>
    Undergrowth,
}
