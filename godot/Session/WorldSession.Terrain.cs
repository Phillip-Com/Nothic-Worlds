using NothicWorlds.Core.Geometry;
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
    private readonly Dictionary<Guid, (TerrainGrid Terrain, TerrainType[] Types, HeightGrid Ground)>
        _terrainGround = [];

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
        ShowTerrain(body);
        MarkChanged(systemChanged: false);
    }

    /// <summary>
    /// True if the selected body can be sculpted (VISION.md BOD-04): a planet or moon shaped as
    /// a globe (flat worlds don't draw heights yet).
    /// </summary>
    public bool SelectedBodyCanBeSculpted =>
        SelectedBodyHasSurface && SelectedBody.Shape == BodyShape.Sphere;

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
        if (!SelectedBodyCanBeSculpted || IsBusy || path.Count == 0)
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
        return TerrainGround(SelectedBody).SampleAt(direction)
            + SelectedBody.Surface.Heights.SampleAt(direction);
    }

    /// <summary>The terrain type painted at a spot on the selected body, or null.</summary>
    public TerrainType? TerrainAt(GeoCoordinate spot)
    {
        byte code = SelectedBody.Surface.Terrain.CodeAt(SphericalPolygon.ToUnit(spot));
        return World.TerrainTypes.Find(type => type.Code == code);
    }

    // Sends a body's terrain, and the colors to draw it in, to its globe.
    private void ShowTerrain(Body body)
    {
        if (System?.SurfaceFor(body.Id) is PlanetSurface surface)
        {
            surface.SetTerrainColors(World.TerrainTypes);
            surface.SetTerrain(body.Surface.Terrain);
            surface.SetHeights(TerrainRelief.Shaped(TerrainGround(body), body.Surface.Heights));
            surface.SetShapes(body.Surface.Shapes, body.RadiusKm);
        }
    }

    // The ground a body's terrain shapes, or none when that's off (or it's a flat world, which
    // doesn't draw heights).
    private HeightGrid TerrainGround(Body body)
    {
        if (!World.TerrainShapesGround || !body.HasSurface || body.Shape != BodyShape.Sphere)
        {
            return HeightGrid.Empty;
        }

        TerrainGrid terrain = body.Surface.Terrain;
        TerrainType[] types = [.. World.TerrainTypes];
        if (_terrainGround.TryGetValue(body.Id, out var known) && SameShaping(known.Types, types))
        {
            if (ReferenceEquals(known.Terrain, terrain))
            {
                return known.Ground;
            }

            HeightGrid updated = TerrainRelief.Update(known.Ground, known.Terrain, terrain, types);
            _terrainGround[body.Id] = (terrain, types, updated);
            return updated;
        }

        HeightGrid ground = TerrainRelief.BaseHeights(terrain, types);
        _terrainGround[body.Id] = (terrain, types, ground);
        return ground;
    }

    // Whether two lists of types shape the ground the same (names and colors don't matter).
    private static bool SameShaping(TerrainType[] a, TerrainType[] b) =>
        a.Select(t => (t.Code, t.HeightMeters, t.Edge))
            .SequenceEqual(b.Select(t => (t.Code, t.HeightMeters, t.Edge)));

    private void TerrainTypesChanged()
    {
        foreach (Body body in World.Bodies)
        {
            ShowTerrain(body);
        }

        MarkChanged(systemChanged: false);
    }
}
