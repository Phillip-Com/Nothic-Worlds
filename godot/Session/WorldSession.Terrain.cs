using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Measurement;
using NothicWorlds.Core.Model;
using NothicWorlds.Rendering;

namespace NothicWorlds.Session;

// The terrain part of the open world (VISION.md BOD-05): the world's terrain types and the
// terrain painted on planets and moons, all undoable; and the ground the terrain shapes, when
// that's on (BOD-07).
public partial class WorldSession
{
    // Each body's ground as its terrain shapes it, kept with the painting and the types'
    // heights and edges it came from, so a paint stroke re-works only the tiles near it and a
    // new name or color re-works nothing.
    private readonly Dictionary<Guid,
        (TerrainGrid Terrain, TerrainType[] Types, double RadiusKm, HeightGrid Ground)>
        _terrainGround = [];

    // Each body's ground seen (terrain and sculpting together), kept with what it came from, so
    // the next can share every tile that didn't change and the globe redraws only what did:
    // making it afresh each time redrew the whole globe on every keystroke and brush move.
    private readonly Dictionary<Guid, (HeightGrid Base, HeightGrid Sculpted, HeightGrid Shaped)>
        _shownGround = [];

    // Each body's rough terrain's fine relief (see RoughFor), kept with what it came from.
    private readonly Dictionary<Guid, (TerrainGrid Terrain, TerrainType[] Types,
        double RadiusKm, bool Shaping, RoughGround? Rough)> _roughGround = [];

    // While a stroke paints, when each body's ground last went to its globe, and the bodies
    // whose newest ground hasn't yet (see ShowPainting).
    private const double ReshapeSeconds = 0.5;
    private readonly Dictionary<Guid, ulong> _reshapedAt = [];
    private readonly HashSet<Guid> _reshapeWaiting = [];

    // Colors offered in turn for new terrain types: distinct from the defaults.
    private static readonly RgbColor[] _newTerrainColors =
    [
        new(0xB0, 0x60, 0xC0),  // Violet
        new(0xD0, 0x50, 0x40),  // Rust
        new(0x50, 0xC0, 0xB0),  // Teal
        new(0xE0, 0xA0, 0x30),  // Gold
        new(0x90, 0x90, 0x98),  // Slate
    ];

    /// <summary>The world's terrain types, in list order.</summary>
    public IReadOnlyList<TerrainType> TerrainTypes => World.TerrainTypes;

    /// <summary>
    /// Whether painted terrain shapes the ground (VISION.md BOD-07): each type's height, with
    /// sculpting on top.
    /// </summary>
    public bool TerrainShapesGround => World.TerrainShapesGround;

    /// <summary>Turns terrain shaping the ground on or off (one undo step).</summary>
    public void SetTerrainShapesGround(bool on)
    {
        if (on == World.TerrainShapesGround)
        {
            return;
        }

        RecordUndo(on ? "Let Terrain Shape the Ground" : "Stop Terrain Shaping the Ground");
        World.TerrainShapesGround = on;
        TerrainTypesChanged();
    }

    /// <summary>
    /// Adds a terrain type with a default name and the next color in turn, at the end of the
    /// list.
    /// </summary>
    /// <returns>The new type, or null if the world already has the most allowed.</returns>
    public TerrainType? AddTerrainType()
    {
        if (TerrainType.FreeCode(World.TerrainTypes) is not byte code)
        {
            return null;
        }

        int number = 1;
        while (World.TerrainTypes.Any(type => type.Name == $"New Terrain {number}"))
        {
            number++;
        }

        var type = new TerrainType(code, $"New Terrain {number}",
            _newTerrainColors[World.TerrainTypes.Count % _newTerrainColors.Length]);
        RecordUndo("Add Terrain Type");
        World.TerrainTypes.Add(type);
        TerrainTypesChanged();
        return type;
    }

    /// <summary>
    /// Renames or recolors a terrain type (found by its code). Rapid changes to the same type
    /// (typing, picking a color) are one undo step.
    /// </summary>
    /// <returns>What's wrong with the change (nothing is changed then), or null.</returns>
    public string? UpdateTerrainType(TerrainType changed)
    {
        int index = World.TerrainTypes.FindIndex(type => type.Code == changed.Code);
        if (index < 0)
        {
            return null;
        }

        TerrainType current = World.TerrainTypes[index];
        changed = changed with { Name = changed.Name.Trim() };
        if (changed == current)
        {
            return null;
        }

        if (TerrainType.Problem([changed]) is string problem)
        {
            return problem;
        }

        RecordUndo($"Edit {current.Name}", mergeKey: ("terrain type", current.Code));
        World.TerrainTypes[index] = changed;
        TerrainTypesChanged();
        return null;
    }

