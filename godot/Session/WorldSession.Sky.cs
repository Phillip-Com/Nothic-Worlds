using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Session;

// The open world's night sky (VISION.md REN-07): re-rolling its stars, and adding, naming,
// drawing, and deleting constellations, all undoable. Kept in its own file to keep
// WorldSession.cs manageable.
public partial class WorldSession
{
    /// <summary>
    /// Gives the sky new stars from a fresh seed and returns null, or says why it can't (the
    /// constellations are drawn on the stars there are, so they must go first).
    /// </summary>
    public string? RerollStars()
    {
        if (World.Constellations.Count > 0)
        {
            return "Delete the constellations first: they're drawn on these stars";
        }

        RecordUndo("New Stars");
        World.StarSeed = StarField.NewSeed();
        MarkChanged(systemChanged: false);
        return null;
    }

    /// <summary>
    /// Adds an empty constellation named after how many there are, and returns it, or null if
    /// the sky has as many as it can hold.
    /// </summary>
    public Constellation? AddConstellation()
    {
        if (World.Constellations.Count >= Constellation.MaxCount)
        {
            return null;
        }

        RecordUndo("Add Constellation");
        var constellation = new Constellation
        {
            Name = $"Constellation {World.Constellations.Count + 1}",
        };
        World.Constellations.Add(constellation);
        MarkChanged(systemChanged: false);
        return constellation;
    }

    /// <summary>
    /// Renames a constellation and returns null, or what's wrong with the name (nothing is
    /// changed then). Typing one constellation's name is one undo step.
    /// </summary>
    public string? RenameConstellation(Guid id, string name)
    {
        int index = World.Constellations.FindIndex(c => c.Id == id);
        if (index < 0 || World.Constellations[index].Name == name)
        {
            return null;
        }

        Constellation renamed = World.Constellations[index] with { Name = name };
        if (renamed.Problem(World.StarSeed) is string problem)
        {
            return problem;
        }

        RecordUndo("Rename Constellation", mergeKey: ("constellation-name", id));
        World.Constellations[index] = renamed;
        MarkChanged(systemChanged: false);
        return null;
    }

    /// <summary>Deletes a constellation (its stars stay on the sky).</summary>
    public void DeleteConstellation(Guid id)
    {
        int index = World.Constellations.FindIndex(c => c.Id == id);
        if (index < 0)
        {
            return;
        }

        RecordUndo($"Delete {World.Constellations[index].Name}");
        World.Constellations.RemoveAt(index);
        MarkChanged(systemChanged: false);
    }

    /// <summary>
    /// Joins two stars in a constellation and returns null, or what's wrong (the same star,
    /// a line it already has, or too many lines; nothing is changed then).
    /// </summary>
    public string? AddConstellationLine(Guid id, StarLink line)
    {
        int index = World.Constellations.FindIndex(c => c.Id == id);
        if (index < 0)
        {
            return "that constellation is gone";
        }

        Constellation current = World.Constellations[index];
        Constellation joined = current with { Lines = [.. current.Lines, line] };
        if (joined.Problem(World.StarSeed) is string problem)
        {
            return problem;
        }

        RecordUndo($"Draw {current.Name}");
        World.Constellations[index] = joined;
        MarkChanged(systemChanged: false);
        return null;
    }

    /// <summary>Takes a line (either way round) out of a constellation.</summary>
    public void RemoveConstellationLine(Guid id, StarLink line)
    {
        int index = World.Constellations.FindIndex(c => c.Id == id);
        if (index < 0 || !World.Constellations[index].Lines.Any(line.Joins))
        {
            return;
        }

        Constellation current = World.Constellations[index];
        RecordUndo($"Erase a Line of {current.Name}");
        World.Constellations[index] =
            current with { Lines = [.. current.Lines.Where(l => !l.Joins(line))] };
        MarkChanged(systemChanged: false);
    }
}
