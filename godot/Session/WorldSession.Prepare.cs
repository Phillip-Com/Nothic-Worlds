using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Rendering;

namespace NothicWorlds.Session;

// Preparing bodies' ground and globes in the background (VISION.md REN-03; owner's choices:
// prepare in the background, and only bodies that are wanted). Working out the ground a body's
// painted terrain shapes, and making its globe's first images, took seconds on the main thread
// for a large world, freezing the app while it opened. Now a body is prepared only once it's
// selected, stood on, or big enough on screen to show it, and the work runs on worker threads;
// until then it's drawn plain. Small edits (a brush stroke) are still shown at once.
public partial class WorldSession
{
    // A painting change touching at most this many tiles (of 1,536) is reshaped at once; more
    // (undoing a large edit, deleting a widely painted type) is done in the background.
    private const int AtOnceTiles = 96;

    // Bodies waiting to be prepared until they're wanted, the one being prepared now, and those
    // wanted but waiting their turn: one at a time, so the selected body (taken first) isn't
    // slowed by others sharing the processor.
    private readonly HashSet<Guid> _detailWaiting = [];
    private readonly HashSet<Guid> _preparing = [];
    private readonly List<Guid> _preparingNext = [];

    /// <summary>
    /// Raised when a body starts (true) or finishes (false) being prepared in the background:
    /// its ground worked out and its globe's images made.
    /// </summary>
    public event Action<Guid, bool>? Preparing;

    /// <summary>Whether a body is being prepared in the background now.</summary>
    public bool IsPreparing(Guid bodyId) =>
        _preparing.Contains(bodyId) || _preparingNext.Contains(bodyId);

    /// <summary>
    /// Whether any body's ground, globe, or water is being worked out in the background now.
    /// </summary>
    public bool IsPreparingAnything =>
        _preparing.Count > 0 || _preparingNext.Count > 0 || _waterUnderway.Count > 0;

    // Shows bodies when they're wanted: selected, or grown big enough on screen.
    private void WatchForWantedBodies()
    {
        if (System is not null)
        {
            System.DetailWanted += ShowIfWaiting;
        }

        SelectionChanged += () => ShowIfWaiting(SelectedBodyId);
    }

    private void ShowIfWaiting(Guid bodyId)
    {
        if (_detailWaiting.Contains(bodyId) && FindBody(bodyId) is Body body)
        {
            ShowTerrain(body);
        }
    }

    // Whether a body's ground and globe are worth preparing now.
    private bool IsWanted(Body body, PlanetSurface surface) =>
        body.Id == SelectedBodyId || System?.StandingOn == body.Id || surface.WantsDetail;

    // Works out a body's ground and its globe's first images in the background, then shows
    // them. One at a time per body: an edit made meanwhile is caught up with when it's done.
    private async void StartPreparing(Body body, PlanetSurface surface)
    {
        Guid bodyId = body.Id;
        if (_preparing.Contains(bodyId))
        {
            return;
        }

        if (_preparing.Count > 0)
        {
            if (!_preparingNext.Contains(bodyId))
            {
                _preparingNext.Add(bodyId);
                Preparing?.Invoke(bodyId, true);
            }

            return;
        }

        _preparing.Add(bodyId);
        _preparingNext.Remove(bodyId);

        World world = World;
        TerrainGrid terrain = body.Surface.Terrain;
        HeightGrid sculpted = body.Surface.Heights;
        TerrainType[] types = [.. World.TerrainTypes];
        double radiusKm = body.RadiusKm;
        int seed = TerrainRelief.SeedFor(bodyId);
        bool shaping = World.TerrainShapesGround;
        (TerrainGrid Terrain, TerrainType[] Types, double RadiusKm, HeightGrid Ground)? known =
            _terrainGround.TryGetValue(bodyId, out var had) ? had : null;
        TerrainGrid? terrainBefore = surface.HasTerrainImages ? surface.ShownTerrain : null;
        HeightGrid? heightsBefore = surface.HasHeightImages ? surface.ShownHeights : null;
        byte[] palette = surface.TerrainPaletteBytes;
        Preparing?.Invoke(bodyId, true);
        try
        {
            (HeightGrid ground, HeightGrid shown, PreparedSurface images) =
                await Task.Run(() =>
                {
                    HeightGrid ground = shaping
                        ? WorkOutGround(known, terrain, types, radiusKm, seed)
                        : HeightGrid.Empty;
                    HeightGrid shown = TerrainRelief.Shaped(ground, sculpted);
                    return (ground, shown, SurfaceImages.Prepare(terrain, terrainBefore,
                        palette, shown, heightsBefore));
                });
            if (world == World)
            {
                if (shaping)
                {
                    _terrainGround[bodyId] = (terrain, types, radiusKm, ground);
                }

                _shownGround[bodyId] = (ground, sculpted, shown);
                PlanetSurface? globe = System?.SurfaceFor(bodyId);
                globe?.ShowPrepared(images);
                globe?.SetRoughness(RoughFor(bodyId, terrain, types, radiusKm, shaping));
            }
        }
        catch (ArgumentException exception)
        {
            // Saved worlds and edits are checked, so this is a bug.
            GD.PushError($"Couldn't prepare {body.Name}: {exception.Message}");
        }
        finally
        {
            _preparing.Remove(bodyId);
            Preparing?.Invoke(bodyId, false);
        }

        if (world == World && FindBody(bodyId) is Body now)
        {
            if (_gesture is not null)
            {
                // Mid-stroke: the stroke reshapes again soon, and when it ends.
                _reshapeWaiting.Add(bodyId);
            }
            else
            {
                ShowTerrain(now);  // Shows the rest, or catches up with edits made meanwhile.
            }
        }

        if (world == World && _preparing.Count == 0)
        {
            PrepareNext();
        }
    }

    // Starts the next body waiting its turn: the selected one if it's waiting, else the one
    // that's waited longest.
    private void PrepareNext()
    {
        while (_preparingNext.Count > 0)
        {
            Guid next = _preparingNext.Contains(SelectedBodyId)
                ? SelectedBodyId
                : _preparingNext[0];
            _preparingNext.Remove(next);
            Preparing?.Invoke(next, false);
            if (FindBody(next) is Body body)
            {
                ShowTerrain(body);  // Prepares it if it still needs it
                if (_preparing.Count > 0)
                {
                    return;
                }
            }
        }
    }

    // The ground a body's terrain shapes, worked out from what was known before: only the
    // tiles whose painting or types changed when that's possible, else all of it. Runs on a
    // worker thread.
    private static HeightGrid WorkOutGround(
        (TerrainGrid Terrain, TerrainType[] Types, double RadiusKm, HeightGrid Ground)? known,
        TerrainGrid terrain, TerrainType[] types, double radiusKm, int seed)
    {
        if (known is var (knownTerrain, knownTypes, knownRadius, knownGround)
            && knownRadius == radiusKm)
        {
            if (SameShaping(knownTypes, types))
            {
                return TerrainRelief.Update(knownGround, knownTerrain, terrain, types, radiusKm,
                    seed);
            }

            if (ReferenceEquals(knownTerrain, terrain))
            {
                return TerrainRelief.Rework(knownGround, terrain, knownTypes, types, radiusKm,
                    seed);
            }
        }

        // A new size makes features a different number of cells across: all of it again.
        return TerrainRelief.BaseHeights(terrain, types, radiusKm, seed);
    }

    private void ForgetPreparing()
    {
        _detailWaiting.Clear();
        _preparing.Clear();
        _preparingNext.Clear();
    }
}
