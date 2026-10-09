using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Tests.Geometry;

public sealed class GroundTileGridTests
{
    private const int Cells = 8;

    public static TheoryData<string> Surfaces => ["globe", "flat"];

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GridTriangles_FaceUp(string surfaceName)
    {
        ITileSurface surface = SurfaceNamed(surfaceName);
        foreach (GroundTile tile in SomeTiles(surface))
        {
            Vector3D[] points = Points(surface, tile, out _);
            int[] triangles = GroundTileGrid.Triangles(Cells);
            for (int i = 0; i < Cells * Cells * 6; i += 3)
            {
                Vector3D a = points[triangles[i]], b = points[triangles[i + 1]];
                Vector3D c = points[triangles[i + 2]];
                Vector3D up = Up(surface, (a + b + c) * (1.0 / 3));

                // Clockwise seen from above, as the engine's front faces are.
                Assert.True(Cross(b - a, c - a).Dot(up) < 0, $"{tile}, triangle {i / 3}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void SkirtTriangles_FaceOutFromTheTile(string surfaceName)
    {
        ITileSurface surface = SurfaceNamed(surfaceName);
        foreach (GroundTile tile in SomeTiles(surface))
        {
            Vector3D[] points = Points(surface, tile, out Vector3D middle);
            int[] triangles = GroundTileGrid.Triangles(Cells);
            for (int i = Cells * Cells * 6; i < triangles.Length; i += 3)
            {
                Vector3D a = points[triangles[i]], b = points[triangles[i + 1]];
                Vector3D c = points[triangles[i + 2]];
                Vector3D outward = (a + b + c) * (1.0 / 3) - middle;

                Assert.True(Cross(b - a, c - a).Dot(outward) < 0, $"{tile}, triangle {i / 3}");
            }
        }
    }

    [Fact]
    public void Edge_GoesRoundEveryEdgePointOnce()
    {
        (int Column, int Row)[] edge = GroundTileGrid.Edge(Cells);

        Assert.Equal(4 * Cells, edge.Distinct().Count());
        Assert.All(edge, point => Assert.True(point.Column is 0 or Cells
            || point.Row is 0 or Cells));
    }

    [Fact]
    public void FullyMorphed_EveryTriangleLiesInOneOfTheParentsTriangles()
    {
        // Where each point ends up, in grid units, when fully morphed.
        var morphed = new (double Column, double Row)[GroundTileGrid.GridCount(Cells)];
        for (int row = 0; row <= Cells; row++)
        {
            for (int column = 0; column <= Cells; column++)
            {
                (int a, int b) = GroundTileGrid.MorphPair(column, row, Cells);
                morphed[GroundTileGrid.Index(column, row, Cells)] =
                    ((a % (Cells + 1) + b % (Cells + 1)) / 2.0,
                        (a / (Cells + 1) + b / (Cells + 1)) / 2.0);
            }
        }

        int[] triangles = GroundTileGrid.Triangles(Cells);
        for (int i = 0; i < Cells * Cells * 6; i += 3)
        {
            (double Column, double Row)[] corners =
                [morphed[triangles[i]], morphed[triangles[i + 1]], morphed[triangles[i + 2]]];

            // The parent's square (two of the child's across) and which half of it, from
            // where the child's triangle lies before morphing.
            double centerColumn = 0, centerRow = 0;
            for (int k = 0; k < 3; k++)
            {
                int index = triangles[i + k];
                centerColumn += index % (Cells + 1) / 3.0;
                centerRow += index / (Cells + 1) / 3.0;
            }

            int squareColumn = (int)(centerColumn / 2) * 2, squareRow = (int)(centerRow / 2) * 2;
            bool upperRight = centerColumn - squareColumn > centerRow - squareRow;
            foreach ((double column, double row) in corners)
            {
                double x = column - squareColumn, y = row - squareRow;
                Assert.InRange(x, -1e-9, 2 + 1e-9);
                Assert.InRange(y, -1e-9, 2 + 1e-9);
                Assert.True(upperRight ? x >= y - 1e-9 : x <= y + 1e-9,
                    $"triangle {i / 3} leaves its parent's triangle");
            }
        }
    }

    [Fact]
    public void Triangles_UseEveryPoint()
    {
        int[] triangles = GroundTileGrid.Triangles(Cells);

        Assert.Equal(GroundTileGrid.VertexCount(Cells), triangles.Distinct().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void OddOrTooFewCells_AreRefused(int cells) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => GroundTileGrid.Triangles(cells));

    internal static ITileSurface SurfaceNamed(string name) =>
        name == "globe" ? new GlobeTileSurface() : new FlatTopTileSurface();

    // A root (on a globe), a tile from each root, and a small one deep down.
    private static IEnumerable<GroundTile> SomeTiles(ITileSurface surface)
    {
        for (int root = 0; root < surface.RootCount; root++)
        {
            if (surface is GlobeTileSurface)
            {
                yield return new GroundTile(root, 0, 0, 0);  // A flat root reaches off the disc
            }

            yield return new GroundTile(root, 2, 1, 2);
            yield return new GroundTile(root, 12, 2049, 2050);
        }
    }

    // A tile's points at a gently varying height, its skirt hung below; and its middle.
    private static Vector3D[] Points(ITileSurface surface, GroundTile tile, out Vector3D middle)
    {
        double baseHeight = surface is GlobeTileSurface ? 1 : 0;
        double width = surface.RootWidth * tile.Share;
        var points = new Vector3D[GroundTileGrid.VertexCount(Cells)];
        var bases = new Vector3D[points.Length];
        for (int row = 0; row <= Cells; row++)
        {
            for (int column = 0; column <= Cells; column++)
            {
                (double u, double v) = tile.OnRoot((double)column / Cells, (double)row / Cells);
                int index = GroundTileGrid.Index(column, row, Cells);
                bases[index] = surface.BasePoint(tile.Root, u, v);
                points[index] = surface.Place(bases[index],
                    baseHeight + width * 0.01 * Math.Sin(column + 2.0 * row));
            }
        }

        (int Column, int Row)[] edge = GroundTileGrid.Edge(Cells);
        for (int i = 0; i < edge.Length; i++)
        {
            int index = GroundTileGrid.Index(edge[i].Column, edge[i].Row, Cells);
            points[GroundTileGrid.GridCount(Cells) + i] =
                points[index] - Up(surface, bases[index]) * (width * 0.1);
        }

        middle = points[GroundTileGrid.Index(Cells / 2, Cells / 2, Cells)];
        return points;
    }

    // The way up at a point: out from a globe's middle, or up off a flat world's face.
    private static Vector3D Up(ITileSurface surface, Vector3D point) =>
        surface is GlobeTileSurface ? point * (1 / point.Length) : new Vector3D(0, 1, 0);

    private static Vector3D Cross(Vector3D a, Vector3D b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X);

    [Fact]
    public void SkirtDepths_OnLevelGround_AreTheLeast()
    {
        double[] depths = GroundTileGrid.SkirtDepths(new double[4 * Cells], 5);

        Assert.All(depths, depth => Assert.Equal(5, depth));
    }

    [Fact]
    public void SkirtDepths_UpACliff_ReachTheFoot()
    {
        // The bug: an edge running up a 2 km cliff, the step between points 3 and 4. A coarser
        // neighbor joins points 0 and 8 straight, leaving a crack the old 5 m skirt left open.
        double[] heights = new double[4 * Cells];
        for (int i = 4; i < 4 * Cells - 4; i++)
        {
            heights[i] = 2000;
        }

        double[] depths = GroundTileGrid.SkirtDepths(heights, 5);

        Assert.Equal(2005, depths[4]);       // At the top of the step
        Assert.Equal(2005, depths[11]);      // Eight points past the foot, still in reach
        Assert.Equal(5, depths[12]);         // Nine past, the foot is out of reach
        Assert.Equal(5, depths[3]);          // At the foot
    }

    [Fact]
    public void SkirtDepths_LookAcrossTheCornerWhereTheEdgeCloses()
    {
        double[] heights = new double[4 * Cells];
        heights[0] = 100;

        double[] depths = GroundTileGrid.SkirtDepths(heights, 1);

        Assert.Equal(101, depths[0]);
        Assert.Equal(1, depths[^1]);
    }
}
