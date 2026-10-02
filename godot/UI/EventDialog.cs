using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The editor for one timeline event (VISION.md LORE-03): its title, timeline, start, optional
/// end (for events that last), place, description, and the journal entries it links to. Dates
/// are entered in the selected body's calendar. Save applies everything as one undo step.
/// </summary>
public partial class EventDialog : ConfirmationDialog
{
    private const string DeleteAction = "delete";
    private const string GoToAction = "go-to";

    private LineEdit _title = null!;
    private OptionButton _timeline = null!;
    private DateFields _start = null!;
    private CheckBox _lasts = null!;
    private DateFields _end = null!;
    private OptionButton _place = null!;
    private TextEdit _description = null!;
    private VBoxContainer _entries = null!;
    private Label _problem = null!;
    private TimelineEvent? _editing;

    /// <summary>The open world. Set it before adding the dialog to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    /// <summary>Glides the clock to a time (standard days), for the Go to button.</summary>
    public Action<double>? GlideTo { get; init; }

    public override void _Ready()
    {
        OkButtonText = "Save";
        DialogHideOnOk = false;
        AddButton("Delete Event", right: false, action: DeleteAction).TooltipText =
            "Delete this event (Ctrl+Z brings it back); its journal entries stay";
        AddButton("Go to", right: false, action: GoToAction).TooltipText =
            "Run the clock to when this event starts";
        Confirmed += Save;
        CustomAction += OnCustomAction;

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(440, 480),
        };
        var layout = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(layout);
        AddChild(scroll);

        var grid = new GridContainer { Columns = 2 };
        _title = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        AddLabelled(grid, "Title", _title);
        _timeline = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        AddLabelled(grid, "Timeline", _timeline);
        _place = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        AddLabelled(grid, "Place", _place);
        layout.AddChild(grid);

        layout.AddChild(new Label { Text = "Starts" });
        _start = new DateFields();
        layout.AddChild(_start);
        _lasts = new CheckBox
        {
            Text = "Lasts until…",
            TooltipText = "For something that goes on for a while: a war, a reign, a journey",
        };
        _lasts.Toggled += on => _end.Visible = on;
        layout.AddChild(_lasts);
        _end = new DateFields { Visible = false };
        layout.AddChild(_end);

        layout.AddChild(new Label { Text = "Description" });
        _description = new TextEdit
        {
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            CustomMinimumSize = new Vector2(0, 90),
        };
        layout.AddChild(_description);

        layout.AddChild(new Label { Text = "Linked journal entries" });
        _entries = new VBoxContainer();
        layout.AddChild(_entries);

        _problem = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1.0f, 0.55f, 0.5f),
        };
        layout.AddChild(_problem);
    }

    /// <summary>Opens the editor on an event, with dates in the selected body's calendar.</summary>
    public void Edit(TimelineEvent timelineEvent)
    {
        _editing = timelineEvent;
        Body body = Session.SelectedBody;
        Title = $"Event (dates on {body.Name})";
        _problem.Text = "";
        _title.Text = timelineEvent.Title;
        ShowTimelines(timelineEvent.TimelineId);
        ShowPlaces(timelineEvent.Location);
        _start.ShowTime(body, timelineEvent.StartDays);
        _lasts.ButtonPressed = timelineEvent.EndDays is not null;
        _end.ShowTime(body, timelineEvent.EndDays ?? timelineEvent.StartDays);
        _end.Visible = _lasts.ButtonPressed;
        _description.Text = timelineEvent.Description;
        ShowEntries(timelineEvent.EntryIds);
        PopupCentered();
    }

    private void Save()
    {
        if (_editing is not TimelineEvent original)
        {
            return;
        }

        var changed = original with
        {
            Title = _title.Text.Trim(),
            TimelineId = Guid.Parse(_timeline.GetItemMetadata(_timeline.Selected).AsString()),
            StartDays = _start.TimeDays,
            EndDays = _lasts.ButtonPressed ? _end.TimeDays : null,
            Location = ChosenPlace(original.Location),
            Description = _description.Text,
            EntryIds = [.. _entries.GetChildren().OfType<CheckBox>()
                .Where(box => box.ButtonPressed)
                .Select(box => Guid.Parse(box.GetMeta("id").AsString()))],
        };
        if (Session.UpdateEvent(changed) is string problem)
        {
            _problem.Text = $"Can't save: {problem}.";
            return;
        }

        Hide();
    }

    private void OnCustomAction(StringName action)
    {
        if (_editing is not TimelineEvent timelineEvent)
        {
            return;
        }

        if (action == DeleteAction)
        {
            Session.DeleteEvent(timelineEvent.Id);
            Hide();
        }
        else if (action == GoToAction)
        {
            GlideTo?.Invoke(_start.TimeDays);
        }
    }

    private void ShowTimelines(Guid current)
    {
        _timeline.Clear();
        foreach (Timeline timeline in Session.World.Timelines)
        {
            _timeline.AddItem(timeline.Hidden ? $"{timeline.Name} (hidden)" : timeline.Name);
            _timeline.SetItemMetadata(_timeline.ItemCount - 1, timeline.Id.ToString());
            if (timeline.Id == current)
            {
                _timeline.Select(_timeline.ItemCount - 1);
            }
        }
    }

    private void ShowPlaces(LoreLocation? current)
    {
        _place.Clear();
        _place.AddItem("Nowhere in particular");
        _place.SetItemMetadata(0, "");
        _place.Select(0);
        foreach (Body body in Session.World.Bodies)
        {
            _place.AddItem(body.Name);
            _place.SetItemMetadata(_place.ItemCount - 1, body.Id.ToString());
            if (body.Id == current?.BodyId)
            {
                _place.Select(_place.ItemCount - 1);
            }
        }
    }

    // The place chosen. A pin is kept while the body stays the same.
    private LoreLocation? ChosenPlace(LoreLocation? current)
    {
        if (_place.Selected <= 0
            || !Guid.TryParse(_place.GetItemMetadata(_place.Selected).AsString(), out Guid body))
        {
            return null;
        }

        return current?.BodyId == body ? current : new LoreLocation(body);
    }

    private void ShowEntries(IReadOnlyList<Guid> linked)
    {
        foreach (Node child in _entries.GetChildren())
        {
            _entries.RemoveChild(child);
            child.QueueFree();
        }

        if (Session.World.Journal.Count == 0)
        {
            _entries.AddChild(new Label { Text = "The journal has no entries yet." });
            return;
        }

        foreach (JournalEntry entry in Session.World.Journal
            .OrderBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase))
        {
            var box = new CheckBox
            {
                Text = entry.Title,
                ButtonPressed = linked.Contains(entry.Id),
            };
            box.SetMeta("id", entry.Id.ToString());
            _entries.AddChild(box);
        }
    }

    private static void AddLabelled(GridContainer grid, string text, Control field)
    {
        grid.AddChild(new Label { Text = text });
        grid.AddChild(field);
    }
}
