using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Tests.Geometry;

public sealed class GroundTileSelectionTests
{
    private const double SplitFactor = 2;
    private const double FinestWidth = 1e-5;  // About 64 m on an Earth-sized globe
    private const double Reach = 0.05;

    public static TheoryData<string> Surfaces => ["globe", "flat"];

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void AllBuilt_TheDrawnTilesCoverTheReachOnceEach(string surfaceName)
    {
        (GroundTileSelection selection, Vector3D spot, Vector3D eye) = Setup(surfaceName);

        GroundTileChoice choice = selection.Choose(eye, spot, Reach, _ => null,
            _ => TileNeed.None);

        // Points around the spot, out to the edge of reach.
        var random = new Random(7);
        for (int i = 0; i < 2000; i++)
        {
            Vector3D point = Near(selection.Surface, spot, Reach * Math.Sqrt(random.NextDouble()),
                random.NextDouble() * Math.Tau);
            (int root, double u, double v) = selection.Surface.Locate(point)!.Value;
            int holding = choice.Drawn.Count(tile => Holds(tile, root, u, v));
            Assert.True(holding >= 1, $"no tile over point {i}");

            // On a shared edge two can touch; inside one, only one.
            Assert.True(holding <= 2, $"{holding} tiles over point {i}");
        }

        Assert.Empty(choice.Wanted);
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void AllBuilt_TilesAreFinestUnderTheEyeAndCoarserAway(string surfaceName)
    {
        (GroundTileSelection selection, Vector3D spot, Vector3D eye) = Setup(surfaceName);

        GroundTileChoice choice = selection.Choose(eye, spot, Reach, _ => null,
            _ => TileNeed.None);
        GroundTile underfoot = TileOver(selection.Surface, choice, spot);
        GroundTile away = TileOver(selection.Surface, choice,
            Near(selection.Surface, spot, Reach * 0.9, 1));

        Assert.True(selection.Width(underfoot) < 2 * FinestWidth);
        Assert.True(away.Level < underfoot.Level - 5);
    }

    [Fact]
    public void NothingBuilt_NothingIsDrawnAndTheRootsAreWantedFirst()
    {
        (GroundTileSelection selection, Vector3D spot, Vector3D eye) = Setup("globe");

        GroundTileChoice choice = selection.Choose(eye, spot, Reach, _ => null,
            _ => TileNeed.Missing);

        Assert.Empty(choice.Drawn);
        Assert.Equal(0, choice.Wanted[0].Level);
        Assert.True(choice.Wanted.Max(tile => tile.Level) > 10);
        Assert.Equal(choice.Wanted.OrderBy(tile => tile.Level), choice.Wanted);
    }

    [Fact]
    public void QuartersNotAllBuilt_TheTileIsDrawnWhole()
    {
        (GroundTileSelection selection, Vector3D spot, Vector3D eye) = Setup("globe");

        // Only the coarsest three levels are built.
        GroundTileChoice choice = selection.Choose(eye, spot, Reach, _ => null,
            tile => tile.Level <= 2 ? TileNeed.None : TileNeed.Missing);

        Assert.NotEmpty(choice.Drawn);
        Assert.All(choice.Drawn, tile => Assert.True(tile.Level <= 2));
        Assert.Contains(choice.Wanted, tile => tile.Level == 3);
    }

    [Fact]
    public void StaleTiles_AreWantedAfterMissingOnes()
    {
        (GroundTileSelection selection, Vector3D spot, Vector3D eye) = Setup("globe");
        GroundTileChoice all = selection.Choose(eye, spot, Reach, _ => null, _ => TileNeed.None);
        GroundTile stale = all.Drawn[0], missing = all.Drawn[^1];

        GroundTileChoice choice = selection.Choose(eye, spot, Reach, _ => null,
            tile => tile == stale ? TileNeed.Stale
                : tile == missing ? TileNeed.Missing
                : TileNeed.None);

        Assert.Equal([missing, stale], choice.Wanted);
        Assert.Contains(stale, choice.Drawn);
    }

    [Fact]
    public void Kept_HoldsTheDrawnTilesAndEveryOneAboveThem()
    {
        (GroundTileSelection selection, Vector3D spot, Vector3D eye) = Setup("globe");

        GroundTileChoice choice = selection.Choose(eye, spot, Reach, _ => null,
            _ => TileNeed.None);

        Assert.All(choice.Drawn, tile =>
        {
            for (GroundTile up = tile; ; up = up.Parent())
            {
                Assert.Contains(up, choice.Kept);
                if (up.Level == 0)
                {
                    break;
                }
            }
        });
    }

    [Fact]
    public void MorphRange_EndsWhereTheParentStopsBeingSplit()
    {
        (GroundTileSelection selection, _, _) = Setup("globe");
        var tile = new GroundTile(0, 6, 10, 20);

        (double start, double end) = selection.MorphRange(tile);

        Assert.Equal(SplitFactor * selection.Width(tile.Parent()), end, 12);
        Assert.InRange(start, SplitFactor * selection.Width(tile), end);
    }

    [Fact]
    public void FlatTiles_OffTheDiscAreLeftOut()
    {
        var surface = new FlatTopTileSurface();

        Assert.True(surface.Covers(new GroundTile(0, 0, 0, 0)));
        Assert.True(surface.Covers(new GroundTile(0, 1, 0, 0)));
        Assert.False(surface.Covers(new GroundTile(0, 3, 0, 0)));  // The square's far corner
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void Locate_FindsWhereABasePointCameFrom(string surfaceName)
    {
        ITileSurface surface = GroundTileGridTests.SurfaceNamed(surfaceName);
        for (int root = 0; root < surface.RootCount; root++)
        {
            Vector3D point = surface.Place(surface.BasePoint(root, 0.3, 0.6), 1.2);

            (int foundRoot, double u, double v) = surface.Locate(point)!.Value;

            Assert.Equal(root, foundRoot);
            Assert.Equal(0.3, u, 9);
            Assert.Equal(0.6, v, 9);
        }
    }

    [Fact]
    public void Tile_RoundTripsThroughItsParentAndRoot()
    {
        var tile = new GroundTile(2, 5, 13, 7);

        Assert.All(tile.Children(), child => Assert.Equal(tile, child.Parent()));
        (double u, double v) = tile.OnRoot(0.25, 0.75);
        Assert.Equal((0.25, 0.75), tile.FromRoot(u, v));
    }

    // A selection on a surface, a spot on it, and an eye 2 m over it.
    private static (GroundTileSelection, Vector3D Spot, Vector3D Eye) Setup(string surfaceName)
    {
        ITileSurface surface = GroundTileGridTests.SurfaceNamed(surfaceName);
        double baseHeight = surface is GlobeTileSurface ? 1 : 0;
        Vector3D spot = surface.BasePoint(0, 0.43, 0.61);
        return (new GroundTileSelection(surface, SplitFactor, FinestWidth, baseHeight), spot,
            surface.Place(spot, baseHeight + 3e-7));
    }

    // A base point `distance` from another along the surface, toward `bearing`.
    private static Vector3D Near(ITileSurface surface, Vector3D from, double distance,
        double bearing)
    {
        if (surface is GlobeTileSurface)
        {
            return GlobeWalk.Walk(from, bearing, distance);
        }

        return from + new Vector3D(Math.Sin(bearing), 0, Math.Cos(bearing)) * distance;
    }

    private static GroundTile TileOver(ITileSurface surface, GroundTileChoice choice,
        Vector3D point)
    {
        (int root, double u, double v) = surface.Locate(point)!.Value;
        return choice.Drawn.First(tile => Holds(tile, root, u, v));
    }

    private static bool Holds(GroundTile tile, int root, double u, double v)
    {
        (double across, double down) = tile.FromRoot(u, v);
        return tile.Root == root && across is >= 0 and <= 1 && down is >= 0 and <= 1;
    }
}
