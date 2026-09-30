namespace NothicWorlds.Core.Editing;

/// <summary>
/// Undo and redo by snapshots: before each edit, the caller records a copy of the state it's
/// about to change, with a description for the Edit menu (e.g. "Move Piece 1"). Undo hands that
/// copy back and keeps the current state for redo.
/// </summary>
/// <remarks>
/// Rapid edits of the same thing (dragging a color picker, holding a number field's arrow) can
/// share a merge key, so they become one step: an edit merges into the previous one if it has
/// the same key and comes within <see cref="MergeWindow"/> of it. Snapshots must not change after
/// they're recorded; pass copies.
/// </remarks>
/// <typeparam name="TState">The snapshot type.</typeparam>
public sealed class UndoHistory<TState>
{
    /// <summary>How many steps are kept by default; older ones are dropped.</summary>
    public const int DefaultCapacity = 100;

    /// <summary>How close together edits with the same merge key must be to merge.</summary>
    public static readonly TimeSpan MergeWindow = TimeSpan.FromSeconds(1);

    private readonly List<Step> _undo = [];
    private readonly List<Step> _redo = [];
    private readonly int _capacity;
    private readonly TimeProvider _clock;

    // The last recorded step and when it was last extended, while merging into it is allowed.
    private (Step Step, DateTimeOffset At)? _mergeable;

    /// <param name="capacity">The most steps to keep (at least 1).</param>
    /// <param name="clock">Time source for merging; the system clock by default.</param>
    public UndoHistory(int capacity = DefaultCapacity, TimeProvider? clock = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>True if there's a step to undo.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>True if there's a step to redo.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>What Undo would undo (e.g. "Move Piece 1"), or null.</summary>
    public string? UndoDescription => CanUndo ? _undo[^1].Description : null;

    /// <summary>What Redo would redo, or null.</summary>
    public string? RedoDescription => CanRedo ? _redo[^1].Description : null;

    /// <summary>Every snapshot held, for undo and redo (e.g. to keep what they refer to).</summary>
    public IEnumerable<TState> States => _undo.Concat(_redo).Select(step => step.State);

    /// <summary>
    /// Records the state from before an edit. Clears the redo steps, since they no longer
    /// follow from the current state.
    /// </summary>
    /// <param name="description">What the edit does, e.g. "Move Piece 1".</param>
    /// <param name="before">A copy of the state before the edit.</param>
    /// <param name="mergeKey">
    /// Identifies a stream of rapid edits of one thing (e.g. "fill color"), or null to never
    /// merge.
    /// </param>
    public void Record(string description, TState before, object? mergeKey = null)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        _redo.Clear();

        if (mergeKey is not null && _mergeable is (Step last, DateTimeOffset at)
            && Equals(last.MergeKey, mergeKey) && now - at <= MergeWindow)
        {
            // Same stream of edits: keep the earlier "before" so it all undoes in one step.
            _mergeable = (last, now);
            return;
        }

        var step = new Step(description, before, mergeKey);
        _undo.Add(step);
        if (_undo.Count > _capacity)
        {
            _undo.RemoveAt(0);
        }

        _mergeable = mergeKey is null ? null : (step, now);
    }

    /// <summary>
    /// Takes back the last step: returns the state to restore, and keeps
    /// <paramref name="current"/> for redo.
    /// </summary>
    /// <exception cref="InvalidOperationException">There's nothing to undo.</exception>
    public TState Undo(TState current)
    {
        return Move(_undo, _redo, current, "undo");
    }

    /// <summary>
    /// Re-applies the last undone step: returns the state to restore, and keeps
    /// <paramref name="current"/> for undo.
    /// </summary>
    /// <exception cref="InvalidOperationException">There's nothing to redo.</exception>
    public TState Redo(TState current)
    {
        return Move(_redo, _undo, current, "redo");
    }

    /// <summary>Forgets every step (e.g. when another world is opened).</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _mergeable = null;
    }

    private TState Move(List<Step> from, List<Step> to, TState current, string action)
    {
        if (from.Count == 0)
        {
            throw new InvalidOperationException($"There's nothing to {action}.");
        }

        Step step = from[^1];
        from.RemoveAt(from.Count - 1);
        to.Add(step with { State = current, MergeKey = null });
        _mergeable = null;  // An edit after undo/redo always starts a new step.
        return step.State;
    }

    private sealed record Step(string Description, TState State, object? MergeKey);
}
