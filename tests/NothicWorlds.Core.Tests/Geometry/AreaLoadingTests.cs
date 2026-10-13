using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Tests.Geometry;

public sealed class AreaLoadingTests
{
    private static readonly AreaLoadingReport _allIn = new(true, true, 0, true, true, false);

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

        loading.Report(_allIn with { NearGroundShown = false, NearTilesLeft = 5 }, 0.1);
        Assert.Equal(AreaLoadingStage.Ground, loading.Stage);
        Assert.Equal(5, loading.TilesLeft);
        loading.Report(_allIn with { RiversShown = false }, 0.1);
        Assert.Equal(AreaLoadingStage.Rivers, loading.Stage);
        loading.Report(_allIn with { PlantsPlaced = false }, 0.1);
        Assert.Equal(AreaLoadingStage.Plants, loading.Stage);
        loading.Report(_allIn, 0.1);
        Assert.True(loading.IsReady);
        Assert.Equal(1, loading.Done);
    }

    [Fact]
    public void StageDueAgain_TheStageSaidDoesNotGoBack()
    {
        // As far tiles join, the near ground is briefly being drawn again.
        var loading = new AreaLoading();
        loading.Report(_allIn with { RiversShown = false }, 0.1);

        loading.Report(_allIn with { NearGroundShown = false, RiversShown = false }, 0.1);

        Assert.Equal(AreaLoadingStage.Rivers, loading.Stage);
        Assert.False(loading.IsReady);
    }

    [Fact]
    public void LaterStagesInFirst_NotReadyUntilAllAreIn()
    {
        var loading = new AreaLoading();
        loading.Report(_allIn with { NearGroundShown = false }, 0.1);
        loading.Report(_allIn with { PlantsPlaced = false }, 0.1);

        loading.Report(_allIn with { NearGroundShown = false }, 0.1);

        Assert.Equal(AreaLoadingStage.Plants, loading.Stage);
        Assert.False(loading.IsReady);
        Assert.True(loading.Done < 1);
    }

    [Fact]
    public void MoreTilesWanted_TheBarNeverGoesBack()
    {
        // Building coarse tiles shows finer ones are wanted: the count left rises.
        var loading = new AreaLoading();
        AreaLoadingReport building = _allIn with { NearGroundShown = false, Busy = true };
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
        loading.Report(_allIn, 0.1);

        loading.Report(new AreaLoadingReport(false, false, 50, false, false, true), 0.1);

        Assert.True(loading.IsReady);
        Assert.Equal(1, loading.Done);
    }

    [Fact]
    public void Ready_TheNoteShowsOnlyOnceLoadingHasGoneOnAWhile()
    {
        var loading = new AreaLoading();
        AreaLoadingReport busy = _allIn with { Busy = true };
        loading.Report(busy, 0.1);  // Ready, but the far ground is still loading

        loading.Report(busy, AreaLoading.NoteSeconds / 2);
        Assert.False(loading.ShowsNote);
        loading.Report(busy, AreaLoading.NoteSeconds / 2);
        Assert.True(loading.ShowsNote);

        // A moment's quiet (walking, between tiles) doesn't hide it.
        loading.Report(_allIn, AreaLoading.NoteSeconds / 2);
        loading.Report(busy, 0.1);
        loading.Report(_allIn, AreaLoading.NoteSeconds / 2);
        Assert.True(loading.ShowsNote);
        loading.Report(_allIn, AreaLoading.NoteSeconds / 2);
        Assert.False(loading.ShowsNote);
    }

    [Fact]
    public void NotReady_NoNote()
    {
        var loading = new AreaLoading();

        for (int i = 0; i < 10; i++)
        {
            loading.Report(_allIn with { PlantsPlaced = false, Busy = true }, 1);
        }

        Assert.False(loading.ShowsNote);
    }
}