    /// <summary>
    /// Deletes a terrain type, clearing it from every body it's painted on. One undo step.
    /// </summary>
    public void DeleteTerrainType(byte code)
    {
        int index = World.TerrainTypes.FindIndex(type => type.Code == code);
        if (index < 0)
        {
            return;
        }

        RecordUndo($"Delete {World.TerrainTypes[index].Name}");
        World.TerrainTypes.RemoveAt(index);
        foreach (Body body in World.Bodies)
        {
            body.Surface.Terrain = body.Surface.Terrain.Replace(code, 0);
            ShowTerrain(body);
        }

        TerrainTypesChanged();
    }

    /// <summary>
    /// Paints the selected body along a brush stroke from <paramref name="from"/> to
    /// <paramref name="to"/> (the same spot for a single dab), with terrain
    /// <paramref name="code"/> (0 erases). Wrap a whole stroke in <see cref="BeginGesture"/> and
    /// <see cref="EndGesture"/> to make it one undo step.
    /// </summary>
    public void PaintTerrain(
        GeoCoordinate from, GeoCoordinate to, double radiusDegrees, byte code)
    {
        if (!SelectedBodyHasSurface || IsBusy)
        {
            return;
        }

        Body body = SelectedBody;
        TerrainGrid painted = body.Surface.Terrain.PaintStroke(
            SphericalPolygon.ToUnit(from), SphericalPolygon.ToUnit(to), radiusDegrees, code);
        if (ReferenceEquals(painted, body.Surface.Terrain))
        {
            return;
        }

        RecordUndo(code == 0 ? "Erase Terrain" : "Paint Terrain");
        body.Surface.Terrain = painted;
        ShowPainting(body);
        MarkChanged(systemChanged: false);
    }

    /// <summary>
    /// Sculpts the selected body (VISION.md BOD-04) with a whole stroke so far, through
    /// <paramref name="path"/>'s spots, redone from <paramref name="before"/> (its heights when
    /// the stroke began), so a stroke drawn bit by bit has no bumps where the bits join. Wrap a
    /// whole stroke in <see cref="BeginGesture"/> and <see cref="EndGesture"/> to make it one
    /// undo step.
    /// </summary>
    /// <param name="before">The body's heights when the stroke began.</param>
    /// <param name="path">The stroke's spots so far, in order.</param>
    /// <param name="radiusDegrees">The brush's radius, in degrees of arc.</param>
    /// <param name="tool">Which brush.</param>
    /// <param name="strength">
    /// Meters for Raise and Lower; for Smooth and Flatten, how far (0 to 1) to go.
    /// </param>
    /// <param name="flattenTo">The height Flatten levels to, in meters.</param>
    public void SculptHeights(HeightGrid before, IReadOnlyList<GeoCoordinate> path,
        double radiusDegrees, SculptTool tool, double strength, double flattenTo)
    {
        if (!SelectedBodyHasSurface || IsBusy || path.Count == 0)
        {
            return;
        }

        Body body = SelectedBody;
        Vector3D[] points = [.. path.Select(SphericalPolygon.ToUnit)];
        HeightGrid under = TerrainGround(body);  // So the ground seen is levelled and smoothed
        HeightGrid sculpted = tool switch
        {
            SculptTool.Raise => before.Raise(points, radiusDegrees, strength),
            SculptTool.Lower => before.Raise(points, radiusDegrees, -strength),
            SculptTool.Smooth => before.Smooth(points, radiusDegrees, strength, under),
            _ => before.Flatten(points, radiusDegrees, flattenTo, strength, under),
        };
        if (sculpted.HasSameCells(body.Surface.Heights))
        {
            return;
        }

        RecordUndo($"{tool} Ground");
        body.Surface.Heights = sculpted;
        ShowTerrain(body);
        MarkChanged(systemChanged: false);
    }

    /// <summary>
    /// The ground's height at a spot on the selected body, in meters: what's sculpted, on the
    /// ground the terrain shapes when that's on.
    /// </summary>
    public double HeightAt(GeoCoordinate spot)
    {
        Vector3D direction = SphericalPolygon.ToUnit(spot);
        // While the selected body is prepared, just what's sculpted.
        return (TerrainGroundAtOnce(SelectedBody)?.SampleAt(direction) ?? 0)
            + SelectedBody.Surface.Heights.SampleAt(direction);
    }

