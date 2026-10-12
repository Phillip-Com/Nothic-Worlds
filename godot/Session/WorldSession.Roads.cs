using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Session;

// The roads of the open world (VISION.md BOD-13): adding, editing, and deleting them, all
// undoable, and working out the ways they take from the ground and the water. That's done in
// the background, after the water, as the ground, the water, or the roads change, and shown
// when it's in.
public partial class WorldSession
{
    // Each body's roads as last worked out, and what from.
    private readonly Dictionary<Guid, (RoadInputs Inputs, IReadOnlyList<RoadCourseShown> Ways)>
        _roads = [];

    // The bodies whose roads are being worked out now.
    private readonly HashSet<Guid> _roadsUnderway = [];

    /// <summary>
    /// Raised when a body's roads have been worked out again (or gone): their ways may have
    /// changed.
    /// </summary>
    public event Action? RoadsChanged;

    /// <summary>
    /// The ways a body's roads take as last worked out (a moment behind the latest edit while
    /// they're being worked out again); empty if it has none.
    /// </summary>
    public IReadOnlyList<RoadCourseShown> RoadsOn(Guid bodyId) =>
        _roads.TryGetValue(bodyId, out var known) ? known.Ways : [];

    /// <summary>Whether a body's roads are being worked out now (or wait for its water).</summary>
    public bool IsWorkingOutRoads(Guid bodyId) =>
        _roadsUnderway.Contains(bodyId)
        || (World.Roads.Any(r => r.BodyId == bodyId) && _waterUnderway.Contains(bodyId));

    /// <summary>
    /// Adds a road on a planet or moon with a default name and width for its style, through
    /// <paramref name="points"/> (first first).
    /// </summary>
    /// <returns>The new road, or what's wrong (nothing is added then).</returns>
    public (Road? Road, string? Problem) AddRoad(Guid bodyId, RoadStyle style, RoadKind kind,
        IReadOnlyList<GeoCoordinate> points)
    {
        if (FindBody(bodyId) is not Body body || !body.HasSurface)
        {
            return (null, "roads go on planets and moons");
        }

        if (World.Roads.Count >= RoadRules.MaxRoads)
        {
            return (null, $"a world can hold up to {RoadRules.MaxRoads:N0} roads");
        }

        var road = new Road
        {
            BodyId = bodyId,
            Name = $"{StyleName(style)} " +
                $"{World.Roads.Count(r => r.BodyId == bodyId && r.Style == style) + 1}",
            Style = style,
            Kind = kind,
            Points = [.. points],
            WidthMeters = Road.DefaultWidthMeters(style),
        };
        if (road.Problem() is string problem)
        {
            return (null, problem);
        }

        RecordUndo("Add Road");
        World.Roads.Add(road);
        RoadsEdited(body);
        return (road, null);
    }

    /// <summary>
    /// Replaces a road's name, style, or width (it's found by ID; it stays on its body). Rapid
    /// changes to the same road (typing) are one undo step.
    /// </summary>
    /// <returns>What's wrong with the change (nothing is changed then), or null.</returns>
    public string? UpdateRoad(Road changed)
    {
        int index = World.Roads.FindIndex(r => r.Id == changed.Id);
        if (index < 0)
        {
            return null;
        }

        Road current = World.Roads[index];
        changed = changed with { BodyId = current.BodyId };
        if (changed == current)
        {
            return null;
        }

        if (changed.Problem() is string problem)
        {
            return problem;
        }

        RecordUndo($"Edit {current.Name}", mergeKey: ("road", current.Id));
        World.Roads[index] = changed;
        RoadsEdited(FindBody(current.BodyId));
        return null;
    }

    /// <summary>Deletes a road, as one undo step.</summary>
    public void DeleteRoad(Guid roadId)
    {
        int index = World.Roads.FindIndex(r => r.Id == roadId);
        if (index < 0)
        {
            return;
        }

        Road road = World.Roads[index];
        RecordUndo($"Delete {road.Name}");
        World.Roads.RemoveAt(index);
        RoadsEdited(FindBody(road.BodyId));
    }

