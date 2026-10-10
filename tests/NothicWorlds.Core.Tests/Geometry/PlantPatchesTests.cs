using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Tests.Geometry;

public sealed class PlantPatchesTests
{
    private const double EarthMeters = 6_371_000;

    [Fact]
    public void Level_GivesTilesNoWiderThanAsked_AndMoreThanHalf()
    {
        var globe = new GlobeTileSurface();

        int level = PlantPatches.Level(globe, 128, EarthMeters);

        double width = globe.RootWidth * EarthMeters / (1L << level);
        Assert.InRange(width, 64, 128);
    }

    [Theory]
    [InlineData(0.4, 0.6)]          // The middle of a face
    [InlineData(0.99999, 0.5)]      // Beside a cube's edge: tiles on the next face are needed
    [InlineData(0.99999, 0.99999)]  // In a corner
    public void Around_CoversEveryPointWithinReach(double u, double v)
    {
        var globe = new GlobeTileSurface();
        int level = PlantPatches.Level(globe, 128, EarthMeters);
        Vector3D spot = globe.BasePoint(0, u, v);
        double reach = 1000 / EarthMeters;

        HashSet<GroundTile> tiles = PlantPatches.Around(globe, spot, reach, level);

        // Points around the spot, out to the reach, are each on a tile found.
        var random = new Random(3);
        long perRoot = 1L << level;
        for (int i = 0; i < 3000; i++)
        {
            Vector3D point = Near(spot, reach * Math.Sqrt(random.NextDouble()),
                random.NextDouble() * Math.Tau);
            (int root, double pu, double pv) = globe.Locate(point)!.Value;
            var tile = new GroundTile(root, level,
                (int)Math.Min(Math.Floor(pu * perRoot), perRoot - 1),
                (int)Math.Min(Math.Floor(pv * perRoot), perRoot - 1));
            Assert.Contains(tile, tiles);
        }

        // ... and not many more tiles than cover the reach and a tile or two around it.
        double tileWidth = globe.RootWidth / perRoot;
        double most = Math.PI * Math.Pow(reach + 2 * tileWidth, 2) / (tileWidth * tileWidth);
        Assert.InRange(tiles.Count, 1, most * 1.5);
    }

    [Fact]
    public void Around_OffAFlatWorld_GivesNone()
    {
        var flat = new FlatTopTileSurface();

        Assert.Empty(PlantPatches.Around(flat, new Vector3D(50, 0, 50), 0.01, 10));
    }

    [Fact]
    public void AreaSquareMeters_IsTheTilesWidthSquared_NearAFacesMiddle()
    {
        var globe = new GlobeTileSurface();
        var tile = new GroundTile(0, 16, 1 << 15, 1 << 15);
        double width = globe.RootWidth * EarthMeters / (1 << 16);

        Assert.Equal(width * width, PlantPatches.AreaSquareMeters(globe, tile, EarthMeters),
            width * width * 0.01);
    }

    // A point `distance` (radians) from a spot on a unit sphere, toward `bearing`.
    private static Vector3D Near(Vector3D spot, double distance, double bearing)
    {
        Vector3D up = spot * (1 / spot.Length);
        Vector3D side = Math.Abs(up.Z) < 0.9 ? new Vector3D(0, 0, 1) : new Vector3D(1, 0, 0);
        Vector3D east = side.Cross(up);
        east = east * (1 / east.Length);
        Vector3D north = up.Cross(east);
        Vector3D toward = east * Math.Cos(bearing) + north * Math.Sin(bearing);
        return up * Math.Cos(distance) + toward * Math.Sin(distance);
    }
}