    /// <summary>The terrain type painted at a spot on the selected body, or null.</summary>
    public TerrainType? TerrainAt(GeoCoordinate spot)
    {
        byte code = SelectedBody.Surface.Terrain.CodeAt(SphericalPolygon.ToUnit(spot));
        return World.TerrainTypes.Find(type => type.Code == code);
    }

    // Shows a stroke's painting straight away. When the terrain shapes the ground, the ground
    // is reshaped in the background (see StartPreparing) at most every ReshapeSeconds during
    // the stroke, and when it ends: reshaping and sending heights took a tenth of a second
    // or more, which on every brush move, and then every half second, made painting stall.
    private void ShowPainting(Body body)
    {
        if (!World.TerrainShapesGround || _gesture is null
            || System?.SurfaceFor(body.Id) is not PlanetSurface surface)
        {
            ShowTerrain(body);
            return;
        }

        surface.SetTerrain(body.Surface.Terrain);
        ulong now = Godot.Time.GetTicksMsec();
        _reshapedAt.TryGetValue(body.Id, out ulong last);
        if (now - last >= ReshapeSeconds * 1000)
        {
            StartPreparing(body, surface);
            _reshapedAt[body.Id] = now;
            _reshapeWaiting.Remove(body.Id);
        }
        else
        {
            _reshapeWaiting.Add(body.Id);
        }
    }

    // Reshapes the ground of bodies painted since it was last reshaped, as a stroke ends (in
    // the background; the terrain itself is shown already).
    private void ShowWaitingGround()
    {
        foreach (Guid id in _reshapeWaiting.ToList())
        {
            if (FindBody(id) is Body body && System?.SurfaceFor(id) is PlanetSurface surface)
            {
                StartPreparing(body, surface);
            }
        }

        _reshapeWaiting.Clear();
    }

    // Sends a body's terrain, and the colors to draw it in, to its globe: at once when it's
    // quick, else prepared in the background once the body is wanted (see StartPreparing).
    private void ShowTerrain(Body body)
    {
        if (System?.SurfaceFor(body.Id) is not PlanetSurface surface)
        {
            ShowWater(body);
            return;
        }

        surface.SetTerrainColors(World.TerrainTypes);
        surface.SetShapes(body.Surface.Shapes, body.RadiusKm);
        TerrainGrid terrain = body.Surface.Terrain;
        if (ShownGroundAtOnce(body) is not HeightGrid shown
            || surface.NeedsFirstImages(terrain, shown))
        {
            if (IsWanted(body, surface))
            {
                _detailWaiting.Remove(body.Id);
                StartPreparing(body, surface);
            }
            else
            {
                _detailWaiting.Add(body.Id);
            }

            return;
        }

        _detailWaiting.Remove(body.Id);
        surface.SetTerrain(terrain);
        surface.SetHeights(shown);
        surface.SetRoughness(RoughFor(body.Id, terrain, [.. World.TerrainTypes], body.RadiusKm,
            World.TerrainShapesGround));
        _reshapeWaiting.Remove(body.Id);
        ShowWater(body);  // The ground under the rivers and lakes may have changed.
    }

    // The ground a body's terrain shapes (none when that's off), when it can be had at once:
    // known already, nothing painted, or only a little of the painting changed since it was
    // known. Null when much has to be worked out, which is done in the background.
    private HeightGrid? TerrainGroundAtOnce(Body body)
    {
        if (!World.TerrainShapesGround || !body.HasSurface)
        {
            return HeightGrid.Empty;
        }

        TerrainGrid terrain = body.Surface.Terrain;
        TerrainType[] types = [.. World.TerrainTypes];
        double radiusKm = body.RadiusKm;
        if (_terrainGround.TryGetValue(body.Id, out var known) && known.RadiusKm == radiusKm
            && SameShaping(known.Types, types))
        {
            if (ReferenceEquals(known.Terrain, terrain))
            {
                return known.Ground;
            }

            if (TerrainRelief.ChangedTiles(known.Terrain, terrain) <= AtOnceTiles)
            {
                HeightGrid updated = TerrainRelief.Update(known.Ground, known.Terrain, terrain,
                    types, radiusKm, TerrainRelief.SeedFor(body.Id));
                _terrainGround[body.Id] = (terrain, types, radiusKm, updated);
                return updated;
            }
        }

        if (terrain.IsEmpty)
        {
            _terrainGround[body.Id] = (terrain, types, radiusKm, HeightGrid.Empty);
            return HeightGrid.Empty;
        }

        return null;
    }

