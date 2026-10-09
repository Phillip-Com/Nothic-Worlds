namespace NothicWorlds.Core.Geometry;

/// <summary>
/// How a <see cref="GroundTile"/>'s mesh is laid out (VISION.md REN-06): a square grid of
/// <c>cells × cells</c> squares (an even number), its points row by row, then a skirt hanging
/// below its edge (one point under each edge point, going round) that hides any crack against a
/// coarser neighbor. Each square is split along the same diagonal as its parent tile's, so a
/// tile's points can morph onto the parent's shape: the even points are the parent's own, and
/// each odd one moves to the middle of the parent's edge or diagonal it lies on.
/// </summary>
public static class GroundTileGrid
{
    /// <summary>How many points a tile's grid has (not counting the skirt).</summary>
    public static int GridCount(int cells) => (cells + 1) * (cells + 1);

    /// <summary>How many points a tile's mesh has, skirt and all.</summary>
    public static int VertexCount(int cells) => GridCount(cells) + 4 * cells;

    /// <summary>The index of a grid point.</summary>
    public static int Index(int column, int row, int cells) => row * (cells + 1) + column;

    /// <summary>
    /// The grid points round the edge, in the skirt's order: along the top left to right, down
    /// the right, back along the bottom, and up the left (clockwise, seen from above).
    /// </summary>
    public static (int Column, int Row)[] Edge(int cells)
    {
        RequireEven(cells);
        var edge = new (int, int)[4 * cells];
        for (int i = 0; i < cells; i++)
        {
            edge[i] = (i, 0);
            edge[cells + i] = (cells, i);
            edge[2 * cells + i] = (cells - i, cells);
            edge[3 * cells + i] = (0, cells - i);
        }

        return edge;
    }

    /// <summary>
    /// How many edge points either way a skirt point looks for the lowest ground: a neighbor up
    /// to three levels coarser joins the points this many apart with straight lines.
    /// </summary>
    public const int SkirtReach = 8;

    /// <summary>
    /// How far the skirt hangs under each edge point (heights in <see cref="Edge"/>'s order):
    /// <paramref name="least"/> more than the drop to the lowest edge point within
    /// <see cref="SkirtReach"/> either way (going round). Where the edge runs up a cliff, a
    /// coarser neighbor's straight edge leaves a crack as tall as the cliff, which a skirt of a
    /// set depth wouldn't reach (seen as slits of sky through a cliff face).
    /// </summary>
    public static double[] SkirtDepths(IReadOnlyList<double> edgeHeights, double least)
    {
        int count = edgeHeights.Count;
        var depths = new double[count];
        for (int i = 0; i < count; i++)
        {
            double lowest = edgeHeights[i];
            for (int step = -SkirtReach; step <= SkirtReach; step++)
            {
                lowest = Math.Min(lowest, edgeHeights[((i + step) % count + count) % count]);
            }

            depths[i] = least + edgeHeights[i] - lowest;
        }

        return depths;
    }

    /// <summary>
    /// The two grid points whose middle a point moves to as the tile morphs onto its parent's
    /// shape (the point itself twice, for one the parent has too).
    /// </summary>
    public static (int A, int B) MorphPair(int column, int row, int cells) =>
        (column % 2, row % 2) switch
        {
            (0, 0) => (Index(column, row, cells), Index(column, row, cells)),
            (1, 0) => (Index(column - 1, row, cells), Index(column + 1, row, cells)),
            (0, 1) => (Index(column, row - 1, cells), Index(column, row + 1, cells)),
            _ => (Index(column - 1, row - 1, cells), Index(column + 1, row + 1, cells)),
        };

    /// <summary>
    /// The mesh's triangles: the grid's (each square split from its top-left corner to its
    /// bottom-right), then the skirt's; all wound clockwise seen from above and outside (the
    /// engine's front faces).
    /// </summary>
    public static int[] Triangles(int cells)
    {
        RequireEven(cells);
        var indices = new List<int>((cells * cells + 4 * cells) * 6);
        for (int row = 0; row < cells; row++)
        {
            for (int column = 0; column < cells; column++)
            {
                int topLeft = Index(column, row, cells), topRight = topLeft + 1;
                int bottomLeft = Index(column, row + 1, cells), bottomRight = bottomLeft + 1;
                indices.AddRange([topLeft, topRight, bottomRight]);
                indices.AddRange([topLeft, bottomRight, bottomLeft]);
            }
        }

        (int Column, int Row)[] edge = Edge(cells);
        int skirt = GridCount(cells);
        for (int i = 0; i < edge.Length; i++)
        {
            int next = (i + 1) % edge.Length;
            int top = Index(edge[i].Column, edge[i].Row, cells);
            int topNext = Index(edge[next].Column, edge[next].Row, cells);
            indices.AddRange([top, skirt + i, topNext]);
            indices.AddRange([topNext, skirt + i, skirt + next]);
        }

        return [.. indices];
    }

    private static void RequireEven(int cells)
    {
        if (cells < 2 || cells % 2 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cells), cells,
                "A tile needs an even number of cells, at least 2.");
        }
    }
}
