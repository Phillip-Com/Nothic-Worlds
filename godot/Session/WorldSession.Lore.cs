using NothicWorlds.Core.Model;

namespace NothicWorlds.Session;

// The lore part of the open world (VISION.md LORE-02, LORE-03): adding, editing, and deleting
// journal entries, timelines, and events, all undoable. Kept in its own file to keep
// WorldSession.cs manageable.
public partial class WorldSession
{
    // Lane colors offered to new timelines, in turn: soft, and distinct on the dark strip.
    private static readonly RgbColor[] _timelineColors =
    [
        new(0x8C, 0xB4, 0xE6),  // Blue
        new(0xE6, 0x96, 0x78),  // Coral
        new(0x8C, 0xD2, 0x8C),  // Green
        new(0xDC, 0xB4, 0x5A),  // Gold
        new(0xB9, 0x96, 0xE1),  // Violet
        new(0x6E, 0xCD, 0xCD),  // Teal
    ];

    /// <summary>
    /// Adds a timeline at the bottom of the strip, with the next color, and returns it.
    /// </summary>
    public Timeline AddTimeline()
    {
        RecordUndo("Add Timeline");
        Timeline timeline = NewTimeline($"Timeline {World.Timelines.Count + 1}");
        MarkChanged(systemChanged: false);
        return timeline;
    }

    /// <summary>
    /// Replaces a timeline's name, color, or hidden flag (it's found by ID). Rapid changes to
    /// the same timeline (typing its name) are one undo step.
    /// </summary>
    /// <returns>What's wrong with the change (nothing is changed then), or null.</returns>
    public string? UpdateTimeline(Timeline changed)
    {
        int index = World.Timelines.FindIndex(t => t.Id == changed.Id);
        if (index < 0 || World.Timelines[index] == changed)
        {
            return null;
        }

        if (changed.Problem() is string problem)
        {
            return problem;
        }

        RecordUndo($"Edit {World.Timelines[index].Name}", mergeKey: ("timeline", changed.Id));
        World.Timelines[index] = changed;
        MarkChanged(systemChanged: false);
        return null;
    }

    /// <summary>Moves a timeline's lane up (negative) or down (positive) the strip.</summary>
    public void MoveTimeline(Guid timelineId, int steps)
    {
        int index = World.Timelines.FindIndex(t => t.Id == timelineId);
        int target = Math.Clamp(index + steps, 0, World.Timelines.Count - 1);
        if (index < 0 || target == index)
        {
            return;
        }

        RecordUndo($"Move {World.Timelines[index].Name}");
        Timeline timeline = World.Timelines[index];
        World.Timelines.RemoveAt(index);
        World.Timelines.Insert(target, timeline);
        MarkChanged(systemChanged: false);
    }

    /// <summary>
    /// Deletes a timeline and every event on it, as one undo step (owner's choice). Journal
    /// entries the events linked to stay.
    /// </summary>
    public void DeleteTimeline(Guid timelineId)
    {
        int index = World.Timelines.FindIndex(t => t.Id == timelineId);
        if (index < 0)
        {
            return;
        }

        RecordUndo($"Delete {World.Timelines[index].Name}");
        World.Timelines.RemoveAt(index);
        World.Events.RemoveAll(e => e.TimelineId == timelineId);
        MarkChanged(systemChanged: false);
    }

    /// <summary>
    /// Adds an event at a time, on the first shown timeline (making a "History" timeline first
    /// if there are none, in the same undo step), and returns it.
    /// </summary>
    public TimelineEvent AddEvent(double startDays)
    {
        RecordUndo("Add Event");
        Timeline timeline = World.Timelines.FirstOrDefault(t => !t.Hidden)
            ?? World.Timelines.FirstOrDefault()
            ?? NewTimeline("History");
        var timelineEvent = new TimelineEvent
        {
            TimelineId = timeline.Id,
            Title = "New event",
            StartDays = startDays,
        };
        World.Events.Add(timelineEvent);
        MarkChanged(systemChanged: false);
        return timelineEvent;
    }

    /// <summary>Replaces an event (it's found by ID) with everything checked first.</summary>
    /// <returns>What's wrong with the change (nothing is changed then), or null.</returns>
    public string? UpdateEvent(TimelineEvent changed)
    {
        int index = World.Events.FindIndex(e => e.Id == changed.Id);
        if (index < 0 || World.Events[index] == changed)
        {
            return null;
        }

        if (changed.Problem() is string problem)
        {
            return problem;
        }

        if (World.Timelines.All(t => t.Id != changed.TimelineId))
        {
            return "that timeline no longer exists";
        }

        if (changed.EntryIds.Any(id => World.Journal.All(e => e.Id != id)))
        {
            return "a linked journal entry no longer exists";
        }

        if (changed.Location is LoreLocation place && FindBody(place.BodyId) is null)
        {
            return "that body no longer exists";
        }

        RecordUndo($"Edit {World.Events[index].Title}");
        World.Events[index] = changed;
        MarkChanged(systemChanged: false);
        return null;
    }

    /// <summary>Deletes an event (its journal entries stay).</summary>
    public void DeleteEvent(Guid eventId)
    {
        int index = World.Events.FindIndex(e => e.Id == eventId);
        if (index < 0)
        {
            return;
        }

        RecordUndo($"Delete {World.Events[index].Title}");
        World.Events.RemoveAt(index);
        MarkChanged(systemChanged: false);
    }

    // Adds a timeline with the next color in turn (inside an edit that recorded undo).
    private Timeline NewTimeline(string name)
    {
        var timeline = new Timeline
        {
            Name = name,
            Color = _timelineColors[World.Timelines.Count % _timelineColors.Length],
        };
        World.Timelines.Add(timeline);
        return timeline;
    }
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

    // Owner's choices: when bodies are deleted, their regions and weather pins go too (like
    // their map), and entries and events placed on them stay, with their place cleared. Part
    // of the deletion's undo step, so Ctrl+Z brings it all back.
    private void ClearLoreOn(HashSet<Guid> removedBodies)
    {
        World.Regions.RemoveAll(r => removedBodies.Contains(r.BodyId));
        World.WeatherPins.RemoveAll(p => removedBodies.Contains(p.BodyId));

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
