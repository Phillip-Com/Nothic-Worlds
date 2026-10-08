using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

// RiverCourse.Trace as first written, plainly (sets, a dictionary, a priority queue), for
// checking the faster one against: the documented rules (docs/world-format.md) fix every
// river's course, so a faster search must find exactly the same one.
internal static class SimpleRiverCourse
{
    public static List<int> Trace(HeightGrid ground, Vector3D source, Func<int, bool> isWater,
        Func<int, bool>? isBlocked = null)
    {
        isBlocked ??= _ => false;
        int current = WaterCells.IndexAt(source);
        var course = new List<int> { current };
        var visited = new HashSet<int> { current };
        double Height(int cell) => ground.HeightAt(WaterCells.CellOf(cell));

        while (!isWater(current) && course.Count < RiverCourse.MaxCells)
        {
            int lowest = -1;
            double lowestHeight = Height(current);
            foreach (int next in WaterCells.Neighbors(current))
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
                return course;
            }

            foreach (int step in steps)
            {
                course.Add(step);
                visited.Add(step);
            }

            current = course[^1];
        }

        return course;
    }

    private static List<int>? WayOut(int hollow, Func<int, double> height,
        Func<int, bool> isWater, Func<int, bool> isBlocked, HashSet<int> used)
    {
        double bottom = height(hollow);
        var cameFrom = new Dictionary<int, int> { [hollow] = hollow };
        var open = new PriorityQueue<int, (double Highest, int Order)>();
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

            if (cameFrom.Count > RiverCourse.MaxSearch)
            {
                return null;
            }

            foreach (int next in WaterCells.Neighbors(cell))
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
}
