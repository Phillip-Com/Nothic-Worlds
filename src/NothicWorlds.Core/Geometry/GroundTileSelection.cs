namespace NothicWorlds.Core.Geometry;

/// <summary>
/// Chooses the <see cref="GroundTile"/>s to draw around a first-person eye (VISION.md REN-06):
/// a tile is split into its quarters while the eye is closer to it than
/// <c>splitFactor</c> times its width, down to tiles <c>finestWidth</c> across, and only tiles
/// within reach of the eye are kept. Tiles are built a while after they're asked for, so until
/// a tile's quarters are all built it's drawn whole, and until it's built its parent stands in:
/// there's never a hole (but in the far part of the reach, past the horizon, where a tile just
/// come into reach is left out until it's built), and never two tiles drawn over each other.
/// </summary>
/// <param name="surface">The surface tiled.</param>
/// <param name="splitFactor">How many tile widths away from the eye a tile is split.</param>
/// <param name="finestWidth">The narrowest a tile gets, in radii.</param>
public sealed class GroundTileSelection(ITileSurface surface, double splitFactor,
    double finestWidth)
{
    // The share of the reach beyond which a tile not built yet is left out, not stood in for.
    private const double FarShare = 0.5;

    /// <summary>The surface tiled.</summary>
    public ITileSurface Surface => surface;

    /// <summary>The narrowest a tile gets, in radii.</summary>
    public double FinestWidth => finestWidth;

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
    /// point); <paramref name="extentOf"/> gives the space a tile takes up (for one not built
    /// yet, a guess: see <see cref="Estimate"/>), and <paramref name="needOf"/> what it needs.
    /// Tiles wholly beyond <paramref name="nearReach"/> (all within reach, if null) are wanted
    /// after every nearer one, so the far ground never holds up the ground around the eye.
    /// </summary>
    public GroundTileChoice Choose(Vector3D eye, Vector3D reachCenter, double reach,
        Func<GroundTile, TileExtent> extentOf, Func<GroundTile, TileNeed> needOf,
        double? nearReach = null)
    {
        var drawn = new List<GroundTile>();
        var missing = new List<(GroundTile Tile, double Away, bool Far)>();
        var stale = new List<(GroundTile Tile, double Away, bool Far)>();
        var kept = new HashSet<GroundTile>();
        for (int root = 0; root < surface.RootCount; root++)
        {
            var tile = new GroundTile(root, 0, 0, 0);
            if (surface.Covers(tile) && InReach(tile, reachCenter, reach))
            {
                Visit(tile);
            }
        }

        List<GroundTile> wanted = [.. Wanted(far: false), .. Wanted(far: true)];
        return new GroundTileChoice(drawn, wanted, kept);

        // The near or far tiles wanted: missing ones coarsest first (they stand in for the
        // rest), then stale ones, each nearest first.
        IEnumerable<GroundTile> Wanted(bool far) =>
        [
            .. missing.Where(entry => entry.Far == far).OrderBy(entry => entry.Tile.Level)
                .ThenBy(entry => entry.Away).Select(entry => entry.Tile),
            .. stale.Where(entry => entry.Far == far).OrderBy(entry => entry.Away)
                .Select(entry => entry.Tile),
        ];

        // Chooses tiles for a tile's area; true if it's all covered by drawn tiles.
        bool Visit(GroundTile tile)
        {
            kept.Add(tile);
            double away = extentOf(tile).DistanceFrom(eye);
            TileNeed need = needOf(tile);
            bool far = need != TileNeed.None && nearReach is double near
                && AllBeyond(tile, reachCenter, near);
            if (need == TileNeed.Missing)
            {
                missing.Add((tile, away, far));
            }

            bool built = need != TileNeed.Missing;

            // A tile not built yet in the far half of the reach (one just come into it, as the
            // eye rises and the reach grows) is left out until it is, rather than its parent
            // standing in for all its built neighbors too: out there, well past the horizon, a
            // gap goes unseen, but the whole ground dropping to a coarse tile would not.
            bool leftOut = !built && AllBeyond(tile, reachCenter, reach * FarShare);
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
                    covered &= Visit(quarter);
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

                return built || leftOut;
            }

            if (built)
            {
                drawn.Add(tile);
                if (need == TileNeed.Stale)
                {
                    stale.Add((tile, away, far));
                }
            }

            return built || leftOut;
        }
    }

    private bool ShouldSplit(GroundTile tile, double away) =>
        tile.Level < GroundTile.MaxLevel && Width(tile) / 2 >= finestWidth
        && away < splitFactor * Width(tile);

    // Whether any of a tile is within reach of a base point (a little more, as it's measured
    // from the tile's middle out to its farthest corner).
    private bool InReach(GroundTile tile, Vector3D reachCenter, double reach)
    {
        (double away, double spread) = Span(tile, reachCenter);
        return away - spread <= reach;
    }

    // Whether all of a tile is farther than `distance` from a base point.
    private bool AllBeyond(GroundTile tile, Vector3D from, double distance)
    {
        (double away, double spread) = Span(tile, from);
        return away - spread > distance;
    }

    // How far a tile's middle is from a base point along the surface, and from its middle out
    // to its farthest corner.
    private (double Away, double Spread) Span(GroundTile tile, Vector3D from)
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

        return (surface.Across(from, middle), spread);
    }
}
