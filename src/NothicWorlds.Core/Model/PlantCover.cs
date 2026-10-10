namespace NothicWorlds.Core.Model;

/// <summary>
/// What grows on a terrain type in the standing view (VISION.md REN-06; owner's choice: picked
/// per terrain type, from a short fixed list, like its <see cref="GroundKind"/>). What each
/// one grows, and how thickly, is <see cref="PlantMix.For"/>.
/// </summary>
public enum PlantCover
{
    /// <summary>Nothing: water, ice, bare ground.</summary>
    None,

    /// <summary>Grass and the odd bush: fields, steppe, tundra.</summary>
    Grass,

    /// <summary>Grass, flowers, bushes, and a few trees: plains and hills.</summary>
    Meadow,

    /// <summary>Scattered broadleaf trees over grass.</summary>
    Woodland,

    /// <summary>Close broadleaf trees, bushes, and fallen logs.</summary>
    Forest,

    /// <summary>Close pines, with rocks and logs.</summary>
    ConiferForest,

    /// <summary>Palms, broad-leaved plants, and thick bushes.</summary>
    Jungle,

    /// <summary>Cacti, dry bushes, and stones: deserts and dry hills.</summary>
    Scrub,

    /// <summary>Willows, tall grass, and leafy plants: swamps and marshes.</summary>
    Marsh,

    /// <summary>Boulders and stones, with a few bushes and pines.</summary>
    Rocky,
}
