namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Working space over every cell for the water's searches (rivers finding their way down,
/// lakes filling; see <see cref="RiverCourse"/> and <see cref="LakeFill"/>): a search can reach
/// hundreds of thousands of cells, and sets and dictionaries of them took most of its time. A
/// cell counts as marked only if it carries the stamp a search took, so nothing needs clearing
/// between searches. One is kept and lent out; a search running while it's lent makes its own.
/// </summary>
internal sealed class WaterScratch
{
    private static WaterScratch? _spare;
    private int _stamp;

    /// <summary>Each cell's latest stamp.</summary>
    public int[] Stamps { get; } = new int[WaterCells.Count];

    /// <summary>Where a search reached each cell from.</summary>
    public int[] CameFrom { get; } = new int[WaterCells.Count];

    /// <summary>The cells waiting to be searched.</summary>
    public HeightQueue Queue { get; } = new();

    /// <summary>A stamp no cell carries yet.</summary>
    public int NewStamp() => ++_stamp;

    /// <summary>Borrows the kept working space, or makes one if it's lent out.</summary>
    public static WaterScratch Borrow() => Interlocked.Exchange(ref _spare, null) ?? new();

    /// <summary>Gives the working space back, to be kept for the next search.</summary>
    public static void Return(WaterScratch scratch) => _spare = scratch;
}
