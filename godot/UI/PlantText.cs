using NothicWorlds.Core.Model;

namespace NothicWorlds.UI;

/// <summary>
/// Words for the plants a terrain type can grow (VISION.md REN-06): their names and what each
/// grows, for the Terrain panel.
/// </summary>
public static class PlantText
{
    /// <summary>A plant cover's name, e.g. "Conifer forest".</summary>
    public static string Name(PlantCover cover) => cover switch
    {
        PlantCover.Grass => "Grass",
        PlantCover.Meadow => "Meadow",
        PlantCover.Woodland => "Woodland",
        PlantCover.Forest => "Forest",
        PlantCover.ConiferForest => "Conifer forest",
        PlantCover.Jungle => "Jungle",
        PlantCover.Scrub => "Scrub",
        PlantCover.Marsh => "Marsh",
        PlantCover.Rocky => "Rocky",
        _ => "None",
    };

    /// <summary>What a plant cover grows, for its tooltip.</summary>
    public static string Use(PlantCover cover) => cover switch
    {
        PlantCover.Grass => "Grass and the odd bush: fields, steppe, tundra",
        PlantCover.Meadow => "Grass, flowers, bushes, and a few trees: plains and hills",
        PlantCover.Woodland => "Scattered broadleaf trees over grass",
        PlantCover.Forest => "Close broadleaf trees, bushes, and fallen logs",
        PlantCover.ConiferForest => "Close pines, with rocks and logs",
        PlantCover.Jungle => "Palms, broad-leaved plants, and thick bushes",
        PlantCover.Scrub => "Cacti, dry bushes, and stones: deserts and dry hills",
        PlantCover.Marsh => "Willows, tall grass, and leafy plants: swamps and marshes",
        PlantCover.Rocky => "Boulders and stones, with a few bushes and pines",
        _ => "Nothing grows: water, ice, bare ground",
    };
}
