using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class RiverBedNoiseTests
{
    private const int Seed = 1234;

    [Fact]
    public void NoVariation_GivesAnEvenBed()
    {
        Assert.Equal(0, RiverBedNoise.Offset(5_000, 0, 1, 1, Seed));
    }

    [Fact]
    public void TheBed_StaysWithinItsVariation_AndIsTheSameEveryTime()
    {
        double[] first = Along(10, spacingKm: 2, smoothness: 1);
        double[] again = Along(10, spacingKm: 2, smoothness: 1);

        Assert.Equal(first, again);
        Assert.All(first, offset => Assert.InRange(offset, -10, 10));
        Assert.True(first.Max() - first.Min() > 5);  // It really rises and falls.
    }

    [Fact]
    public void EachRiver_HasItsOwnBed()
    {
        int other = RiverBedNoise.SeedFor(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"));

        Assert.NotEqual(Along(10, 2, 1), Along(10, 2, 1, other));
    }

    [Fact]
    public void AtNoSmoothness_TheBedGoesInSteps()
    {
        // Most of the way between rises the stepped bed is level; the smooth one keeps moving.
        static int Level(double[] offsets) =>
            offsets.Zip(offsets.Skip(1)).Count(pair => Math.Abs(pair.First - pair.Second) < 1e-6);

        Assert.True(Level(Along(10, 1, 0)) > 0.8 * 2000);
        Assert.True(Level(Along(10, 1, 1)) < 0.1 * 2000);
    }

    [Fact]
    public void TheRecipe_NeverChanges()
    {
        // Saved worlds keep only the settings, so the beds they show depend on this exactly
        // (docs/world-format.md).
        Assert.Equal((3.2868, 3.1553, -1040893626), (
            Math.Round(RiverBedNoise.Offset(12_345, 10, 2, 1, Seed), 4),
            Math.Round(RiverBedNoise.Offset(12_345, 10, 2, 0.25, Seed), 4),
            RiverBedNoise.SeedFor(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"))));
    }

    // The bed's offsets every 10 m along 20 km of a river.
    private static double[] Along(double variationMeters, double spacingKm, double smoothness,
        int seed = Seed) =>
        [.. Enumerable.Range(0, 2001).Select(i =>
            RiverBedNoise.Offset(i * 10.0, variationMeters, spacingKm, smoothness, seed))];
}
