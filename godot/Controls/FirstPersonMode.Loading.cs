using NothicWorlds.Core.Geometry;
using NothicWorlds.Rendering;
using NothicWorlds.UI;

namespace NothicWorlds.Controls;

/// <summary>
/// Loading the area while standing (VISION.md REN-06; owner's choice, 2026-10-12): on
/// standing somewhere (or travelling there on the minimap), a "Preparing the area…" screen
/// covers the view until the roads, the ground round the eye, the rivers, and the near plants
/// are in (<see cref="AreaLoading"/>); then a note in the corner says while the far ground
/// and trees still load.
/// </summary>
public partial class FirstPersonMode
{
    // Frames the view waits after the screen is shown, before the area starts loading: the
    // ground's materials and the plants' models load on the first, holding it up, and the
    // screen should already be up by then.
    private const int CoverFrames = 2;

    private readonly AreaLoadingScreen _loadingScreen = new();
    private AreaLoading _loading = new();
    private int _coverFramesLeft;

    // How many times the banks and the near plants had begun to be worked out when the ground
    // round the eye came in (null before): those begun after it are for that ground. As the
    // far ground joins, the ground drawn keeps changing, and waiting for banks and plants to
    // match it every time would wait for all of it.
    private (int Banks, int Plants)? _startsAtGround;

    // Puts up the screen, with the area to load from the start.
    private void BeginLoading()
    {
        _loading = new AreaLoading();
        _startsAtGround = null;
        _loadingScreen.Begin();
        _hud?.SetLoadingNote(false);
        _coverFramesLeft = CoverFrames;
    }

    // Takes down the screen and the note (on leaving).
    private void EndLoading()
    {
        _loadingScreen.End();
        _hud?.SetLoadingNote(false);
    }

    // Tells the screen (or, once it's gone, the note) how the area stands, `delta` seconds
    // after last time.
    private void KeepLoading(double delta)
    {
        GroundTiles? tiles = _tiles;
        bool shown = tiles is { Visible: true };
        bool nearGround = !shown || tiles!.NearGroundShown;
        if (nearGround && _startsAtGround is null)
        {
            _startsAtGround = (_banksStarts, _plants?.NearStarts ?? 0);
        }

        (int Banks, int Plants) since = _startsAtGround ?? (int.MaxValue, int.MaxValue);
        bool nearPlants = !PlantsWanted(tiles) || _plantModelsFailed
            || _plants is { NearPlaced: true }
            || (_plants is { } plants && plants.NearShown > since.Plants);
        bool rivers = !shown || (_pendingChannels is null && !_banksStale
            && ((_pendingBanks is null && !BanksWaiting(tiles!)) || _banksShown > since.Banks));
        bool busy = _pendingRoads is not null || _pendingChannels is not null
            || _pendingBanks is not null || (shown && tiles!.IsBusy)
            || (PlantsWanted(tiles) && _plants is { IsBusy: true });
        _loading.Report(new AreaLoadingReport(
            RoadsLaid: _pendingRoads is null && (_roadProfiles is not null || !shown),
            NearGroundShown: nearGround,
            NearTilesLeft: shown ? tiles!.NearTilesLeft : 0,
            RiversShown: rivers,
            PlantsPlaced: nearPlants,
            Busy: busy), delta);
        _loadingScreen.Show(_loading);
        _hud?.SetLoadingNote(_loading.ShowsNote);
    }

    // Whether the rivers' beds and banks or the roads' strips are due to be built for the
    // channels or the ground shown now (BanksDue), but haven't been yet.
    private bool BanksWaiting(GroundTiles tiles)
    {
        bool strips = _channels is { Stretches.Count: > 0 } || _carving?.Roads is not null
            || Strands is not null;
        return _banksStale || (strips && tiles.ShownVersion != _banksShownVersion);
    }
}
