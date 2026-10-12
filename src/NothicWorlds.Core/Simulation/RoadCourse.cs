using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The way a routed road takes between two places (VISION.md BOD-13; owner's choice: the
/// easiest way for its style): the cheapest chain of cells by <see cref="RoadCosts"/>, found
/// by an A* search (it spreads out from the start cheapest-first, steered toward the end). A
/// stretch too long to search that way (hundreds of km over rough ground) is searched again
/// steered harder toward its end, which is quick but may miss a cheaper way that strays
/// farther. The same ground always gives the same way.
/// </summary>
public static class RoadCourse
{
    /// <summary>
    /// The most cells searched for the cheapest way (about a second's search), before searching
    /// again steered harder.
    /// </summary>
    public const int MaxCheapestSearch = 250_000;

    /// <summary>
    /// The most cells searched steered harder: more than enough to cross an Earth-sized world.
    /// </summary>
    public const int MaxSearch = 3_000_000;

    // How much harder the second search is steered: as if the ground ahead were all a bit
    // worse than level, bare, and unworn (from tries over rough ground: less spreads out over
    // millions of cells, crossing a world; this crosses it in thousands).
    private const double HardSteering = 1.15;

    /// <summary>
    /// The cells' middles from the cell of <paramref name="from"/> to that of
    /// <paramref name="to"/>, both included, or null if there's no way (one of them is in
    /// water, water cuts them off, or the search ran too long).
    /// </summary>
    public static List<Vector3D>? Route(Vector3D from, Vector3D to, RoadCosts costs)
    {
        WaterScratch scratch = WaterScratch.Borrow();
        try
        {
            int start = WaterCells.IndexAt(from), goal = WaterCells.IndexAt(to);
            return Route(start, goal, costs, costs.LeastPerRadian, MaxCheapestSearch, scratch)
                ?? Route(start, goal, costs, HardSteering, MaxSearch, scratch);
        }
        finally
        {
            WaterScratch.Return(scratch);
        }
    }

    // An A* search, counting on `steering` per radian still to go, giving up after `limit`
    // cells.
    private static List<Vector3D>? Route(int start, int goal, RoadCosts costs, double steering,
        int limit, WaterScratch scratch)
    {
        if (!costs.IsOpen(start) || !costs.IsOpen(goal))
        {
            return null;
        }

        // A cell carrying `opened` has a cost so far; one carrying `closed` is settled.
        int opened = scratch.NewStamp();
        int closed = scratch.NewStamp();
        int[] stamps = scratch.Stamps;
        int[] cameFrom = scratch.CameFrom;
        float[] spent = scratch.Costs;
        Vector3D goalCenter = WaterCells.Center(goal);
        var open = new PriorityQueue<int, double>();
        stamps[start] = opened;
        spent[start] = 0;
        cameFrom[start] = start;
        open.Enqueue(start, 0);
        Span<int> around = stackalloc int[WaterCells.MaxNeighbors];
        int searched = 0;
        while (open.TryDequeue(out int cell, out _))
        {
            if (stamps[cell] == closed)
            {
                continue;  // Already settled by a cheaper way.
            }

            stamps[cell] = closed;
            if (cell == goal)
            {
                return PathTo(goal, cameFrom);
            }

            if (++searched > limit)
            {
                return null;
            }

            Vector3D center = WaterCells.Center(cell);
            foreach (int next in around[..WaterCells.Neighbors(cell, around)])
            {
                if (stamps[next] == closed || !costs.IsOpen(next))
                {
                    continue;
                }

                Vector3D nextCenter = WaterCells.Center(next);
                double cost = spent[cell] + costs.Step(cell, next, (nextCenter - center).Length);
                if (stamps[next] == opened && spent[next] <= cost)
                {
                    continue;
                }

                stamps[next] = opened;
                spent[next] = (float)cost;
                cameFrom[next] = cell;
                open.Enqueue(next, cost + steering * Arc(goalCenter, nextCenter));
            }
        }

        return null;
    }

    // The angle between two unit directions, in radians (from the chord, which is precise
    // for short and long arcs alike, unlike the dot product's arccosine for short ones).
    private static double Arc(Vector3D a, Vector3D b) =>
        2 * Math.Asin(Math.Min(1, (a - b).Length / 2));

    private static List<Vector3D> PathTo(int goal, int[] cameFrom)
    {
        var cells = new List<Vector3D>();
        for (int cell = goal; ; cell = cameFrom[cell])
        {
            cells.Add(WaterCells.Center(cell));
            if (cameFrom[cell] == cell)
            {
                break;
            }
        }

        cells.Reverse();
        return cells;
    }
}
