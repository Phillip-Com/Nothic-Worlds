using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Tests.Geometry;

public sealed class AreaLoadingTests
{
    private static readonly AreaLoadingReport AllIn = new(true, true, 0, true, true, false);

    [Fact]
    public void Start_WaitsOnTheRoadsWithNothingDone()
    {
        var loading = new AreaLoading();

        loading.Report(new AreaLoadingReport(false, false, 40, false, false, true), 0.1);

        Assert.Equal(AreaLoadingStage.Roads, loading.Stage);
        Assert.Equal(0, loading.Done);
        Assert.False(loading.IsReady);
    }

    [Fact]
    public void Stages_ComeInOrder()
    {
        var loading = new AreaLoading();

        loading.Report(AllIn with { NearGroundShown = false, NearTilesLeft = 5 }, 0.1);
        Assert.Equal(AreaLoadingStage.Ground, loading.Stage);
        Assert.Equal(5, loading.TilesLeft);
        loading.Report(AllIn with { RiversShown = false }, 0.1);
        Assert.Equal(AreaLoadingStage.Rivers, loading.Stage);
        loading.Report(AllIn with { PlantsPlaced = false }, 0.1);
        Assert.Equal(AreaLoadingStage.Plants, loading.Stage);
        loading.Report(AllIn, 0.1);
        Assert.True(loading.IsReady);
        Assert.Equal(1, loading.Done);
    }

    [Fact]
    public void MoreTilesWanted_TheBarNeverGoesBack()
    {
        // Building coarse tiles shows finer ones are wanted: the count left rises.
        var loading = new AreaLoading();
        AreaLoadingReport building = AllIn with { NearGroundShown = false, Busy = true };
        double last = 0;
        foreach (int left in new[] { 10, 4, 30, 25, 12, 40, 3, 0 })
        {
            loading.Report(building with { NearTilesLeft = left }, 0.1);
            Assert.True(loading.Done >= last, $"{loading.Done} after {last} at {left} left");
            Assert.True(loading.Done < 1);
            last = loading.Done;
        }
    }

    [Fact]
    public void Ready_StaysReadyWhenMoreLoads()
    {
        var loading = new AreaLoading();
        loading.Report(AllIn, 0.1);

        loading.Report(new AreaLoadingReport(false, false, 50, false, false, true), 0.1);

        Assert.True(loading.IsReady);
        Assert.Equal(1, loading.Done);
    }

    [Fact]
    public void Ready_TheNoteShowsOnlyOnceLoadingHasGoneOnAWhile()
    {
        var loading = new AreaLoading();
        AreaLoadingReport busy = AllIn with { Busy = true };
        loading.Report(busy, 0.1);  // Ready, but the far ground is still loading

        loading.Report(busy, AreaLoading.NoteSeconds / 2);
        Assert.False(loading.ShowsNote);
        loading.Report(busy, AreaLoading.NoteSeconds / 2);
        Assert.True(loading.ShowsNote);

        // A moment's quiet (walking, between tiles) doesn't hide it.
        loading.Report(AllIn, AreaLoading.NoteSeconds / 2);
        loading.Report(busy, 0.1);
        loading.Report(AllIn, AreaLoading.NoteSeconds / 2);
        Assert.True(loading.ShowsNote);
        loading.Report(AllIn, AreaLoading.NoteSeconds / 2);
        Assert.False(loading.ShowsNote);
    }

    [Fact]
    public void NotReady_NoNote()
    {
        var loading = new AreaLoading();

        for (int i = 0; i < 10; i++)
        {
            loading.Report(AllIn with { PlantsPlaced = false, Busy = true }, 1);
        }

        Assert.False(loading.ShowsNote);
    }
}
