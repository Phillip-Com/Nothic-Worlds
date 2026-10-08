using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Rendering;

namespace NothicWorlds.Session;

// The rivers and lakes of the open world (VISION.md BOD-11): adding, editing, and deleting
// them, all undoable, and working out where their water lies from the ground. That's done in
// the background, as the ground or the water changes, and shown when it's in.
public partial class WorldSession
{
    // How far above the ground at its spot a new lake's surface starts, in meters, when the
    // spot isn't in a hollow it can fill to the brim.
    private const int NewLakeDepthMeters = 50;

    // The most cells searched for the brim of a new lake's hollow: a hollow up to about 2,000
    // km across on an Earth-sized world, found in about a tenth of a second.
    private const int BrimSearchCells = 50_000;

    // Each body's water as last worked out, and what from.
    private readonly Dictionary<Guid, (WaterInputs Inputs, BodyWater Water)> _water = [];

    // The bodies whose water is being worked out now.
    private readonly HashSet<Guid> _waterUnderway = [];

    /// <summary>
    /// Raised when a body's water has been worked out again (or gone): lakes' shapes and
    /// rivers' courses may have changed.
    /// </summary>
    public event Action? WaterChanged;

    /// <summary>
    /// A body's water as last worked out (a moment behind the latest edit while it's being
    /// worked out again), or null if it has no rivers or lakes.
    /// </summary>
    public BodyWater? WaterOn(Guid bodyId) =>
        _water.TryGetValue(bodyId, out var known) ? known.Water : null;

    /// <summary>Whether a body's water is being worked out now.</summary>
    public bool IsWorkingOutWater(Guid bodyId) => _waterUnderway.Contains(bodyId);

    /// <summary>
    /// Adds a river on a planet or moon with a default name: a drawn one along
    /// <paramref name="points"/> (source first), or a natural one from its single point.
    /// </summary>
    /// <returns>The new river, or what's wrong (nothing is added then).</returns>
    public (River? River, string? Problem) AddRiver(
        Guid bodyId, RiverKind kind, IReadOnlyList<GeoCoordinate> points)
    {
        if (FindBody(bodyId) is not Body body || !body.HasSurface)
        {
            return (null, "rivers go on planets and moons");
        }

        if (World.Rivers.Count >= WaterRules.MaxRivers)
        {
            return (null, $"a world can hold up to {WaterRules.MaxRivers:N0} rivers");
        }

        var river = new River
        {
            BodyId = bodyId,
            Name = $"River {World.Rivers.Count(r => r.BodyId == bodyId) + 1}",
            Kind = kind,
            Points = [.. points],
        };
        if (river.Problem() is string problem)
        {
            return (null, problem);
        }

        RecordUndo("Add River");
        World.Rivers.Add(river);
        WaterEdited(body);
        return (river, null);
    }

    /// <summary>
    /// Replaces a river's name, width, or course (it's found by ID; it stays on its body).
    /// Rapid changes to the same river (typing) are one undo step.
    /// </summary>
    /// <returns>What's wrong with the change (nothing is changed then), or null.</returns>
    public string? UpdateRiver(River changed)
    {
        int index = World.Rivers.FindIndex(r => r.Id == changed.Id);
        if (index < 0)
        {
            return null;
        }

        River current = World.Rivers[index];
        changed = changed with { BodyId = current.BodyId };
        if (changed == current)
        {
            return null;
        }

        if (changed.Problem() is string problem)
        {
            return problem;
        }

        RecordUndo($"Edit {current.Name}", mergeKey: ("river", current.Id));
        World.Rivers[index] = changed;
        WaterEdited(FindBody(current.BodyId));
        return null;
    }

    /// <summary>Deletes a river, as one undo step.</summary>
    public void DeleteRiver(Guid riverId)
    {
        int index = World.Rivers.FindIndex(r => r.Id == riverId);
        if (index < 0)
        {
            return;
        }

        River river = World.Rivers[index];
        RecordUndo($"Delete {river.Name}");
        World.Rivers.RemoveAt(index);
        WaterEdited(FindBody(river.BodyId));
    }

    /// <summary>
    /// Adds a lake on a planet or moon with a default name where water at
    /// <paramref name="spot"/> would collect: the hollow it runs down into, filled to the brim,
    /// or (on level ground, or where it runs into the sea) at the spot, its surface a little
    /// above the ground.
    /// </summary>
    /// <returns>The new lake, or what's wrong (nothing is added then).</returns>
    public (Lake? Lake, string? Problem) AddLake(Guid bodyId, GeoCoordinate spot)
    {
        if (FindBody(bodyId) is not Body body || !body.HasSurface)
        {
            return (null, "lakes go on planets and moons");
        }

        if (World.Lakes.Count >= WaterRules.MaxLakes)
        {
            return (null, $"a world can hold up to {WaterRules.MaxLakes:N0} lakes");
        }

        if (ShownGroundAtOnce(body) is not HeightGrid ground)
        {
            return (null, $"{body.Name}'s ground is still being prepared: try again in a moment");
        }

        Func<int, bool> isSea = BodyWater.SeaRule(body, ground, World.TerrainTypes)
            ?? (_ => false);
        Vector3D direction = SphericalPolygon.ToUnit(spot);
        if (isSea(WaterCells.IndexAt(direction)))
        {
            return (null, "that spot is in the sea: lakes go on land");
        }

        (GeoCoordinate seatSpot, int level) =
            LakeFill.HollowBelow(ground, direction, isSea, BrimSearchCells) is LakeSeat seat
                ? (SphericalPolygon.FromUnit(seat.Spot), seat.LevelMeters)
                : (spot, (int)Math.Round(ground.SampleAt(direction)) + NewLakeDepthMeters);
        var lake = new Lake
        {
            BodyId = bodyId,
            Name = $"Lake {World.Lakes.Count(l => l.BodyId == bodyId) + 1}",
            Spot = seatSpot,
            LevelMeters = Math.Clamp(level, Lake.MinLevelMeters, Lake.MaxLevelMeters),
        };
        if (lake.Problem() is string problem)
        {
            return (null, problem);
        }

        RecordUndo("Add Lake");
        World.Lakes.Add(lake);
        WaterEdited(body);
        return (lake, null);
    }