    /// <summary>What a road style is called, for names and lists.</summary>
    public static string StyleName(RoadStyle style) => style switch
    {
        RoadStyle.StoneHighway => "Stone Highway",
        RoadStyle.DirtPaths => "Dirt Paths",
        RoadStyle.CartTrack => "Cart Track",
        _ => "Old Road",
    };

    private void RoadsEdited(Body? body)
    {
        if (body is not null)
        {
            ShowRoads(body);
        }

        MarkChanged(systemChanged: false);
    }

    // Brings a body's roads up to date with its ground, water, terrain, and roads: at once if
    // they're known, or worked out. They wait while the body's ground is being prepared or its
    // water worked out: both show the water again when they're done (ShowWater), and so these.
    private void ShowRoads(Body body)
    {
        if (!body.HasSurface)
        {
            return;
        }

        if (World.Roads.All(r => r.BodyId != body.Id))
        {
            if (_roads.Remove(body.Id))
            {
                RoadsChanged?.Invoke();
            }

            return;
        }

        bool wet = World.Rivers.Any(r => r.BodyId == body.Id)
            || World.Lakes.Any(l => l.BodyId == body.Id);
        if (ShownGroundAtOnce(body) is not HeightGrid ground
            || (wet && (_waterUnderway.Contains(body.Id) || !_water.ContainsKey(body.Id))))
        {
            return;
        }

        var wanted = RoadInputs.Of(body, ground, wet ? _water[body.Id].Water : null, World);
        if (_roads.TryGetValue(body.Id, out var known) && known.Inputs.Matches(wanted))
        {
            return;
        }

        StartWorkingOutRoads(body, wanted);
    }

    // Works out a body's roads in the background, unless that's already under way (it checks
    // again when it's done, so the latest edit is never missed).
    private async void StartWorkingOutRoads(Body body, RoadInputs wanted)
    {
        Guid bodyId = body.Id;
        if (!_roadsUnderway.Add(bodyId))
        {
            return;
        }

        World world = World;
        Body snapshot = body.Clone();
        RoadsChanged?.Invoke();  // Shows that they're being worked out.
        IReadOnlyList<RoadCourseShown>? ways = null;
        try
        {
            ways = await Task.Run(() => BodyRoads.For(snapshot, wanted.Ground, wanted.Water,
                wanted.Roads, wanted.Types));
        }
        catch (ArgumentException exception)
        {
            // Saved worlds and edits are checked, so this is a bug.
            GD.PushError($"Couldn't work out the roads on {snapshot.Name}: {exception.Message}");
        }
        finally
        {
            _roadsUnderway.Remove(bodyId);
        }

        if (world != World || FindBody(bodyId) is not Body now)
        {
            return;
        }

        if (ways is not null)
        {
            _roads[bodyId] = (wanted, ways);
        }

        ShowRoads(now);  // Catches up with edits made meanwhile.
        RoadsChanged?.Invoke();
    }

    private void ForgetRoads()
    {
        _roads.Clear();
        _roadsUnderway.Clear();
    }

    // What a body's roads are worked out from. The grids and the water are immutable, so
    // comparing them by reference is enough.
    private sealed record RoadInputs(HeightGrid Ground, TerrainGrid Terrain, int? Level,
        BodyWater? Water, List<Road> Roads, List<TerrainType> Types)
    {
        public static RoadInputs Of(Body body, HeightGrid ground, BodyWater? water,
            World world) => new(ground, body.Surface.Terrain, body.WaterLevelMeters, water,
            [.. world.Roads.Where(r => r.BodyId == body.Id)], [.. world.TerrainTypes]);

        public bool Matches(RoadInputs other) =>
            ReferenceEquals(Ground, other.Ground) && ReferenceEquals(Terrain, other.Terrain)
            && Level == other.Level && ReferenceEquals(Water, other.Water)
            && Roads.SequenceEqual(other.Roads) && Types.SequenceEqual(other.Types);
    }
}
