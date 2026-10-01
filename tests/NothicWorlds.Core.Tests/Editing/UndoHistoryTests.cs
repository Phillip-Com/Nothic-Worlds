using NothicWorlds.Core.Editing;

namespace NothicWorlds.Core.Tests.Editing;

public class UndoHistoryTests
{
    [Fact]
    public void Undo_ReturnsTheStateBeforeTheEdit_AndRedoReturnsTheStateAfter()
    {
        var history = new UndoHistory<string>();
        history.Record("Type B", "A");

        Assert.Equal("A", history.Undo("AB"));
        Assert.Equal("AB", history.Redo("A"));
    }

    [Fact]
    public void Descriptions_FollowTheSteps()
    {
        var history = new UndoHistory<int>();
        Assert.Null(history.UndoDescription);

        history.Record("First", 0);
        history.Record("Second", 1);
        Assert.Equal("Second", history.UndoDescription);

        history.Undo(2);
        Assert.Equal("First", history.UndoDescription);
        Assert.Equal("Second", history.RedoDescription);
    }

    [Fact]
    public void Undo_SeveralSteps_WalksBackInOrder()
    {
        var history = new UndoHistory<int>();
        history.Record("1", 0);
        history.Record("2", 1);
        history.Record("3", 2);

        Assert.Equal(2, history.Undo(3));
        Assert.Equal(1, history.Undo(2));
        Assert.Equal(0, history.Undo(1));
        Assert.False(history.CanUndo);
        Assert.Equal(1, history.Redo(0));
    }

    [Fact]
    public void NewEdit_ClearsRedo()
    {
        var history = new UndoHistory<int>();
        history.Record("1", 0);
        history.Undo(1);

        history.Record("Other", 0);

        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Capacity_DropsTheOldestSteps()
    {
        var history = new UndoHistory<int>(capacity: 2);
        history.Record("1", 0);
        history.Record("2", 1);
        history.Record("3", 2);

        history.Undo(3);
        history.Undo(2);

        Assert.False(history.CanUndo);
    }

    [Fact]
    public void RapidEditsWithTheSameKey_MergeIntoOneStep()
    {
        var clock = new ManualClock();
        var history = new UndoHistory<int>(clock: clock);
        history.Record("Color", 0, mergeKey: "fill");
        clock.Advance(TimeSpan.FromMilliseconds(500));
        history.Record("Color", 1, mergeKey: "fill");
        clock.Advance(TimeSpan.FromMilliseconds(500));
        history.Record("Color", 2, mergeKey: "fill");

        Assert.Equal(0, history.Undo(3));  // The state before the first of them
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void EditsWithTheSameKey_SeparatedByAPause_AreSeparateSteps()
    {
        var clock = new ManualClock();
        var history = new UndoHistory<int>(clock: clock);
        history.Record("Color", 0, mergeKey: "fill");
        clock.Advance(TimeSpan.FromSeconds(2));
        history.Record("Color", 1, mergeKey: "fill");

        Assert.Equal(1, history.Undo(2));
        Assert.True(history.CanUndo);
    }

    [Fact]
    public void EditsWithDifferentKeys_DoNotMerge()
    {
        var history = new UndoHistory<int>(clock: new ManualClock());
        history.Record("A", 0, mergeKey: "a");
        history.Record("B", 1, mergeKey: "b");

        Assert.Equal(1, history.Undo(2));
        Assert.True(history.CanUndo);
    }

    [Fact]
    public void AnEditAfterUndo_NeverMergesIntoAnOldStep()
    {
        var history = new UndoHistory<int>(clock: new ManualClock());
        history.Record("Color", 0, mergeKey: "fill");
        history.Record("Other", 1);
        history.Undo(2);

        history.Record("Color", 1, mergeKey: "fill");

        Assert.Equal(1, history.Undo(3));
        Assert.Equal(0, history.Undo(1));
    }

    [Fact]
    public void States_IncludesUndoAndRedoSnapshots()
    {
        var history = new UndoHistory<int>();
        history.Record("1", 10);
        history.Record("2", 20);
        history.Undo(30);

        Assert.Equal([10, 30], history.States.Order());
    }

    [Fact]
    public void Clear_ForgetsEverything()
    {
        var history = new UndoHistory<int>();
        history.Record("1", 0);
        history.Record("2", 1);
        history.Undo(2);

        history.Clear();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void UndoWithNothingToUndo_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new UndoHistory<int>().Undo(0));
        Assert.Throws<InvalidOperationException>(() => new UndoHistory<int>().Redo(0));
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