    // The ground a body's terrain shapes, worked out now if need be: for an edit that needs it
    // straight away (sculpting the selected body, which is prepared already but for a moment).
    private HeightGrid TerrainGround(Body body)
    {
        if (TerrainGroundAtOnce(body) is HeightGrid ground)
        {
            return ground;
        }

        TerrainGrid terrain = body.Surface.Terrain;
        TerrainType[] types = [.. World.TerrainTypes];
        (TerrainGrid, TerrainType[], double, HeightGrid)? known =
            _terrainGround.TryGetValue(body.Id, out var had) ? had : null;
        ground = WorkOutGround(known, terrain, types, body.RadiusKm,
            TerrainRelief.SeedFor(body.Id));
        _terrainGround[body.Id] = (terrain, types, body.RadiusKm, ground);
        return ground;
    }

    // The ground a body shows, when it can be had at once (see TerrainGroundAtOnce): its
    // sculpting, on the ground its terrain shapes when that's on. Null while it's prepared.
    private HeightGrid? ShownGroundAtOnce(Body body)
    {
        if (TerrainGroundAtOnce(body) is not HeightGrid under)
        {
            return null;
        }

        HeightGrid sculpted = body.Surface.Heights;
        _shownGround.TryGetValue(body.Id, out var before);
        if (before.Shaped is not null && ReferenceEquals(before.Base, under)
            && ReferenceEquals(before.Sculpted, sculpted))
        {
            return before.Shaped;
        }

        HeightGrid shown = TerrainRelief.Shaped(under, sculpted,
            before.Shaped is null ? null : before);
        _shownGround[body.Id] = (under, sculpted, shown);
        return shown;
    }

    // A body's rough terrain's fine relief up close (VISION.md BOD-12), where its terrain shapes
    // the ground: the same as last time unless the terrain, or how rough the types make it,
    // has changed, so its globe's ground isn't rebuilt for nothing.
    private RoughGround? RoughFor(Guid bodyId, TerrainGrid terrain, TerrainType[] types,
        double radiusKm, bool shaping)
    {
        if (_roughGround.TryGetValue(bodyId, out var known)
            && ReferenceEquals(known.Terrain, terrain) && known.RadiusKm == radiusKm
            && known.Shaping == shaping && known.Types.Select(Roughening)
                .SequenceEqual(types.Select(Roughening)))
        {
            return known.Rough;
        }

        RoughGround? rough = shaping
            ? RoughGround.For(terrain, types, radiusKm, TerrainRelief.SeedFor(bodyId))
            : null;
        _roughGround[bodyId] = (terrain, types, radiusKm, shaping, rough);
        return rough;
    }

    private static (byte, int, double, double) Roughening(TerrainType type) =>
        (type.Code, type.VariationMeters, type.FeatureSizeKm, type.Roughness);

    // Whether two lists of types shape the ground the same (names and colors don't matter).
    private static bool SameShaping(TerrainType[] a, TerrainType[] b) =>
        a.Select(Shaping).SequenceEqual(b.Select(Shaping));

    private static (byte, int, double, int, double) Shaping(TerrainType type) =>
        (type.Code, type.HeightMeters, type.Edge, type.VariationMeters, type.FeatureSizeKm);

    private void TerrainTypesChanged()
    {
        foreach (Body body in World.Bodies)
        {
            ShowTerrain(body);
        }

        MarkChanged(systemChanged: false);
    }

    /// <summary>
    /// Gives a planet or moon water up to a level in meters (VISION.md BOD-09), changes the
    /// level, or (null) takes the water away, as one undo step (typing a level merges into
    /// one). Returns why a level was refused, or null.
    /// </summary>
    public string? SetWater(Guid bodyId, int? levelMeters)
    {
        if (FindBody(bodyId) is not Body body || !body.HasSurface
            || body.WaterLevelMeters == levelMeters)
        {
            return null;
        }

        if (levelMeters is < Body.MinWaterLevelMeters or > Body.MaxWaterLevelMeters)
        {
            UnitSystem units = AppSettings.Units;
            return "a water level must be " +
                $"{Units.Format(Quantity.Length, Body.MinWaterLevelMeters, units)} to " +
                Units.Format(Quantity.Length, Body.MaxWaterLevelMeters, units);
        }

        if (levelMeters is null)
        {
            RecordUndo($"Take {body.Name}'s Water");
        }
        else if (body.WaterLevelMeters is null)
        {
            RecordUndo($"Give {body.Name} Water");
        }
        else
        {
            RecordUndo($"Edit {body.Name}", mergeKey: ("water", bodyId));
        }

        body.WaterLevelMeters = levelMeters;
        SyncView();
        ShowWater(body);
        MarkChanged(systemChanged: false);
        return null;
    }
}
