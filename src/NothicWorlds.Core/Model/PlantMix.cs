namespace NothicWorlds.Core.Model;

/// <summary>
/// What grows on each <see cref="PlantCover"/> in the standing view (VISION.md REN-06), and
/// how thickly: the plants of each <see cref="PlantLayer"/>, and the short grass under them.
/// <see cref="Geometry.PlantScatter"/> places them, as thickly as the thickest cover asks;
/// <see cref="Choose"/> then keeps the right share at each spot for the cover there.
/// </summary>
public static class PlantMix
{
    private static readonly PlantShare[] _none = [];

    // Per hectare, on level ground. Real forests are thicker, but these trees are drawn with
    // broad crowns, and fewer keeps them light enough for the baseline laptop.
    private static readonly Dictionary<PlantCover, PlantShare[]> _trees = new()
    {
        [PlantCover.Grass] = [new(PlantModel.BroadleafTree, 0.5, 8, 14)],
        [PlantCover.Meadow] =
        [
            new(PlantModel.BroadleafTree, 4, 9, 16),
            new(PlantModel.Birch, 2, 10, 16),
        ],
        [PlantCover.Woodland] =
        [
            new(PlantModel.BroadleafTree, 40, 9, 16),
            new(PlantModel.Birch, 20, 10, 17),
        ],
        [PlantCover.Forest] =
        [
            new(PlantModel.BroadleafTree, 150, 10, 18),
            new(PlantModel.Birch, 50, 11, 18),
        ],
        [PlantCover.ConiferForest] = [new(PlantModel.Pine, 220, 12, 24)],
        [PlantCover.Jungle] =
        [
            new(PlantModel.Palm, 60, 8, 15),
            new(PlantModel.BroadleafTree, 120, 12, 22),
        ],
        [PlantCover.Marsh] = [new(PlantModel.Willow, 15, 7, 11)],
        [PlantCover.Rocky] = [new(PlantModel.Pine, 6, 6, 14)],
    };

    private static readonly Dictionary<PlantCover, PlantShare[]> _undergrowth = new()
    {
        [PlantCover.Grass] =
        [
            new(PlantModel.Bush, 5, 0.8, 1.6),
            new(PlantModel.TallGrass, 300, 0.6, 1.1),
        ],
        [PlantCover.Meadow] =
        [
            new(PlantModel.Bush, 20, 0.8, 1.8),
            new(PlantModel.BerryBush, 5, 0.8, 1.5),
            new(PlantModel.Flowers, 400, 0.3, 0.6),
            new(PlantModel.TallGrass, 200, 0.6, 1.1),
            new(PlantModel.Boulder, 3, 0.4, 1.5),
        ],
        [PlantCover.Woodland] =
        [
            new(PlantModel.Bush, 60, 0.8, 1.8),
            new(PlantModel.BerryBush, 15, 0.8, 1.5),
            new(PlantModel.LeafyPlant, 100, 0.4, 1.0),
            new(PlantModel.Flowers, 50, 0.3, 0.6),
            new(PlantModel.Stump, 5, 0.4, 0.7),
            new(PlantModel.Log, 5, 0.6, 0.9),
            new(PlantModel.MossyBoulder, 5, 0.5, 1.5),
        ],
        [PlantCover.Forest] =
        [
            new(PlantModel.Bush, 150, 0.8, 2.0),
            new(PlantModel.LeafyPlant, 300, 0.4, 1.0),
            new(PlantModel.Stump, 15, 0.4, 0.7),
            new(PlantModel.Log, 20, 0.6, 0.9),
            new(PlantModel.MossyBoulder, 10, 0.5, 1.8),
        ],
        [PlantCover.ConiferForest] =
        [
            new(PlantModel.Bush, 60, 0.8, 1.6),
            new(PlantModel.Boulder, 20, 0.4, 2.0),
            new(PlantModel.MossyBoulder, 20, 0.5, 2.0),
            new(PlantModel.Log, 20, 0.6, 0.9),
            new(PlantModel.Stump, 15, 0.4, 0.7),
        ],
        [PlantCover.Jungle] =
        [
            new(PlantModel.Bush, 400, 1.0, 2.5),
            new(PlantModel.LeafyPlant, 800, 0.6, 1.5),
            new(PlantModel.Log, 10, 0.7, 1.0),
        ],
        [PlantCover.Scrub] =
        [
            new(PlantModel.Cactus, 25, 1.0, 3.0),
            new(PlantModel.FloweringCactus, 10, 1.5, 4.0),
            new(PlantModel.Bush, 15, 0.6, 1.2),
            new(PlantModel.Boulder, 25, 0.3, 1.8),
        ],
        [PlantCover.Marsh] =
        [
            new(PlantModel.TallGrass, 1500, 0.8, 1.6),
            new(PlantModel.LeafyPlant, 200, 0.4, 1.0),
            new(PlantModel.Bush, 30, 0.8, 1.6),
        ],
        [PlantCover.Rocky] =
        [
            new(PlantModel.Boulder, 250, 0.3, 2.5),
            new(PlantModel.MossyBoulder, 50, 0.4, 2.0),
            new(PlantModel.Bush, 15, 0.6, 1.2),
        ],
    };

