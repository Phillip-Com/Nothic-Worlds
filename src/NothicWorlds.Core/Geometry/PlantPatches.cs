namespace NothicWorlds.Core.Geometry;

/// <summary>
/// The squares of ground plants are scattered over around an eye in the standing view
/// (VISION.md REN-06): <see cref="GroundTile"/>s of one level, fixed on the surface, so each
/// keeps its plants (<see cref="PlantScatter"/>) wherever the eye is.
/// </summary>
public static class PlantPatches
{
    /// <summary>
    /// The level whose tiles are at most <paramref name="meters"/> wide (and more than half
    /// that) on a surface whose unit is <paramref name="radiusMeters"/>.
    /// </summary>
    public static int Level(ITileSurface surface, double meters, double radiusMeters)
    {
        double rootMeters = surface.RootWidth * radiusMeters;
        double halvings = Math.Ceiling(Math.Log2(rootMeters / meters));
        return (int)Math.Clamp(halvings, 0, GroundTile.MaxLevel);
    }

    /// <summary>
    /// The tiles of <paramref name="level"/> that may have ground within
    /// <paramref name="reach"/> (along the surface, in its units) of a base point: found by
    /// looking at points half a tile apart around it, so tiles on other root squares (across
    /// a cube's edge) are found too. Off a flat world's edge there are none.
    /// </summary>
    public static HashSet<GroundTile> Around(ITileSurface surface, Vector3D basePoint,
        double reach, int level)
    {
        var tiles = new HashSet<GroundTile>();
        if (surface.Locate(basePoint) is not var (root, u, v))
        {
            return tiles;
        }

        // Two directions along the surface at the point, square to each other (a cube face's
        // grid is skewed toward its corners), from the root square's own grid.
        const double step = 1e-6;
        Vector3D origin = surface.BasePoint(root, u, v);
        Vector3D across = Along(surface.BasePoint(root, Math.Min(u + step, 1), v)
            - surface.BasePoint(root, Math.Max(u - step, 0), v));
        Vector3D down = surface.BasePoint(root, u, Math.Min(v + step, 1))
            - surface.BasePoint(root, u, Math.Max(v - step, 0));
        down = Along(down - across * down.Dot(across));

        double width = surface.RootWidth / (1L << level);
        double spacing = width / 2;
        double outer = reach + width;
        int steps = (int)Math.Ceiling(outer / spacing);
        long perRoot = 1L << level;
        for (int i = -steps; i <= steps; i++)
        {
            for (int j = -steps; j <= steps; j++)
            {
                Vector3D point = origin + across * (i * spacing) + down * (j * spacing);
                if (surface.Locate(point) is not var (r, pu, pv))
                {
                    continue;
                }

                Vector3D onSurface = surface.BasePoint(r, pu, pv);
                if (surface.Across(origin, onSurface) > outer)
                {
                    continue;
                }

                tiles.Add(new GroundTile(r, level,
                    (int)Math.Clamp(Math.Floor(pu * perRoot), 0, perRoot - 1),
                    (int)Math.Clamp(Math.Floor(pv * perRoot), 0, perRoot - 1)));
            }
        }

        return tiles;
    }

    /// <summary>
    /// About how much ground a tile covers, in square meters, on a surface whose unit is
    /// <paramref name="radiusMeters"/>: the lengths of two of its sides multiplied.
    /// </summary>
    public static double AreaSquareMeters(ITileSurface surface, GroundTile tile,
        double radiusMeters)
    {
        (double u0, double v0) = tile.OnRoot(0, 0);
        (double u1, double v1) = tile.OnRoot(1, 1);
        Vector3D corner = surface.BasePoint(tile.Root, u0, v0);
        double wide = surface.Across(corner, surface.BasePoint(tile.Root, u1, v0));
        double tall = surface.Across(corner, surface.BasePoint(tile.Root, u0, v1));
        return wide * tall * radiusMeters * radiusMeters;
    }

    private static Vector3D Along(Vector3D direction) => direction * (1 / direction.Length);
}
