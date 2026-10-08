using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Where a lake's water lies (VISION.md BOD-11; owner's choice: click and fill): every cell
/// lower than its surface that's joined to its spot through such cells, as water filling a
/// basin would. It stops at the sea (the world's own water), which a lake can't flow into or
/// rise over.
/// </summary>
public static class LakeFill
{
    /// <summary>The most cells one lake can cover (a sixth of a world).</summary>
    public const int MaxCells = HeightGrid.CellCount / 6;

    /// <summary>
    /// Fills a lake with its surface at <paramref name="levelMeters"/> from
    /// <paramref name="spot"/>, on ground of these heights (meters), where
    /// <paramref name="isSea"/> marks the world's own water.
    /// </summary>
    public static LakeShape Fill(HeightGrid ground, Vector3D spot, int levelMeters,
        Func<int, bool> isSea)
    {
        int start = WaterCells.IndexAt(spot);
        if (isSea(start))
        {
            return new LakeShape(new HashSet<int>(), null, "that spot is in the sea");
        }

        if (ground.HeightAt(WaterCells.CellOf(start)) >= levelMeters)
        {
            return new LakeShape(new HashSet<int>(), null,
                "the ground there is higher than the lake's surface: raise the surface or " +
                "choose lower ground");
        }

        WaterScratch scratch = WaterScratch.Borrow();
        try
        {
            return Fill(ground, start, levelMeters, isSea, scratch);
        }
        finally
        {
            WaterScratch.Return(scratch);
        }
    }

    private static LakeShape Fill(HeightGrid ground, int start, int levelMeters,
        Func<int, bool> isSea, WaterScratch scratch)
    {
        // The cells filled, in the order they were reached (and taken from in that order, as a
        // queue), and which are in the lake, by their stamp: a set was slow for a lake that
        // spreads over much of a world before it's refused.
        Span<int> around = stackalloc int[WaterCells.MaxNeighbors];
        int[] filled = scratch.Stamps;
        int stamp = scratch.NewStamp();
        var cells = new List<int> { start };
        filled[start] = stamp;
        int? spill = null;
        short spillHeight = short.MaxValue;
        for (int taken = 0; taken < cells.Count; taken++)
        {
            int cell = cells[taken];
            foreach (int next in around[..WaterCells.Neighbors(cell, around)])
            {
                if (filled[next] == stamp || isSea(next))
                {
                    continue;
                }

                short height = ground.HeightAt(WaterCells.CellOf(next));
                if (height >= levelMeters)
                {
                    // The shore: its lowest cell is where the water would spill out.
                    if (height < spillHeight)
                    {
                        (spill, spillHeight) = (next, height);
                    }

                    continue;
                }

                if (cells.Count >= MaxCells)
                {
                    return new LakeShape(new HashSet<int>(), null,
                        "the lake would cover too much of the world: lower its surface");
                }

                filled[next] = stamp;
                cells.Add(next);
            }
        }

        return new LakeShape(new HashSet<int>(cells), spill, null);
    }

    /// <summary>
    /// The hollow that water at <paramref name="spot"/> collects in, and how high it can rise
    /// there before it spills out: the water runs downhill from the spot to the bottom of the
    /// hollow, then rises until it reaches the hollow's lowest pass. A lake at the bottom with
    /// that surface fills the hollow to its brim. Null if the water runs into the sea, or the
    /// hollow is wider than <paramref name="maxCells"/> (as on level ground).
    /// </summary>
    public static LakeSeat? HollowBelow(HeightGrid ground, Vector3D spot, Func<int, bool> isSea,
        int maxCells)
    {
        double Height(int cell) =>
            isSea(cell) ? double.NegativeInfinity : ground.HeightAt(WaterCells.CellOf(cell));

        Span<int> around = stackalloc int[WaterCells.MaxNeighbors];
        int bottom = WaterCells.IndexAt(spot);
        for (int step = 0; step < maxCells && !isSea(bottom); step++)
        {
            int lowest = bottom;
            foreach (int next in around[..WaterCells.Neighbors(bottom, around)])
            {
                if (Height(next) < Height(lowest))
                {
                    lowest = next;
                }
            }

            if (lowest == bottom)
            {
                break;
            }

            bottom = lowest;
        }

        if (isSea(bottom))
        {
            return null;
        }

        // Lowest cells first, spreading out from the bottom: the water rises through them
        // until the next lowest is lower than it has already risen, past the pass.
        double floor = Height(bottom), highest = floor;
        var seen = new HashSet<int> { bottom };
        var open = new PriorityQueue<int, (double Height, int Order)>();
        int order = 0;
        open.Enqueue(bottom, (floor, order++));
        while (open.TryDequeue(out int cell, out (double Height, int Order) at))
        {
            if (at.Height < highest)
            {
                return highest > floor
                    ? new LakeSeat(WaterCells.Center(bottom), (int)highest)
                    : null;
            }

            highest = at.Height;
            if (seen.Count > maxCells)
            {
                return null;
            }

            foreach (int next in around[..WaterCells.Neighbors(cell, around)])
            {
                if (seen.Add(next))
                {
                    open.Enqueue(next, (Height(next), order++));
                }
            }
        }

        return null;
    }
}
