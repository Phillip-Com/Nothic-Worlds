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

        var inLake = new HashSet<int> { start };
        var queue = new Queue<int>();
        queue.Enqueue(start);
        int? spill = null;
        short spillHeight = short.MaxValue;
        while (queue.Count > 0)
        {
            int cell = queue.Dequeue();
            foreach (int next in WaterCells.Neighbors(cell))
            {
                if (inLake.Contains(next) || isSea(next))
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

                if (inLake.Count >= MaxCells)
                {
                    return new LakeShape(new HashSet<int>(), null,
                        "the lake would cover too much of the world: lower its surface");
                }

                inLake.Add(next);
                queue.Enqueue(next);
            }
        }

        return new LakeShape(inLake, spill, null);
    }
}

/// <summary>
/// Where a lake's water lies: its cells (see <see cref="WaterCells"/>), the lowest cell of its
/// shore (where a river flows out), or why it can't be filled.
/// </summary>
/// <param name="Cells">The cells under its water.</param>
/// <param name="Outflow">The lowest cell around it, or null if none (or it can't fill).</param>
/// <param name="Problem">Why the lake has no water, or null.</param>
public sealed record LakeShape(IReadOnlySet<int> Cells, int? Outflow, string? Problem);
