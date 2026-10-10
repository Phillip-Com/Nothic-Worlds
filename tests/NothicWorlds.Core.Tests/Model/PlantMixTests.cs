using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public sealed class PlantMixTests
{
    private static readonly PlantModel[] _trees =
    [
        PlantModel.BroadleafTree, PlantModel.Birch, PlantModel.Pine, PlantModel.Palm,
        PlantModel.Willow,
    ];

    public static TheoryData<PlantCover, PlantLayer> CoversAndLayers()
    {
        var data = new TheoryData<PlantCover, PlantLayer>();
        foreach (PlantCover cover in Enum.GetValues<PlantCover>())
        {
            data.Add(cover, PlantLayer.Trees);
            data.Add(cover, PlantLayer.Undergrowth);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CoversAndLayers))]
    public void Choose_GrowsEachPlantAsThicklyAsTheCoverSays(PlantCover cover, PlantLayer layer)
    {
        const int spots = 100_000;
        double most = PlantMix.MostPerHectare(layer);
        var counts = new Dictionary<PlantModel, int>();
        for (int i = 0; i < spots; i++)
        {
            if (PlantMix.Choose(cover, layer, (i + 0.5) / spots) is PlantShare share)
            {
                counts[share.Model] = counts.GetValueOrDefault(share.Model) + 1;
            }
        }

        // Spots strewn `most` a hectare, so each plant's share of them is its own density.
        var expected = PlantMix.For(cover, layer)
            .GroupBy(share => share.Model)
            .ToDictionary(group => group.Key, group => group.Sum(share => share.PerHectare));
        Assert.Equal(expected.Keys.Order(), counts.Keys.Order());
        foreach ((PlantModel model, double perHectare) in expected)
        {
            Assert.Equal(perHectare, counts[model] * most / spots, perHectare * 0.01 + 0.01);
        }
    }

    [Fact]
    public void None_GrowsNothing()
    {
        Assert.Empty(PlantMix.For(PlantCover.None, PlantLayer.Trees));
        Assert.Empty(PlantMix.For(PlantCover.None, PlantLayer.Undergrowth));
        Assert.Equal(0, PlantMix.Grass(PlantCover.None).PerSquareMeter);
        Assert.Null(PlantMix.Choose(PlantCover.None, PlantLayer.Trees, 0));
    }

    [Fact]
    public void EveryShare_HasASensibleHeightRange()
    {
        foreach (PlantCover cover in Enum.GetValues<PlantCover>())
        {
            foreach (PlantLayer layer in Enum.GetValues<PlantLayer>())
            {
                Assert.All(PlantMix.For(cover, layer), share =>
                {
                    Assert.InRange(share.MinHeightMeters, 0.1, share.MaxHeightMeters);
                    Assert.True(share.PerHectare > 0);
                });
            }
        }
    }

    [Fact]
    public void Trees_AreOnlyTrees_AndUndergrowthHasNone()
    {
        foreach (PlantCover cover in Enum.GetValues<PlantCover>())
        {
            Assert.All(PlantMix.For(cover, PlantLayer.Trees),
                share => Assert.Contains(share.Model, _trees));
            Assert.All(PlantMix.For(cover, PlantLayer.Undergrowth),
                share => Assert.DoesNotContain(share.Model, _trees));
        }
    }

    [Fact]
    public void MostGrass_IsTheThickestCoversGrass()
    {
        Assert.Equal(Enum.GetValues<PlantCover>().Max(c => PlantMix.Grass(c).PerSquareMeter),
            PlantMix.MostGrassPerSquareMeter);
    }
}
