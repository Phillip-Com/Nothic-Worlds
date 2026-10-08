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

    /// <summary>The cells around a cell.</summary>
    public static IEnumerable<int> Neighbors(int index) =>
        CubeSphere.Neighbors(CellOf(index), Size).Select(IndexOf);
}
