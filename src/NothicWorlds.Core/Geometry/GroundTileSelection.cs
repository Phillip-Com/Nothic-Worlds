namespace NothicWorlds.Core.Geometry;

/// <summary>
/// Chooses the <see cref="GroundTile"/>s to draw around a first-person eye (VISION.md REN-06):
/// a tile is split into its quarters while the eye is closer to it than
/// <c>splitFactor</c> times its width, down to tiles <c>finestWidth</c> across, and only tiles
/// within reach of the eye are kept. Tiles are built a while after they're asked for, so until
/// a tile's quarters are all built it's drawn whole, and until it's built its parent stands in:
/// there's never a hole, and never two tiles drawn over each other.
/// </summary>
/// <param name="surface">The surface tiled.</param>
/// <param name="splitFactor">How many tile widths away from the eye a tile is split.</param>
/// <param name="finestWidth">The narrowest a tile gets, in radii.</param>
/// <param name="baseHeight">The bare surface's height, for guessing an unbuilt tile's extent
/// when nothing above it is built either.</param>
public sealed class GroundTileSelection(ITileSurface surface, double splitFactor,
    double finestWidth, double baseHeight)
{
    /// <summary>The surface tiled.</summary>
    public ITileSurface Surface => surface;

    /// <summary>A tile's width along the surface, in radii.</summary>
    public double Width(GroundTile tile) => surface.RootWidth * tile.Share;

    /// <summary>
    /// How far from the eye (in radii) a tile's points start morphing onto its parent's shape,
    /// and how far they've finished: by the distance at which its parent is no longer split,
    /// where a coarser neighbor may be, so their edges meet.
    /// </summary>
    public (double Start, double End) MorphRange(GroundTile tile)
    {
        double end = 2 * splitFactor * Width(tile);
        return (end * 0.75, end);
    }

    /// <summary>
    /// A guess at the space an unbuilt tile takes up, its heights between
    /// <paramref name="lowest"/> and <paramref name="highest"/>.
    /// </summary>
    public TileExtent Estimate(GroundTile tile, double lowest, double highest)
    {
        var points = new List<Vector3D>(18);
        for (int i = 0; i <= 2; i++)
        {
            for (int j = 0; j <= 2; j++)
            {
                (double u, double v) = tile.OnRoot(i / 2.0, j / 2.0);
                Vector3D basePoint = surface.BasePoint(tile.Root, u, v);
                points.Add(surface.Place(basePoint, lowest));
                points.Add(surface.Place(basePoint, highest));
            }
        }

        return TileExtent.Around(points, lowest, highest);
    }

    /// <summary>
    /// The tiles to draw for an eye at <paramref name="eye"/> (in the body's own space), within
    /// <paramref name="reach"/> (along the surface) of <paramref name="reachCenter"/> (a base
    /// point); <paramref name="extentOf"/> gives a built tile's extent (null if it isn't built),
    /// and <paramref name="needOf"/> what it needs.
    /// </summary>
    public GroundTileChoice Choose(Vector3D eye, Vector3D reachCenter, double reach,
        Func<GroundTile, TileExtent?> extentOf, Func<GroundTile, TileNeed> needOf)
    {
        var drawn = new List<GroundTile>();
        var missing = new List<(GroundTile Tile, double Away)>();
        var stale = new List<(GroundTile Tile, double Away)>();
        var kept = new HashSet<GroundTile>();
        for (int root = 0; root < surface.RootCount; root++)
        {
            var tile = new GroundTile(root, 0, 0, 0);
            if (surface.Covers(tile) && InReach(tile, reachCenter, reach))
            {
                Visit(tile, baseHeight, baseHeight);
            }
        }

        List<GroundTile> wanted =
        [
            .. missing.OrderBy(entry => entry.Tile.Level).ThenBy(entry => entry.Away)
                .Select(entry => entry.Tile),
            .. stale.OrderBy(entry => entry.Away).Select(entry => entry.Tile),
        ];
        return new GroundTileChoice(drawn, wanted, kept);

        // Chooses tiles for a tile's area; true if it's all covered by drawn tiles.
        bool Visit(GroundTile tile, double lowest, double highest)
        {
            kept.Add(tile);
            TileExtent extent = extentOf(tile) ?? Estimate(tile, lowest, highest);
            double away = extent.DistanceFrom(eye);
            TileNeed need = needOf(tile);
            if (need == TileNeed.Missing)
            {
                missing.Add((tile, away));
            }

            bool built = need != TileNeed.Missing;
            GroundTile[] quarters = ShouldSplit(tile, away)
                ? [.. tile.Children().Where(child => surface.Covers(child)
                    && InReach(child, reachCenter, reach))]
                : [];
            if (quarters.Length > 0)
            {
                int mark = drawn.Count;
                bool covered = true;
                foreach (GroundTile quarter in quarters)
                {
                    // Every quarter's visited, covered or not, so all that's wanted is asked for.
                    covered &= Visit(quarter, extent.Lowest, extent.Highest);
                }

                if (covered)
                {
                    return true;
                }

                drawn.RemoveRange(mark, drawn.Count - mark);
                if (built)
                {
                    drawn.Add(tile);
                }

                return built;
            }

            if (built)
            {
                drawn.Add(tile);
                if (need == TileNeed.Stale)
                {
                    stale.Add((tile, away));
                }
            }

            return built;
        }
    }

    private bool ShouldSplit(GroundTile tile, double away) =>
        tile.Level < GroundTile.MaxLevel && Width(tile) / 2 >= finestWidth
        && away < splitFactor * Width(tile);

    // Whether any of a tile is within reach of a base point (a little more, as it's measured
    // from the tile's middle out to its farthest corner).
    private bool InReach(GroundTile tile, Vector3D reachCenter, double reach)
    {
        (double u, double v) = tile.OnRoot(0.5, 0.5);
        Vector3D middle = surface.BasePoint(tile.Root, u, v);
        double spread = 0;
        foreach ((double across, double down) in (ReadOnlySpan<(double, double)>)
            [(0, 0), (1, 0), (0, 1), (1, 1)])
        {
            (double cu, double cv) = tile.OnRoot(across, down);
            spread = Math.Max(spread, surface.Across(middle,
                surface.BasePoint(tile.Root, cu, cv)));
        }

        return surface.Across(reachCenter, middle) - spread <= reach;
    }
}