    /// <summary>
    /// Replaces a lake's name, surface height, or whether it flows out (it's found by ID; it
    /// stays on its body). Rapid changes to the same lake (typing) are one undo step.
    /// </summary>
    /// <returns>What's wrong with the change (nothing is changed then), or null.</returns>
    public string? UpdateLake(Lake changed)
    {
        int index = World.Lakes.FindIndex(l => l.Id == changed.Id);
        if (index < 0)
        {
            return null;
        }

        Lake current = World.Lakes[index];
        changed = changed with { BodyId = current.BodyId };
        if (changed == current)
        {
            return null;
        }

        if (changed.Problem() is string problem)
        {
            return problem;
        }

        RecordUndo($"Edit {current.Name}", mergeKey: ("lake", current.Id));
        World.Lakes[index] = changed;
        WaterEdited(FindBody(current.BodyId));
        return null;
    }

    /// <summary>Deletes a lake (and so its outflow), as one undo step.</summary>
    public void DeleteLake(Guid lakeId)
    {
        int index = World.Lakes.FindIndex(l => l.Id == lakeId);
        if (index < 0)
        {
            return;
        }

        Lake lake = World.Lakes[index];
        RecordUndo($"Delete {lake.Name}");
        World.Lakes.RemoveAt(index);
        WaterEdited(FindBody(lake.BodyId));
    }

    private void WaterEdited(Body? body)
    {
        if (body is not null)
        {
            ShowWater(body);
        }

        MarkChanged(systemChanged: false);
    }

    // Brings a body's water up to date with its ground, rivers, and lakes: shows it straight
    // away if it's known, or starts working it out. While the body's ground is still being
    // prepared, it waits: preparing shows the terrain again when it's done, and so the water.
    private void ShowWater(Body body)
    {
        if (!body.HasSurface)
        {
            return;
        }

        bool dry = World.Rivers.All(r => r.BodyId != body.Id)
            && World.Lakes.All(l => l.BodyId != body.Id);
        HeightGrid? ground = dry ? HeightGrid.Empty : ShownGroundAtOnce(body);
        if (ground is null)
        {
            return;
        }

        var wanted = WaterInputs.Of(body, ground, World);
        if (wanted.IsDry)
        {
            if (_water.Remove(body.Id))
            {
                SendWater(body.Id, null);
                WaterChanged?.Invoke();
            }

            return;
        }

        if (_water.TryGetValue(body.Id, out var known) && known.Inputs.Matches(wanted))
        {
            SendWater(body.Id, known.Water);
            return;
        }

        StartWorkingOutWater(body, wanted);
    }

    // Works out a body's water in the background, unless that's already under way (it checks
    // again when it's done, so the latest edit is never missed).
    private async void StartWorkingOutWater(Body body, WaterInputs wanted)
    {
        Guid bodyId = body.Id;
        if (!_waterUnderway.Add(bodyId))
        {
            return;
        }

        World world = World;
        Body snapshot = body.Clone();
        WaterChanged?.Invoke();  // Shows that it's being worked out.
        BodyWater? water = null;
        try
        {
            water = await Task.Run(() => BodyWater.For(snapshot, wanted.Ground, wanted.Lakes,
                wanted.Rivers, wanted.Types));
        }
        catch (ArgumentException exception)
        {
            // Saved worlds and edits are checked, so this is a bug.
            GD.PushError($"Couldn't work out the water on {snapshot.Name}: {exception.Message}");
        }
        finally
        {
            _waterUnderway.Remove(bodyId);
        }

        if (world != World || FindBody(bodyId) is not Body now)
        {
            return;
        }

        if (water is not null)
        {
            _water[bodyId] = (wanted, water);
        }

        ShowWater(now);  // Shows it, or catches up with edits made meanwhile.
        WaterChanged?.Invoke();
    }

    private void SendWater(Guid bodyId, BodyWater? water)
    {
        if (System?.SurfaceFor(bodyId) is PlanetSurface surface)
        {
            surface.SetLakeLevels(water?.LakeLevels ?? HeightGrid.Empty);
        }
    }

    private void ForgetWater()
    {
        _water.Clear();
        _waterUnderway.Clear();
    }

    // What a body's water is worked out from. The grids are immutable, so comparing them by
    // reference is enough.
    private sealed record WaterInputs(HeightGrid Ground, TerrainGrid Terrain, int? Level,
        List<River> Rivers, List<Lake> Lakes, List<TerrainType> Types)
    {
        public bool IsDry => Rivers.Count == 0 && Lakes.Count == 0;

        public static WaterInputs Of(Body body, HeightGrid ground, World world) => new(ground,
            body.Surface.Terrain, body.WaterLevelMeters,
            [.. world.Rivers.Where(r => r.BodyId == body.Id)],
            [.. world.Lakes.Where(l => l.BodyId == body.Id)],
            [.. world.TerrainTypes.Where(t => t.Climate == ClimateKind.Water)]);

        public bool Matches(WaterInputs other) =>
            ReferenceEquals(Ground, other.Ground) && ReferenceEquals(Terrain, other.Terrain)
            && Level == other.Level && Rivers.SequenceEqual(other.Rivers)
            && Lakes.SequenceEqual(other.Lakes) && Types.SequenceEqual(other.Types);
    }
}
