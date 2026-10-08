using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The course of a natural river (VISION.md BOD-11; owner's choice: the path of least
/// resistance until it reaches water): from its source it steps to the lowest of the cells
/// around it while that's downhill. Caught in a hollow, it finds the way out that climbs
/// least (the lowest pass, spreading out from the hollow lowest-first) and carries on beyond
/// it, until it reaches water. The same ground always gives the same course.
/// </summary>
public static class RiverCourse
{
    /// <summary>The longest course worked out, in cells (about 200,000 km on Earth).</summary>
    public const int MaxCells = 20_000;

    /// <summary>The most cells searched for a way out of one hollow.</summary>
    public const int MaxSearch = 300_000;

    /// <summary>
    /// The course from <paramref name="source"/> over ground of these heights (meters), as
    /// the cells' middles from the source to the first water cell (included), where
    /// <paramref name="isWater"/> marks the water it ends in and <paramref name="isBlocked"/>
    /// cells it can't cross (the lake it flows out of). If it can't reach water (the course is
    /// too long, or a hollow too wide to search), it ends where it got to.
    /// </summary>
    public static RiverPath Trace(HeightGrid ground, Vector3D source, Func<int, bool> isWater,
        Func<int, bool>? isBlocked = null)
    {
        isBlocked ??= _ => false;
        int current = WaterCells.IndexAt(source);
        var course = new List<int> { current };
        var visited = new HashSet<int> { current };
        Span<int> around = stackalloc int[WaterCells.MaxNeighbors];
        double Height(int cell) => ground.HeightAt(WaterCells.CellOf(cell));

        while (!isWater(current) && course.Count < MaxCells)
        {
            int lowest = -1;
            double lowestHeight = Height(current);
            foreach (int next in around[..WaterCells.Neighbors(current, around)])
            {
                if (isBlocked(next) || visited.Contains(next))
                {
                    continue;
                }

                double height = isWater(next) ? double.NegativeInfinity : Height(next);
                if (height < lowestHeight)
                {
                    (lowest, lowestHeight) = (next, height);
                }
            }

            List<int>? steps = lowest >= 0
                ? [lowest]
                : WayOut(current, Height, isWater, isBlocked, visited);
            if (steps is null)
            {
                return new RiverPath(Directions(course), ReachesWater: false);
            }

            foreach (int step in steps)
            {
                course.Add(step);
                visited.Add(step);
            }

            current = course[^1];
        }

        return new RiverPath(Directions(course), ReachesWater: isWater(current));
    }

    // From a hollow, the way out that climbs least: cells are taken lowest-first (by the
    // highest point on the way to them) until one is lower than the hollow, or water; then
    // the path to it. Null if none is found within the search's reach.
    private static List<int>? WayOut(int hollow, Func<int, double> height,
        Func<int, bool> isWater, Func<int, bool> isBlocked, HashSet<int> used)
    {
        double bottom = height(hollow);
        var cameFrom = new Dictionary<int, int> { [hollow] = hollow };
        var open = new PriorityQueue<int, (double Highest, int Order)>();
        Span<int> around = stackalloc int[WaterCells.MaxNeighbors];
        int order = 0;
        open.Enqueue(hollow, (bottom, order++));
        while (open.TryDequeue(out int cell, out (double Highest, int Order) cost))
        {
            if (cell != hollow && (isWater(cell) || height(cell) < bottom))
            {
                var path = new List<int>();
                for (int step = cell; step != hollow; step = cameFrom[step])
                {
                    path.Add(step);
                }

                path.Reverse();
                return path;
            }

            if (cameFrom.Count > MaxSearch)
            {
                return null;
            }

            foreach (int next in around[..WaterCells.Neighbors(cell, around)])
            {
                if (cameFrom.ContainsKey(next) || isBlocked(next) || used.Contains(next))
                {
                    continue;
                }

                cameFrom[next] = cell;
                double highest = isWater(next) ? cost.Highest : Math.Max(cost.Highest,
                    height(next));
                open.Enqueue(next, (highest, order++));
            }
        }

        return null;
    }

    private static List<Vector3D> Directions(List<int> cells) =>
        [.. cells.Select(WaterCells.Center)];
}
