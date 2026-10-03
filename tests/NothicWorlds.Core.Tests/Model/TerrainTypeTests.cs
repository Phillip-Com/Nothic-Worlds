using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public sealed class TerrainTypeTests
{
    private static readonly RgbColor _green = new(0x2F, 0x6B, 0x35);

    [Fact]
    public void Defaults_AreTheTwelveStarterTypes_AndValid()
    {
        Assert.Equal(
            ["Ocean", "Shallow Water", "Plains", "Fields", "Forest", "Jungle", "Hills",
                "Mountains", "Desert", "Swamp", "Tundra", "Ice"],
            TerrainType.Defaults.Select(type => type.Name));
        Assert.Equal(Enumerable.Range(1, 12).Select(code => (byte)code),
            TerrainType.Defaults.Select(type => type.Code));
        Assert.Null(TerrainType.Problem(TerrainType.Defaults));
    }

    [Fact]
    public void NewWorlds_StartWithTheDefaults()
    {
        Assert.Equal(TerrainType.Defaults, World.CreateNew().TerrainTypes);
    }

    [Fact]
    public void Problem_FindsCodeZero()
    {
        Assert.NotNull(TerrainType.Problem([new TerrainType(0, "Void", _green)]));
    }

    [Fact]
    public void Problem_FindsSharedCodes()
    {
        Assert.NotNull(TerrainType.Problem(
            [new TerrainType(4, "Forest", _green), new TerrainType(4, "Woods", _green)]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A name far too long to fit in the Terrain panel's list of types, really")]
    public void Problem_FindsBadNames(string name)
    {
        Assert.NotNull(TerrainType.Problem([new TerrainType(4, name, _green)]));
    }

    [Fact]
    public void FreeCode_IsTheLowestUnusedOne()
    {
        TerrainType[] types =
            [new(1, "A", _green), new(2, "B", _green), new(4, "D", _green)];

        Assert.Equal((byte)3, TerrainType.FreeCode(types));
        Assert.Equal((byte)1, TerrainType.FreeCode([]));
    }

    [Fact]
    public void FreeCode_IsNullWhenAllAreTaken()
    {
        TerrainType[] types = [.. Enumerable.Range(1, 255)
            .Select(code => new TerrainType((byte)code, $"Type {code}", _green))];

        Assert.Null(TerrainType.FreeCode(types));
    }

    [Fact]
    public void PaintedTerrain_CountsAsAChangeToTheBody()
    {
        var body = new Body();
        Body copy = body.Clone();

        copy.Surface.Terrain = copy.Surface.Terrain.Paint(new Vector3D(0, 0, 1), 2, 5);

        Assert.True(body.Surface.Terrain.IsEmpty);  // The copy is independent.
        Assert.False(body.HasSameContent(copy));
        Assert.True(copy.HasSameContent(copy.Clone()));
    }

    [Fact]
    public void WorldCopies_KeepTheTerrainTypes()
    {
        World world = World.CreateNew();

        Assert.Equal(world.TerrainTypes, world.Clone().TerrainTypes);
    }
}
