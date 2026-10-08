namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Cells waiting to be searched, lowest first and ties in the order they were added (see
/// <see cref="RiverCourse"/>): a first-in-first-out list for each whole meter of height
/// (heights are whole meters, and in a search they only ever rise), so taking the next is as
/// quick as adding one.
/// </summary>
internal sealed class HeightQueue
{
    private const int Levels = 1 << 16;
    private readonly List<int>?[] _cells = new List<int>?[Levels];
    private readonly int[] _taken = new int[Levels];
    private readonly List<int> _used = [];
    private int _lowest = Levels;

    public void Enqueue(int cell, int height)
    {
        int level = height - short.MinValue;
        List<int> cells = _cells[level] ??= [];
        if (cells.Count == 0)
        {
            _used.Add(level);
        }

        cells.Add(cell);
        _lowest = Math.Min(_lowest, level);
    }

    public bool TryDequeue(out int cell, out int height)
    {
        while (_lowest < Levels
            && (_cells[_lowest] is not List<int> cells || _taken[_lowest] >= cells.Count))
        {
            _lowest++;
        }

        if (_lowest == Levels)
        {
            (cell, height) = (0, 0);
            return false;
        }

        cell = _cells[_lowest]![_taken[_lowest]++];
        height = _lowest + short.MinValue;
        return true;
    }

    public void Clear()
    {
        foreach (int level in _used)
        {
            _cells[level]!.Clear();
            _taken[level] = 0;
        }

        _used.Clear();
        _lowest = Levels;
    }
}
