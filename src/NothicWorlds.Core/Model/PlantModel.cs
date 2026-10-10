namespace NothicWorlds.Core.Model;

/// <summary>
/// A plant (or stone) that can stand on the ground in the standing view (VISION.md REN-06).
/// Each is drawn from a few shapes of its kind; <see cref="PlantMix"/> says which grow on each
/// <see cref="PlantCover"/>.
/// </summary>
public enum PlantModel
{
    /// <summary>A broadleaf tree, like an oak or a beech.</summary>
    BroadleafTree,

    /// <summary>A birch.</summary>
    Birch,

    /// <summary>A pine.</summary>
    Pine,

    /// <summary>A palm.</summary>
    Palm,

    /// <summary>A willow.</summary>
    Willow,

    /// <summary>A leafy bush.</summary>
    Bush,

    /// <summary>A bush with berries.</summary>
    BerryBush,

    /// <summary>A low, broad-leaved plant, like a fern.</summary>
    LeafyPlant,

    /// <summary>A clump of flowers.</summary>
    Flowers,

    /// <summary>A clump of tall grass or reeds.</summary>
    TallGrass,

    /// <summary>A cactus.</summary>
    Cactus,

    /// <summary>A flowering cactus.</summary>
    FloweringCactus,

    /// <summary>A bare stone or boulder.</summary>
    Boulder,

    /// <summary>A stone or boulder grown over with moss.</summary>
    MossyBoulder,

    /// <summary>A tree stump.</summary>
    Stump,

    /// <summary>A fallen log.</summary>
    Log,
}
