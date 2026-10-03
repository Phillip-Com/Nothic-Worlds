using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The Journal panel (VISION.md LORE-02), on the right of the screen (one at a time with the
/// Map, Terrain, and Regions panels; owner's choice): the world's journal entries, searchable and
/// sortable, and an editor for the selected one: its title, place, text, and the timeline events
/// that link to it. Every change applies as it's typed and can be undone (typing in one entry is
/// one step).
/// </summary>
public partial class JournalPanel : CanvasLayer
{
    private const int ScreenMargin = 12;

    // Starts below the toolbar row, and stops above the time bar.
    private const int TopOffset = 56;
    private const int BottomOffset = 130;
    private const float PanelWidth = 340.0f;

    private Label _heading = null!;
    private LineEdit _search = null!;
    private OptionButton _sort = null!;
    private ItemList _list = null!;
    private Button _deleteButton = null!;
    private Control _editor = null!;
    private LineEdit _title = null!;
    private OptionButton _place = null!;
    private RegionChoice _region = null!;
    private Label _pin = null!;
    private Button _pinButton = null!;
    private Button _unpinButton = null!;
    private TextEdit _text = null!;
    private Label _links = null!;
    private Label _dates = null!;
    private Label _problem = null!;
    private Guid? _selectedId;
    private bool _open;
    private bool _syncing;

    // What the list shows, to skip rebuilding it while nothing it shows has changed.
    private string _listSignature = "";

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The toolbar: the panel hides whenever it does.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>Places pins by clicking on the globe.</summary>
    [Export] public Controls.PinPlacer? Placer { get; set; }

    private enum SortOrder
    {
        RecentlyEdited,
        Title,
        Created,
        Place,
    }

    /// <summary>Whether the panel is open (it's still hidden while the toolbar is).</summary>
    public bool IsPanelOpen
    {
        get => _open;
        set
        {
            _open = value;
            UpdateVisibility();
        }
    }

    public override void _Ready()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(PanelWidth, 0) };
        panel.AnchorLeft = 1;
        panel.AnchorRight = 1;
        panel.AnchorTop = 0;
        panel.AnchorBottom = 1;
        panel.OffsetLeft = -ScreenMargin - PanelWidth;
        panel.OffsetRight = -ScreenMargin;
        panel.OffsetTop = TopOffset;
        panel.OffsetBottom = -BottomOffset;
        panel.GrowHorizontal = Control.GrowDirection.Begin;  // Never off the right edge.

        // Nearly opaque: this panel is for reading and writing, so the view mustn't show
        // through the text.
        panel.AddThemeStyleboxOverride("panel", PanelStyle.SidePanel());
        AddChild(panel);

        // Scrolls when the window is too short for everything, so the panel never runs into
        // the time bar.
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        panel.AddChild(scroll);
        var layout = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        scroll.AddChild(layout);
        _heading = new Label { Text = "Journal" };
        layout.AddChild(_heading);
        layout.AddChild(BuildListControls());
        _list = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 90),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 0.6f,
            FocusMode = Control.FocusModeEnum.None,
        };
        _list.ItemSelected += index => Select(Guid.Parse(_list.GetItemMetadata((int)index)
            .AsString()));
        layout.AddChild(_list);
        layout.AddChild(BuildButtons());
        layout.AddChild(new HSeparator());
        _editor = BuildEditor();
        layout.AddChild(_editor);

        if (Toolbar is not null)
        {
            Toolbar.VisibilityChanged += UpdateVisibility;
        }

        if (Session is null)
        {
            GD.PushError("JournalPanel needs a world session.");
            return;
        }

        Session.Changed += Refresh;
        Session.WorldClosed += _ => _selectedId = null;
        Refresh();
        UpdateVisibility();
    }

    private Control BuildListControls()
    {
        var rows = new VBoxContainer();
        _search = new LineEdit
        {
            PlaceholderText = "Search titles and text",
            ClearButtonEnabled = true,
        };
        _search.TextChanged += _ => Refresh();
        rows.AddChild(_search);
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = "Sort by" });
        _sort = new Dropdown
        {
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _sort.AddItem("Recently edited", (int)SortOrder.RecentlyEdited);
        _sort.AddItem("Title", (int)SortOrder.Title);
        _sort.AddItem("Date written", (int)SortOrder.Created);
        _sort.AddItem("Place", (int)SortOrder.Place);
        _sort.TooltipText = "How the entries are ordered";
        _sort.ItemSelected += _ => Refresh();
        row.AddChild(_sort);
        rows.AddChild(row);
        return rows;
    }

    private Control BuildButtons()
    {
        var row = new HBoxContainer();
        var add = new Button { Text = "New Entry", FocusMode = Control.FocusModeEnum.None };
        add.Pressed += AddEntry;
        row.AddChild(add);
        _deleteButton = new Button
        {
            Text = "Delete",
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Delete the selected entry (Ctrl+Z brings it back)",
        };
        _deleteButton.Pressed += DeleteEntry;
        row.AddChild(_deleteButton);
        return row;
    }

    private Control BuildEditor()
    {
        var editor = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _title = new LineEdit { PlaceholderText = "Title" };
        _title.TextChanged += _ => Commit();
        editor.AddChild(_title);

        var placeRow = new HBoxContainer();
        placeRow.AddChild(new Label { Text = "Place" });
        _place = new Dropdown
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "The planet, moon, or star the entry is about",
        };
        _place.ItemSelected += _ => Commit();
        placeRow.AddChild(_place);
        editor.AddChild(placeRow);
        _region = new RegionChoice();
        _region.Changed += Commit;
        editor.AddChild(_region);
        var pinRow = new HBoxContainer();
        _pin = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        pinRow.AddChild(_pin);
        _pinButton = new Button
        {
            Text = "Pin on Globe…",
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Click the spot on the place's globe (Esc cancels)",
        };
        _pinButton.Pressed += PlacePin;
        pinRow.AddChild(_pinButton);
        _unpinButton = new Button
        {
            Text = "Remove Pin",
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Keep the place, without a spot on it",
        };
        _unpinButton.Pressed += RemovePin;
        pinRow.AddChild(_unpinButton);
        editor.AddChild(pinRow);

        _text = new TextEdit
        {
            PlaceholderText = "Write here…",
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            CustomMinimumSize = new Vector2(0, 90),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _text.TextChanged += Commit;
        editor.AddChild(_text);

        _links = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        editor.AddChild(_links);
        _dates = new Label
        {
            Modulate = new Color(1, 1, 1, 0.6f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        editor.AddChild(_dates);
        _problem = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1.0f, 0.55f, 0.5f),
        };
        editor.AddChild(_problem);
        return editor;
    }

    private void UpdateVisibility()
    {
        Visible = _open && (Toolbar?.Visible ?? true);
    }

    private JournalEntry? Selected =>
        Session?.World.Journal.FirstOrDefault(e => e.Id == _selectedId);

    /// <summary>Selects an entry and shows it in the editor (e.g. from a pin's pop-up).</summary>
    public void SelectEntry(Guid entryId)
    {
        _search.Text = "";  // So it's in the list.
        Select(entryId);
    }

    private void Select(Guid entryId)
    {
        _selectedId = entryId;
        _problem.Text = "";
        Refresh();
        ShowEntry(force: true);
    }

    /// <summary>Adds an entry and selects it, ready to type its title.</summary>
    public void AddEntry()
    {
        if (Session is null)
        {
            return;
        }

        JournalEntry entry = Session.AddJournalEntry();
        _search.Text = "";  // So the new entry shows.
        Select(entry.Id);
        _title.GrabFocus();
        _title.SelectAll();
    }

    private void DeleteEntry()
    {
        if (Session is not null && _selectedId is Guid id)
        {
            _selectedId = null;
            Session.DeleteJournalEntry(id);
        }
    }

    // Applies the editor's title, place, and text to the selected entry.
    private void Commit()
    {
        if (_syncing || Session is null || Selected is not JournalEntry entry)
        {
            return;
        }

        string? problem = Session.UpdateJournalEntry(entry with
        {
            Title = _title.Text.Trim(),
            Text = _text.Text,
            Location = ChosenPlace(entry.Location),
        });
        _problem.Text = problem is null ? "" : $"Not saved yet: {problem}.";
    }

    private Body? PlaceBody(LoreLocation? place)
    {
        return place is null
            ? null
            : Session!.World.Bodies.FirstOrDefault(b => b.Id == place.BodyId);
    }

    // Places a pin for the selected entry on its place's globe.
    private void PlacePin()
    {
        if (Placer is null || Selected is not { Location: LoreLocation place } entry)
        {
            return;
        }

        Guid entryId = entry.Id;
        Placer.Start(place.BodyId, spot =>
        {
            if (Session?.World.Journal.FirstOrDefault(e => e.Id == entryId) is JournalEntry now)
            {
                Report(Session.UpdateJournalEntry(
                    now with { Location = SamePlace(now.Location, place) with { Pin = spot } }));
            }
        });
    }

    // The entry's place now if it's still on the same body (keeping its region), else the one
    // the pin was started for.
    private static LoreLocation SamePlace(LoreLocation? now, LoreLocation started) =>
        now?.BodyId == started.BodyId ? now : started;

    private void RemovePin()
    {
        if (Session is not null && Selected is { Location: LoreLocation place } entry)
        {
            Report(Session.UpdateJournalEntry(
                entry with { Location = place with { Pin = null } }));
        }
    }

    private void Report(string? problem)
    {
        _problem.Text = problem is null ? "" : $"Not saved yet: {problem}.";
    }

    // The place chosen in the dropdown. A pin is kept while the body stays the same.
    private LoreLocation? ChosenPlace(LoreLocation? current)
    {
        if (_place.Selected <= 0
            || !Guid.TryParse(_place.GetItemMetadata(_place.Selected).AsString(), out Guid body))
        {
            return null;
        }

        LoreLocation place = current?.BodyId == body ? current : new LoreLocation(body);
        return _region.Apply(Session!.World, place);
    }

    private void Refresh()
    {
        if (Session is null)
        {
            return;
        }

        if (Selected is null)
        {
            _selectedId = null;
        }

        ShowList();
        ShowEntry(force: false);
    }

    private void ShowList()
    {
        List<JournalEntry> entries = Filtered(Session!.World.Journal);
        _heading.Text = Session.World.Journal.Count == 0
            ? "Journal"
            : $"Journal ({Session.World.Journal.Count})";
        string signature = string.Join("|", entries.Select(e => $"{e.Id}:{ListText(e)}"))
            + $"#{_selectedId}";
        if (signature == _listSignature)
        {
            return;
        }

        _listSignature = signature;
        _list.Clear();
        foreach (JournalEntry entry in entries)
        {
            int index = _list.AddItem(ListText(entry));
            _list.SetItemMetadata(index, entry.Id.ToString());
            _list.SetItemTooltip(index, PlaceName(entry.Location) ?? "Nowhere in particular");
            if (entry.Id == _selectedId)
            {
                _list.Select(index);
            }
        }
    }

    // The entries matching the search, in the chosen order.
    private List<JournalEntry> Filtered(IEnumerable<JournalEntry> entries)
    {
        string search = _search.Text.Trim();
        IEnumerable<JournalEntry> matching = search.Length == 0
            ? entries
            : entries.Where(e => e.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || e.Text.Contains(search, StringComparison.OrdinalIgnoreCase));
        return (SortOrder)_sort.GetSelectedId() switch
        {
            SortOrder.Title =>
                [.. matching.OrderBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)],
            SortOrder.Created => [.. matching.OrderBy(e => e.CreatedUtc)],
            SortOrder.Place => [.. matching
                .OrderBy(e => PlaceName(e.Location) ?? "￿")  // Unplaced entries last.
                .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)],
            _ => [.. matching.OrderByDescending(e => e.EditedUtc)],
        };
    }

    private string ListText(JournalEntry entry)
    {
        return PlaceName(entry.Location) is string place
            ? $"{entry.Title}  ·  {place}"
            : entry.Title;
    }

    private string? PlaceName(LoreLocation? place)
    {
        return place is null
            ? null
            : Session!.World.Bodies.FirstOrDefault(b => b.Id == place.BodyId)?.Name;
    }

    // Fills the editor from the selected entry. Unless forced (a new selection), fields being
    // edited are left alone, so updates from the world never fight the typing.
    private void ShowEntry(bool force)
    {
        JournalEntry? entry = Selected;
        _editor.Visible = entry is not null;
        _deleteButton.Disabled = entry is null;
        if (entry is null)
        {
            return;
        }

        _syncing = true;
        if (force || !_title.HasFocus())
        {
            _title.Text = entry.Title;
        }

        if (force || !_text.HasFocus())
        {
            _text.Text = entry.Text;
        }

        ShowPlaces(entry.Location);
        _region.ShowFor(Session!.World, entry.Location);
        Body? placeBody = PlaceBody(entry.Location);
        _pin.Text = entry.Location?.Pin is { } pin
            ? $"Pinned at {PlaceText.Describe(pin)}"
            : placeBody is null ? "" : "No pin";
        _pinButton.Visible = placeBody is not null;
        _pinButton.Disabled = placeBody?.Kind == BodyKind.Star;
        _pinButton.TooltipText = _pinButton.Disabled
            ? "Stars have no surface to pin"
            : "Click the spot on the place's globe (Esc cancels)";
        _unpinButton.Visible = entry.Location?.Pin is not null;
        _links.Text = LinksText(entry);
        _dates.Text = $"Written {entry.CreatedUtc.ToLocalTime():g}  ·  " +
            $"edited {entry.EditedUtc.ToLocalTime():g}";
        _syncing = false;
    }

    private void ShowPlaces(LoreLocation? current)
    {
        _place.Clear();
        _place.AddItem("Nowhere in particular");
        _place.SetItemMetadata(0, "");
        foreach (Body body in Session!.World.Bodies)
        {
            _place.AddItem(body.Name);
            _place.SetItemMetadata(_place.ItemCount - 1, body.Id.ToString());
            if (body.Id == current?.BodyId)
            {
                _place.Select(_place.ItemCount - 1);
            }
        }

        if (current is null)
        {
            _place.Select(0);
        }
    }

    // The timeline events linking to the entry, with their dates on the selected body.
    private string LinksText(JournalEntry entry)
    {
        List<TimelineEvent> events = [.. LoreRules.EventsLinkedTo(Session!.World, entry.Id)];
        if (events.Count == 0)
        {
            return "No timeline events link here yet.";
        }

        Body body = Session.SelectedBody;
        return "Timeline events linking here:\n" + string.Join("\n", events
            .OrderBy(e => e.StartDays)
            .Select(e => $"•  {e.Title}, {BodyClock.Describe(body, e.StartDays)}"));
    }
}
