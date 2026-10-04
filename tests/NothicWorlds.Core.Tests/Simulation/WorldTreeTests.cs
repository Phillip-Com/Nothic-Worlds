using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class WorldTreeTests
{
    [Fact]
    public void ATree_HasOneTipForEachGreatBranch()
    {
        GrownTree tree = WorldTreeShape.Grow(WorldTreeLook.Default);

        Assert.Equal(WorldTreeLook.Default.Branches, tree.BranchTips.Count);
        Assert.True(tree.Leaves.Count > tree.BranchTips.Count);  // Twigs too
    }

    [Fact]
    public void Everything_StaysWithinTheTreesRadius()
    {
        foreach (int seed in new[] { 1, 2, 3, 42 })
        {
            GrownTree tree = WorldTreeShape.Grow(WorldTreeLook.Default with
            {
                Seed = seed,
                Spread = WorldTreeLook.MaxSpread,
                Branches = WorldTreeLook.MaxBranches,
            });

            Assert.All(tree.Pieces, piece =>
            {
                Assert.True(piece.From.Length <= 1);
                Assert.True(piece.To.Length <= 1);
            });
        }
    }

    [Fact]
    public void BranchTips_ReachOutAndUp_RootsReachDown()
    {
        GrownTree tree = WorldTreeShape.Grow(WorldTreeLook.Default);

        Assert.All(tree.BranchTips, tip =>
        {
            Assert.True(tip.Y > 0);
            Assert.True(Math.Sqrt(tip.X * tip.X + tip.Z * tip.Z) > 0.2);
        });
        Assert.Contains(tree.Pieces, piece => piece.To.Y < -0.75);
    }

    [Fact]
    public void TheSameSettings_GrowTheSameTree_AndANewSeedADifferentOne()
    {
        GrownTree first = WorldTreeShape.Grow(WorldTreeLook.Default);
        GrownTree again = WorldTreeShape.Grow(WorldTreeLook.Default);
        GrownTree other = WorldTreeShape.Grow(WorldTreeLook.Default with { Seed = 2 });

        Assert.Equal(first.Pieces, again.Pieces);
        Assert.Equal(first.BranchTips, again.BranchTips);
        Assert.NotEqual(first.BranchTips, other.BranchTips);
    }

    [Fact]
    public void ANewTree_IsUsable()
    {
        World world = World.CreateNew();
        Body star = world.Bodies.Single(b => b.Kind == BodyKind.Star);

        Body tree = NewBodies.WorldTree(world.Bodies, star);

        Assert.Null(tree.Problem());
        Assert.Equal(BodyKind.WorldTree, tree.Kind);
        Assert.False(tree.HasSurface);
        Assert.Equal(star.Id, tree.Orbit!.ParentId);
        Assert.Equal(365.25, tree.DayLengthHours / 24, 9);  // Turns once a year
    }

    [Fact]
    public void OnlyWorldTrees_HaveTreeLooks()
    {
        var tree = new Body { Kind = BodyKind.WorldTree };
        var planet = new Body { Tree = WorldTreeLook.Default };

        Assert.NotNull(tree.Problem());     // Needs its look
        Assert.NotNull(planet.Problem());   // Can't grow branches

        tree.Tree = WorldTreeLook.Default;
        Assert.Null(tree.Problem());
    }

    [Theory]
    [InlineData(2, 0.9, 1)]     // Too few branches
    [InlineData(9, 2.0, 1)]     // Spreads too far
    [InlineData(9, 0.9, 5)]     // Glows too brightly
    public void BadLooks_AreFound(int branches, double spread, double glow)
    {
        Assert.NotNull((WorldTreeLook.Default with
        {
            Branches = branches,
            Spread = spread,
            GlowStrength = glow,
        }).Problem());
    }
}
