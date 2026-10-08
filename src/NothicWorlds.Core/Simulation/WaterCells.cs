using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The cells of a body's height grid as a flat index (VISION.md BOD-11): each cell's place in
/// one array, and back, for the water's sums (lakes filling, rivers running downhill).
/// </summary>
public static class WaterCells
{
    /// <summary>Cells along a face's edge.</summary>
    public const int Size = HeightGrid.FaceSize;

    /// <summary>How many cells there are.</summary>
    public const int Count = HeightGrid.CellCount;

    /// <summary>The most cells around a cell.</summary>
    public const int MaxNeighbors = 8;

    /// <summary>A cell's index.</summary>
    public static int IndexOf(CubeCell cell) => (cell.Face * Size + cell.Row) * Size + cell.Column;

    /// <summary>The cell at an index.</summary>
    public static CubeCell CellOf(int index) =>
        new(index / (Size * Size), index % Size, index / Size % Size);

    /// <summary>The cell a direction points into.</summary>
    public static int IndexAt(Vector3D direction) =>
        IndexOf(CubeSphere.CellAt(direction, Size));

    /// <summary>The direction to a cell's middle.</summary>
    public static Vector3D Center(int index) => CubeSphere.CellCenter(CellOf(index), Size);

    /// <summary>The cells around a cell (see <see cref="CubeSphere.Neighbors"/>).</summary>
    public static IEnumerable<int> Neighbors(int index)
    {
        int[] around = new int[MaxNeighbors];
        int count = Neighbors(index, around);
        return around.Take(count);
    }

    /// <summary>
    /// Writes the cells around a cell into <paramref name="around"/> (room for
    /// <see cref="MaxNeighbors"/>), in the same order as <see cref="CubeSphere.Neighbors"/>,
    /// and returns how many there are. Doesn't allocate for cells away from a face's edge,
    /// which is nearly all of them: the water's sums ask for millions.
    /// </summary>
    public static int Neighbors(int index, Span<int> around)
    {
        int column = index % Size, row = index / Size % Size;
        if (column is 0 or Size - 1 || row is 0 or Size - 1)
        {
            int found = 0;
            foreach (CubeCell cell in CubeSphere.Neighbors(CellOf(index), Size))
            {
                around[found++] = IndexOf(cell);
            }

            return found;
        }

        int count = 0;
        for (int rows = -1; rows <= 1; rows++)
        {
            for (int columns = -1; columns <= 1; columns++)
            {
                if (rows != 0 || columns != 0)
                {
                    around[count++] = index + rows * Size + columns;
                }
            }
        }

        return count;
    }
}
