using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The ways all the roads on one body take (VISION.md BOD-13): drawn roads as clicked, and
/// routed roads by the easiest way between their points for their style, around the sea and
/// lakes and across rivers. Roads are worked out in the order they were added, each favoring
/// the ways of those before it, so paths join up into a network. Worked out again whenever the
/// ground, the water, the terrain, or the roads change.
/// </summary>
public static class BodyRoads
{
    /// <summary>
    /// The ways of the roads on <paramref name="body"/> (others are left out), over ground of
    /// these heights (meters) with its <paramref name="water"/> (null when it has no rivers or
    /// lakes: then only its sea, if any, is in the way) and the world's terrain types.
    /// </summary>
    public static IReadOnlyList<RoadCourseShown> For(Body body, HeightGrid ground,
        BodyWater? water, IReadOnlyList<Road> roads, IReadOnlyList<TerrainType> types)
    {
        var onBody = roads.Where(r => r.BodyId == body.Id).ToList();
        if (onBody.Count == 0)
        {
            return [];
        }

        Func<int, bool> isWater = water is not null ? water.IsWater
            : BodyWater.SeaRule(body, ground, types) ?? (_ => false);
        var plantsByCode = types.GroupBy(t => t.Code)
            .ToDictionary(g => g.Key, g => g.First().Plants);
        TerrainGrid terrain = body.Surface.Terrain;
        PlantCover PlantsAt(int cell) =>
            plantsByCode.GetValueOrDefault(terrain.CodeAt(WaterCells.CellOf(cell)));
        Dictionary<int, double> riverWidthKm = RiverCells(water);
        var worn = new bool[WaterCells.Count];
        var shown = new List<RoadCourseShown>(onBody.Count);
        foreach (Road road in onBody)
        {
            List<Vector3D> points = [.. road.Points.Select(SphericalPolygon.ToUnit)];
            (List<Vector3D> path, string? problem) = road.Kind == RoadKind.Drawn
                ? (RiverLine.Densified(points), null)
                : Routed(points, new RoadCosts(ground, body.RadiusKm, isWater, PlantsAt,
                    riverWidthKm, worn, road.Style));
            Mark(path, (cell, _) => worn[cell] = true);
            shown.Add(new RoadCourseShown(road.Id, road.Style, road.Kind, road.WidthMeters,
                path, problem));
        }

        return shown;
    }

    // A routed road's way through its points, stretch by stretch: each found and smoothed,
    // or drawn straight if it can't be found.
    private static (List<Vector3D> Path, string? Problem) Routed(List<Vector3D> points,
        RoadCosts costs)
    {
        var path = new List<Vector3D> { points[0] };
        string? problem = null;
        for (int i = 1; i < points.Count; i++)
        {
            List<Vector3D> stretch;
            if (RoadCourse.Route(points[i - 1], points[i], costs) is List<Vector3D> cells)
            {
                // The clicked places themselves, not their cells' middles, at the ends.
                cells[0] = points[i - 1];
                cells[^1] = points[i];
                stretch = RiverLine.Smoothed(RiverLine.Smoothed(cells));
            }
            else
            {
                stretch = RiverLine.Densified([points[i - 1], points[i]]);
                problem ??= $"no way could be found from point {i} to point {i + 1} " +
                    "(drawn straight): water is in the way, or it's too far";
            }

            path.AddRange(stretch.Skip(1));
        }

        return (path, problem);
    }

    // The cells the rivers run through, each with the widest river's width there.
    private static Dictionary<int, double> RiverCells(BodyWater? water)
    {
        var widths = new Dictionary<int, double>();
        foreach (RiverCourseShown river in water?.Rivers ?? [])
        {
            List<Vector3D> path = RiverLine.PathOf(river);
            Mark(path, (cell, share) =>
            {
                double widthKm = river.WidthKm * (0.2 + 0.8 * share);
                widths[cell] = Math.Max(widths.GetValueOrDefault(cell), widthKm);
            });
        }

        return widths;
    }

    // Calls `mark` for each cell along a path (with how far along the path it is, 0 to 1),
    // stepping a quarter of a cell at a time so no cell it crosses is missed.
    private static void Mark(List<Vector3D> path, Action<int, double> mark)
    {
        double step = Math.PI / 2 / WaterCells.Size / 4;
        for (int i = 0; i < path.Count; i++)
        {
            double share = path.Count > 1 ? (double)i / (path.Count - 1) : 0;
            mark(WaterCells.IndexAt(path[i]), share);
            if (i == 0)
            {
                continue;
            }

            Vector3D from = path[i - 1], to = path[i];
            int steps = (int)Math.Ceiling((to - from).Length / step);
            for (int s = 1; s < steps; s++)
            {
                Vector3D between = from + (to - from) * ((double)s / steps);
                mark(WaterCells.IndexAt(between * (1 / between.Length)), share);
            }
        }
    }
}
