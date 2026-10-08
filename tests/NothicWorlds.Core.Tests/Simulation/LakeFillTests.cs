using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class LakeFillTests
{
    private static readonly Vector3D _spot = WaterGround.At(20, 30);

    // A basin 2° across at 100 m, in ground 1,000 m high.
    private static readonly Lazy<HeightGrid> _basin = new(() =>
        WaterGround.Make(at => at.Dot(_spot) > Math.Cos(double.DegreesToRadians(2)) ? 100 : 1000));

    [Fact]
    public void ALake_FillsTheBasinItsSpotIsIn_AndNoMore()
    {
        LakeShape lake = LakeFill.Fill(_basin.Value, _spot, 500, _ => false);

        Assert.Null(lake.Problem);
        Assert.True(lake.Cells.SetEquals(WaterGround.Within(_spot, 2)));
    }

    [Fact]
    public void ALake_FlowsOut_AtTheLowestPointOfItsShore()
    {
        HashSet<int> basin = WaterGround.Within(_spot, 2);
        int notch = basin.SelectMany(WaterCells.Neighbors).First(cell => !basin.Contains(cell));
        HeightGrid ground = WaterGround.Make((cell, at) =>
            cell == notch ? 700 : basin.Contains(cell) ? 100 : 1000);

        LakeShape lake = LakeFill.Fill(ground, _spot, 500, _ => false);

        Assert.Equal(notch, lake.Outflow);
    }

    [Fact]
    public void ALake_StopsAtTheSea()
    {
        // The sea takes the basin's eastern half.
        Vector3D east = WaterGround.At(20, 32);
        bool IsSea(int cell) => WaterGround.CenterOf(cell).Dot(east) > WaterGround
            .CenterOf(cell).Dot(_spot);

        LakeShape lake = LakeFill.Fill(_basin.Value, WaterGround.At(20, 29), 500, IsSea);

        Assert.Null(lake.Problem);
        Assert.NotEmpty(lake.Cells);
        Assert.DoesNotContain(lake.Cells, IsSea);
    }

    [Fact]
    public void ASpotInTheSea_HasNoLake()
    {
        LakeShape lake = LakeFill.Fill(_basin.Value, _spot, 500, _ => true);

        Assert.Empty(lake.Cells);
        Assert.Contains("sea", lake.Problem);
    }

    [Fact]
    public void ASurfaceBelowTheGround_HasNoLake()
    {
        LakeShape lake = LakeFill.Fill(_basin.Value, _spot, 50, _ => false);

        Assert.Empty(lake.Cells);
        Assert.Null(lake.Outflow);
        Assert.Contains("higher", lake.Problem);
    }

    [Fact]
    public void ALakeOverMostOfTheWorld_IsRefused()
    {
        LakeShape lake = LakeFill.Fill(_basin.Value, _spot, 5000, _ => false);

        Assert.Empty(lake.Cells);
        Assert.Contains("too much", lake.Problem);
    }
}
