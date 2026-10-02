using NothicWorlds.Core.Model;

namespace NothicWorlds.Session;

// The journal part of the open world (VISION.md LORE-02): adding, editing, and deleting
// entries, all undoable. Kept in its own file to keep WorldSession.cs manageable.
public partial class WorldSession
{
    /// <summary>
    /// Adds an empty journal entry, placed nowhere in particular, and returns it.
    /// </summary>
    public JournalEntry AddJournalEntry()
    {
        RecordUndo("Add Journal Entry");
        var entry = new JournalEntry { Title = "Untitled entry" };
        World.Journal.Add(entry);
        MarkChanged(systemChanged: false);
        return entry;
    }

    /// <summary>
    /// Replaces a journal entry's title, text, and place (it's found by ID; its created time is
    /// kept and its edited time set to now). Typing in the same entry keeps adding to one undo
    /// step.
    /// </summary>
    /// <returns>What's wrong with the change (nothing is changed then), or null.</returns>
    public string? UpdateJournalEntry(JournalEntry changed)
    {
        int index = World.Journal.FindIndex(e => e.Id == changed.Id);
        if (index < 0)
        {
            return null;
        }

        JournalEntry current = World.Journal[index];
        if (changed with { CreatedUtc = current.CreatedUtc, EditedUtc = current.EditedUtc }
            == current)
        {
            return null;  // Nothing actually changed.
        }

        JournalEntry updated = changed with
        {
            CreatedUtc = current.CreatedUtc,
            EditedUtc = DateTimeOffset.UtcNow,
        };
        if (updated.Problem() is string problem)
        {
            return problem;
        }

        if (updated.Location is LoreLocation place && FindBody(place.BodyId) is null)
        {
            return "that body no longer exists";
        }

        RecordUndo($"Edit {current.Title}", mergeKey: ("journal", current.Id));
        World.Journal[index] = updated;
        MarkChanged(systemChanged: false);
        return null;
    }

    /// <summary>
    /// Deletes a journal entry, and its links from timeline events, as one undo step.
    /// </summary>
    public void DeleteJournalEntry(Guid entryId)
    {
        int index = World.Journal.FindIndex(e => e.Id == entryId);
        if (index < 0)
        {
            return;
        }

        RecordUndo($"Delete {World.Journal[index].Title}");
        World.Journal.RemoveAt(index);
        for (int i = 0; i < World.Events.Count; i++)
        {
            TimelineEvent timelineEvent = World.Events[i];
            if (timelineEvent.EntryIds.Contains(entryId))
            {
                World.Events[i] = timelineEvent with
                {
                    EntryIds = [.. timelineEvent.EntryIds.Where(id => id != entryId)],
                };
            }
        }

        MarkChanged(systemChanged: false);
    }

    // Owner's choice: when bodies are deleted, entries and events placed on them stay, with
    // their place cleared. Part of the deletion's undo step, so Ctrl+Z brings the places back.
    private void ClearPlacesOn(HashSet<Guid> removedBodies)
    {
        bool IsOnRemoved(LoreLocation? place) =>
            place is not null && removedBodies.Contains(place.BodyId);

        for (int i = 0; i < World.Journal.Count; i++)
        {
            if (IsOnRemoved(World.Journal[i].Location))
            {
                World.Journal[i] = World.Journal[i] with { Location = null };
            }
        }

        for (int i = 0; i < World.Events.Count; i++)
        {
            if (IsOnRemoved(World.Events[i].Location))
            {
                World.Events[i] = World.Events[i] with { Location = null };
            }
        }
    }
}
