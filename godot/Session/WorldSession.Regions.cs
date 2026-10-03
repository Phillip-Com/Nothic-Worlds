using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Session;

// The regions part of the open world (VISION.md LORE-01): adding, editing, and deleting the
// areas outlined on planets and moons, all undoable.
public partial class WorldSession
{
    // Region colors offered in turn: warm and readable over most maps.
    private static readonly RgbColor[] _regionColors =
    [
        new(0xE6, 0xC8, 0x78),  // Sand
        new(0x78, 0xB4, 0xE6),  // Sky
        new(0xE6, 0x82, 0x82),  // Rose
        new(0x8C, 0xD2, 0x82),  // Leaf
        new(0xC8, 0x96, 0xE6),  // Heather
        new(0xF0, 0xA0, 0x50),  // Amber
    ];

    /// <summary>
    /// Adds a region on a planet or moon with the given outline, a default name, and the next
    /// color in turn.
    /// </summary>
    /// <returns>The new region, or what's wrong with the outline (nothing is added then).</returns>
    public (Region? Region, string? Problem) AddRegion(
        Guid bodyId, IReadOnlyList<GeoCoordinate> corners)
    {
        if (FindBody(bodyId) is not Body body || !body.HasSurface)
        {
            return (null, "regions go on planets and moons");
        }

        var region = new Region
        {
            BodyId = bodyId,
            Name = $"Region {World.Regions.Count(r => r.BodyId == bodyId) + 1}",
            Color = _regionColors[World.Regions.Count % _regionColors.Length],
            Corners = [.. corners],
        };
        if (region.Problem() is string problem)
        {
            return (null, problem);
        }

        RecordUndo("Add Region");
        World.Regions.Add(region);
        MarkChanged(systemChanged: false);
        return (region, null);
    }

    /// <summary>
    /// Replaces a region's name, notes, color, or outline (it's found by ID; it stays on its
    /// body). Rapid changes to the same region (typing, picking a color) are one undo step;
    /// dragging its points is one step through <see cref="BeginGesture"/>.
    /// </summary>
    /// <returns>What's wrong with the change (nothing is changed then), or null.</returns>
    public string? UpdateRegion(Region changed)
    {
        int index = World.Regions.FindIndex(r => r.Id == changed.Id);
        if (index < 0)
        {
            return null;
        }

        Region current = World.Regions[index];
        changed = changed with { BodyId = current.BodyId };
        if (changed == current)
        {
            return null;
        }

        if (changed.Problem() is string problem)
        {
            return problem;
        }

        RecordUndo($"Edit {current.Name}", mergeKey: ("region", current.Id));
        World.Regions[index] = changed;
        MarkChanged(systemChanged: false);
        return null;
    }

    /// <summary>
    /// Deletes a region. Entries and events placed in it stay on its body, just not in the
    /// region. One undo step.
    /// </summary>
    public void DeleteRegion(Guid regionId)
    {
        int index = World.Regions.FindIndex(r => r.Id == regionId);
        if (index < 0)
        {
            return;
        }

        RecordUndo($"Delete {World.Regions[index].Name}");
        World.Regions.RemoveAt(index);
        for (int i = 0; i < World.Journal.Count; i++)
        {
            World.Journal[i] = World.Journal[i] with
            {
                Location = OutOf(World.Journal[i].Location, regionId),
            };
        }

        for (int i = 0; i < World.Events.Count; i++)
        {
            World.Events[i] = World.Events[i] with
            {
                Location = OutOf(World.Events[i].Location, regionId),
            };
        }

        MarkChanged(systemChanged: false);
    }

    // A place, no longer in a region (unchanged if it wasn't in it).
    private static LoreLocation? OutOf(LoreLocation? place, Guid regionId)
    {
        return place?.RegionId == regionId ? place with { RegionId = null } : place;
    }
}
