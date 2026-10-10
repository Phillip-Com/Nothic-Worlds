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
    public void Defaults_HaveTheGroundTheirClimateAndColorSuggest()
    {
        Assert.All(TerrainType.Defaults, type =>
            Assert.Equal(TerrainType.GuessGround(type.Climate, type.Color), type.Ground));
    }

    [Theory]
    [InlineData(ClimateKind.Water, 0x1F, 0x4E, 0x79, GroundKind.Sand)]
    [InlineData(ClimateKind.Desert, 0xB0, 0x30, 0x20, GroundKind.Sand)]
    [InlineData(ClimateKind.Forest, 0xD0, 0xD0, 0xD0, GroundKind.ForestFloor)]
    [InlineData(ClimateKind.Wetland, 0x4F, 0x6B, 0x4A, GroundKind.Mud)]
    [InlineData(ClimateKind.Mountains, 0x7D, 0x6E, 0x62, GroundKind.Rock)]
    [InlineData(ClimateKind.Ice, 0x30, 0x30, 0x30, GroundKind.Snow)]
    [InlineData(ClimateKind.OpenLand, 0xF0, 0xF0, 0xF8, GroundKind.Snow)]      // Near white
    [InlineData(ClimateKind.OpenLand, 0x30, 0x28, 0x20, GroundKind.Mud)]       // Very dark
    [InlineData(ClimateKind.OpenLand, 0xA8, 0xC6, 0x6C, GroundKind.Grass)]     // Green
    [InlineData(ClimateKind.OpenLand, 0x80, 0x80, 0x84, GroundKind.Gravel)]    // Grey
    [InlineData(ClimateKind.OpenLand, 0xC0, 0x60, 0x40, GroundKind.Sand)]      // Red
    [InlineData(ClimateKind.OpenLand, 0xD8, 0xC8, 0x78, GroundKind.DryGrass)]  // Yellow
    public void GuessGround_GoesByClimate_ThenColor(
        ClimateKind climate, byte red, byte green, byte blue, GroundKind expected)
    {
        Assert.Equal(expected, TerrainType.GuessGround(climate, new RgbColor(red, green, blue)));
    }

    [Fact]
    public void Problem_UnknownGround_IsReported()
    {
        TerrainType odd = TerrainType.Defaults[0] with { Ground = (GroundKind)99 };

        Assert.NotNull(TerrainType.Problem([odd]));
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
