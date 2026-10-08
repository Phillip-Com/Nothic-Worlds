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
    public void AHollowsBrim_IsItsLowestPass()
    {
        // A basin inside a 1,000 m rim, with low ground beyond and a 700 m pass through it.
        Vector3D pass = WaterGround.At(20, 32.6);
        HeightGrid ground = WaterGround.Make(at =>
            Within(at, _spot, 2) ? 100 : Within(at, pass, 0.6) ? 700 : Within(at, _spot, 3)
                ? 1000 : 0);

        LakeSeat? seat = LakeFill.HollowBelow(ground, _spot, _ => false, LakeFill.MaxCells);

        Assert.Equal(700, seat?.LevelMeters);
        LakeShape lake = LakeFill.Fill(ground, seat!.Spot, seat.LevelMeters, _ => false);
        Assert.True(lake.Cells.SetEquals(WaterGround.Within(_spot, 2)));
        Assert.Equal(700, ground.HeightAt(WaterCells.CellOf(lake.Outflow!.Value)));
    }

    [Fact]
    public void ASpotOnASlope_FindsTheHollowBelowIt()
    {
        // A bowl 3° across, 100 m deeper each degree in, with the ground falling away beyond.
        HeightGrid ground = WaterGround.Make(at =>
        {
            double degrees = double.RadiansToDegrees(Math.Acos(Math.Clamp(at.Dot(_spot), -1, 1)));
            return degrees < 3 ? 100 * degrees : 300 - 50 * (degrees - 3);
        });

        LakeSeat? seat = LakeFill.HollowBelow(ground, WaterGround.At(21.5, 30), _ => false,
            LakeFill.MaxCells);

        Assert.NotNull(seat);
        Assert.InRange(WaterGround.Degrees(seat.Spot, WaterCells.IndexAt(_spot)), 0, 0.2);
        Assert.InRange(seat.LevelMeters, 280, 300);
    }

    [Fact]
    public void WaterRunningIntoTheSea_HasNoHollow()
    {
        // Ground falling away eastward, into a sea.
        HeightGrid ground = WaterGround.Make(at => 1000 + 500 * at.X);

        Assert.Null(LakeFill.HollowBelow(ground, _spot, cell => ground.HeightAt(
            WaterCells.CellOf(cell)) < 600, LakeFill.MaxCells));
    }

    [Fact]
    public void AHollowWiderThanTheSearch_HasNoBrim()
    {
        Assert.Null(LakeFill.HollowBelow(_basin.Value, _spot, _ => false, maxCells: 100));
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

    private static bool Within(Vector3D at, Vector3D center, double degrees) =>
        at.Dot(center) > Math.Cos(double.DegreesToRadians(degrees));
}