    // Tufts of short grass a square meter, and how tall they grow (meters).
    private static readonly Dictionary<PlantCover, (double PerSquareMeter, double Height)>
        _grass = new()
        {
            [PlantCover.Grass] = (3, 0.35),
            [PlantCover.Meadow] = (4, 0.3),
            [PlantCover.Woodland] = (2.5, 0.25),
            [PlantCover.Forest] = (0.6, 0.2),
            [PlantCover.ConiferForest] = (0.4, 0.2),
            [PlantCover.Jungle] = (1.5, 0.35),
            [PlantCover.Scrub] = (0.3, 0.2),
            [PlantCover.Marsh] = (3, 0.5),
            [PlantCover.Rocky] = (0.6, 0.15),
        };

    // MostPerHectare for each layer, worked out once: Choose is asked for every spot. (Below
    // the tables, so they're filled first.)
    private static readonly double _mostTrees = Most(_trees);
    private static readonly double _mostUndergrowth = Most(_undergrowth);

    /// <summary>The plants of a layer growing on a cover (none for an unknown cover).</summary>
    public static IReadOnlyList<PlantShare> For(PlantCover cover, PlantLayer layer) =>
        (layer == PlantLayer.Trees ? _trees : _undergrowth).GetValueOrDefault(cover, _none);

    /// <summary>
    /// The most plants of a layer any cover grows a hectare: how thickly
    /// <see cref="Geometry.PlantScatter"/> places spots for it.
    /// </summary>
    public static double MostPerHectare(PlantLayer layer) =>
        layer == PlantLayer.Trees ? _mostTrees : _mostUndergrowth;

    /// <summary>
    /// What grows at a spot of a layer placed <see cref="MostPerHectare"/> thick, given the
    /// cover there and the spot's <paramref name="pick"/> (0 to 1, even): each plant takes its
    /// share of the range, so it grows as thickly as the cover says; null past them all.
    /// </summary>
    public static PlantShare? Choose(PlantCover cover, PlantLayer layer, double pick)
    {
        double reach = pick * MostPerHectare(layer);
        foreach (PlantShare share in For(cover, layer))
        {
            if (reach < share.PerHectare)
            {
                return share;
            }

            reach -= share.PerHectare;
        }

        return null;
    }

    /// <summary>
    /// The short grass on a cover: tufts a square meter, and their height in meters (none on
    /// an unknown cover).
    /// </summary>
    public static (double PerSquareMeter, double HeightMeters) Grass(PlantCover cover) =>
        _grass.GetValueOrDefault(cover, (0, 0));

    /// <summary>The most tufts of short grass any cover grows a square meter.</summary>
    public static double MostGrassPerSquareMeter { get; } =
        _grass.Values.Max(grass => grass.PerSquareMeter);

    private static double Most(Dictionary<PlantCover, PlantShare[]> layer) =>
        layer.Values.Max(shares => shares.Sum(share => share.PerHectare));
}
