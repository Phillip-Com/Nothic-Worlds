using NothicWorlds.Core.Model;

namespace NothicWorlds.Session;

// The relationship diagrams of the open world (VISION.md LORE-04): adding, editing, and deleting
// relationships and diagrams, and placing entries on diagrams, all undoable. Kept in its own
// file to keep WorldSession.cs manageable.
public partial class WorldSession
{
    /// <summary>
    /// Adds a relationship (checked against the world first) and returns null, or what's
    /// wrong with it (nothing is changed then).
    /// </summary>
    public string? AddRelationship(Relationship relationship)
    {
        if (CheckRelationship(relationship) is string problem)
        {
            return problem;
        }

        RecordUndo("Add Relationship");
        World.Relationships.Add(relationship);
        MarkChanged(systemChanged: false);
        return null;
    }

    /// <summary>
    /// Replaces a relationship (found by ID) and returns null, or what's wrong with the change
    /// (nothing is changed then).
    /// </summary>
    public string? UpdateRelationship(Relationship changed)
    {
        int index = World.Relationships.FindIndex(r => r.Id == changed.Id);
        if (index < 0 || World.Relationships[index] == changed)
        {
            return null;
        }

        if (CheckRelationship(changed) is string problem)
        {
            return problem;
        }

        RecordUndo("Edit Relationship");
        World.Relationships[index] = changed;
        MarkChanged(systemChanged: false);
        return null;
    }

    /// <summary>Deletes a relationship; its two entries stay.</summary>
    public void DeleteRelationship(Guid relationshipId)
    {
        int index = World.Relationships.FindIndex(r => r.Id == relationshipId);
        if (index < 0)
        {
            return;
        }

        RecordUndo("Delete Relationship");
        World.Relationships.RemoveAt(index);
        MarkChanged(systemChanged: false);
    }

    /// <summary>Adds an empty diagram named after how many there are, and returns it.</summary>
    public LoreDiagram AddDiagram()
    {
        RecordUndo("Add Diagram");
        var diagram = new LoreDiagram { Name = $"Diagram {World.Diagrams.Count + 1}" };
        World.Diagrams.Add(diagram);
        MarkChanged(systemChanged: false);
        return diagram;
    }

    /// <summary>
    /// Renames a diagram and returns null, or what's wrong with the name (nothing is changed
    /// then). Typing the same diagram's name is one undo step.
    /// </summary>
    public string? RenameDiagram(Guid diagramId, string name)
    {
        int index = World.Diagrams.FindIndex(d => d.Id == diagramId);
        if (index < 0 || World.Diagrams[index].Name == name)
        {
            return null;
        }

        LoreDiagram renamed = World.Diagrams[index] with { Name = name };
        if (renamed.Problem() is string problem)
        {
            return problem;
        }

        RecordUndo("Rename Diagram", mergeKey: ("diagram-name", diagramId));
        World.Diagrams[index] = renamed;
        MarkChanged(systemChanged: false);
        return null;
    }

    /// <summary>Deletes a diagram; its entries and their relationships stay.</summary>
    public void DeleteDiagram(Guid diagramId)
    {
        int index = World.Diagrams.FindIndex(d => d.Id == diagramId);
        if (index < 0)
        {
            return;
        }

        RecordUndo($"Delete {World.Diagrams[index].Name}");
        World.Diagrams.RemoveAt(index);
        MarkChanged(systemChanged: false);
    }

    /// <summary>
    /// Puts an entry's box on a diagram at a spot, or moves it there if it's already on it.
    /// Inside a drag (<see cref="BeginGesture"/>), all the moves are one undo step.
    /// </summary>
    /// <returns>What's wrong (the diagram is full, the spot out of bounds), or null.</returns>
    public string? PlaceOnDiagram(Guid diagramId, Guid entryId, double x, double y)
    {
        int index = World.Diagrams.FindIndex(d => d.Id == diagramId);
        if (index < 0 || !World.Journal.Any(e => e.Id == entryId))
        {
            return null;
        }

        LoreDiagram diagram = World.Diagrams[index];
        var placement = new DiagramPlacement(entryId, x, y);
        int at = diagram.Placements.ToList().FindIndex(p => p.EntryId == entryId);
        if (at >= 0 && diagram.Placements[at] == placement)
        {
            return null;
        }

        List<DiagramPlacement> placements = [.. diagram.Placements];
        if (at >= 0)
        {
            placements[at] = placement;
        }
        else
        {
            placements.Add(placement);
        }

        LoreDiagram changed = diagram with { Placements = placements };
        if (changed.Problem() is string problem)
        {
            return problem;
        }

        RecordUndo(at >= 0 ? "Move Box" : "Add to Diagram");
        World.Diagrams[index] = changed;
        MarkChanged(systemChanged: false);
        return null;
    }

    /// <summary>Takes an entry's box off a diagram; the entry and its ties stay.</summary>
    public void RemoveFromDiagram(Guid diagramId, Guid entryId)
    {
        int index = World.Diagrams.FindIndex(d => d.Id == diagramId);
        if (index < 0 || !World.Diagrams[index].Placements.Any(p => p.EntryId == entryId))
        {
            return;
        }

        RecordUndo("Remove from Diagram");
        World.Diagrams[index] = World.Diagrams[index] with
        {
            Placements = [.. World.Diagrams[index].Placements.Where(p => p.EntryId != entryId)],
        };
        MarkChanged(systemChanged: false);
    }

    /// <summary>
    /// Lays a diagram out tidily (<see cref="DiagramLayout"/>: a family tree, then the rest),
    /// as one undo step.
    /// </summary>
    public void ArrangeDiagram(Guid diagramId)
    {
        int index = World.Diagrams.FindIndex(d => d.Id == diagramId);
        if (index < 0 || World.Diagrams[index].Placements.Count == 0)
        {
            return;
        }

        LoreDiagram diagram = World.Diagrams[index];
        IReadOnlyList<DiagramPlacement> arranged = DiagramLayout.Arrange(
            [.. diagram.Placements.Select(p => p.EntryId)], World.Relationships);
        if (arranged.SequenceEqual(diagram.Placements))
        {
            return;
        }

        RecordUndo($"Arrange {diagram.Name}");
        World.Diagrams[index] = diagram with { Placements = arranged };
        MarkChanged(systemChanged: false);
    }

    // What's wrong with a relationship in this world, or null.
    private string? CheckRelationship(Relationship relationship)
    {
        if (relationship.Problem() is string problem)
        {
            return problem;
        }

        return World.Journal.Any(e => e.Id == relationship.FromEntryId)
            && World.Journal.Any(e => e.Id == relationship.ToEntryId)
            ? null
            : "one of its journal entries no longer exists";
    }
}
