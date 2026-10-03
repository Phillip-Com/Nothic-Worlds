using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Interop;
using NothicWorlds.Rendering;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The Regions panel (VISION.md LORE-01), on the right of the screen (one at a time with the
/// Journal and Pieces panels): the selected body's regions, a button to draw a new one, and an
/// editor for the selected region's name, color, notes, and outline (Edit Points), listing the
/// journal entries and events placed in it. Every change applies straight away and can be
/// undone.
/// </summary>
public partial class RegionsPanel : CanvasLayer
{
    private const int ScreenMargin = 12;
    private const int TopOffset = 56;
    private const int BottomOffset = 130;
    private const float PanelWidth = 340.0f;

    private Label _heading = null!;
    private Label _noRegions = null!;
    private ItemList _list = null!;
    private Button _newButton = null!;
    private Button _deleteButton = null!;
    private Control _editor = null!;
    private LineEdit _name = null!;
    private ColorPickerButton _color = null!;
    private TextEdit _notes = null!;
    private Button _editPoints = null!;
    private Label _outline = null!;
    private VBoxContainer _placed = null!;
    private Label _problem = null!;
    private Guid? _selectedId;
    private bool _open;
    private bool _syncing;
    private string _listSignature = "";
    private string _placedSignature = "";

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>
    /// The toolbar: the panel hides whenever it does; it opens the Journal panel.
    /// </summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>Draws and reshapes outlines on the globe.</summary>
    [Export] public RegionEditor? Editor { get; set; }

    /// <summary>Draws the regions; the selected one is highlighted.</summary>
    [Export] public RegionRenderer? Renderer { get; set; }

    /// <summary>The Journal panel, to open an entry placed in a region.</summary>
    [Export] public JournalPanel? Journal { get; set; }

    /// <summary>The timeline strip, whose editor opens an event placed in a region.</summary>
    [Export] public TimelineStrip? Timeline { get; set; }

    /// <summary>Whether the panel is open (it's still hidden while the toolbar is).</summary>
    public bool IsPanelOpen
    {
        get => _open;
        set
        {
            _open = value;
            if (!value)
            {
                Editor?.Stop();
            }

            UpdateVisibility();
            UpdateHighlight();
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
        panel.GrowHorizontal = Control.GrowDirection.Begin;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.09f, 0.09f, 0.11f, 0.96f),
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        });
        AddChild(panel);

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
        _heading = new Label();
        layout.AddChild(_heading);
        _noRegions = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        layout.AddChild(_noRegions);
        _list = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 90),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 0.5f,
            FocusMode = Control.FocusModeEnum.None,
        };
        _list.ItemSelected += index =>
            Select(Guid.Parse(_list.GetItemMetadata((int)index).AsString()));
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
            GD.PushError("RegionsPanel needs a world session.");
            return;
        }

        Session.Changed += Refresh;
        Session.SelectionChanged += () =>
        {
            _selectedId = null;
            Refresh();
        };
        if (Editor is not null)
        {
            Editor.RegionDrawn += Select;
            Editor.Stopped += () => _editPoints.SetPressedNoSignal(false);
        }

        Refresh();
        UpdateVisibility();
    }

    /// <summary>Selects a region and shows it in the editor (e.g. from its pop-up).</summary>
    public void SelectRegion(Guid regionId)
    {
        Select(regionId);
    }

    private Control BuildButtons()
    {
        var row = new HBoxContainer();
        _newButton = CreateButton("New Region", () => Editor?.StartDrawing(),
            "Click corners around the region on the globe");
        row.AddChild(_newButton);
        _deleteButton = CreateButton("Delete", DeleteRegion,
            "Delete the selected region (Ctrl+Z brings it back); what's placed in it stays");
        row.AddChild(_deleteButton);
        return row;
    }

    private Control BuildEditor()
    {
        var editor = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var nameRow = new HBoxContainer();
        _name = new LineEdit
        {
            PlaceholderText = "Name",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _name.TextChanged += _ => Commit();
        nameRow.AddChild(_name);
        _color = new ColorPickerButton
        {
            CustomMinimumSize = new Vector2(36, 0),
            EditAlpha = false,
            TooltipText = "The region's color",
        };
        _color.ColorChanged += _ => Commit();
        nameRow.AddChild(_color);
        editor.AddChild(nameRow);

        var outlineRow = new HBoxContainer();
        _outline = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        outlineRow.AddChild(_outline);
        _editPoints = new Button
        {
            Text = "Edit Points",
            ToggleMode = true,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Drag corners on the globe; drag a middle handle to add one; " +
                "right-click a corner to delete it",
        };
        _editPoints.Toggled += on =>
        {
            if (on && _selectedId is Guid id)
            {
                Editor?.StartEditing(id);
            }
            else
            {
                Editor?.Stop();
            }
        };
        outlineRow.AddChild(_editPoints);
        editor.AddChild(outlineRow);

        _notes = new TextEdit
        {
            PlaceholderText = "Notes…",
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            CustomMinimumSize = new Vector2(0, 90),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _notes.TextChanged += Commit;
        editor.AddChild(_notes);

        editor.AddChild(new Label { Text = "Placed in this region" });
        _placed = new VBoxContainer();
        editor.AddChild(_placed);
        _problem = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1.0f, 0.55f, 0.5f),
        };
        editor.AddChild(_problem);
        return editor;
    }

    private Region? Selected =>
        Session?.World.Regions.FirstOrDefault(r => r.Id == _selectedId);

    private void Select(Guid regionId)
    {
        if (Editor?.EditingRegionId is Guid editing && editing != regionId)
        {
            Editor.Stop();
        }

        _selectedId = regionId;
        _problem.Text = "";
        Refresh();
        ShowRegion(force: true);
    }

    private void DeleteRegion()
    {
        if (Session is not null && _selectedId is Guid id)
        {
            _selectedId = null;
            Session.DeleteRegion(id);
        }
    }

    private void Commit()
    {
        if (_syncing || Session is null || Selected is not Region region)
        {
            return;
        }

        string? problem = Session.UpdateRegion(region with
        {
            Name = _name.Text.Trim(),
            Notes = _notes.Text,
            Color = _color.Color.ToRgbColor(),
        });
        _problem.Text = problem is null ? "" : $"Not saved yet: {problem}.";
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

        Body body = Session.SelectedBody;
        bool canHaveRegions = body.Kind != BodyKind.Star;
        List<Region> regions = [.. Session.World.Regions.Where(r => r.BodyId == body.Id)];
        _heading.Text = $"Regions on {body.Name}";
        _noRegions.Text = !canHaveRegions ? "Stars have no regions."
            : regions.Count == 0 ? "No regions yet: New Region draws one." : "";
        _noRegions.Visible = _noRegions.Text != "";
        _newButton.Disabled = !canHaveRegions;
        ShowList(regions);
        ShowRegion(force: false);
        UpdateHighlight();
    }

    private void ShowList(List<Region> regions)
    {
        string signature = string.Join("|", regions.Select(r => $"{r.Id}:{r.Name}:{r.Color}"))
            + $"#{_selectedId}";
        if (signature == _listSignature)
        {
            return;
        }

        _listSignature = signature;
        _list.Clear();
        foreach (Region region in regions)
        {
            int index = _list.AddItem(region.Name);
            _list.SetItemMetadata(index, region.Id.ToString());
            _list.SetItemCustomFgColor(index, region.Color.ToGodot());
            if (region.Id == _selectedId)
            {
                _list.Select(index);
            }
        }
    }

    // Fills the editor from the selected region, leaving fields being edited alone.
    private void ShowRegion(bool force)
    {
        Region? region = Selected;
        _editor.Visible = region is not null;
        _deleteButton.Disabled = region is null;
        if (region is null)
        {
            return;
        }

        _syncing = true;
        if (force || !_name.HasFocus())
        {
            _name.Text = region.Name;
        }

        if (force || !_notes.HasFocus())
        {
            _notes.Text = region.Notes;
        }

        _color.Color = region.Color.ToGodot();
        _outline.Text = $"{region.Corners.Count} corners";
        _editPoints.SetPressedNoSignal(Editor?.EditingRegionId == region.Id);
        ShowPlaced(region);
        _syncing = false;
    }

    // The entries and events placed in the region, each a button that opens it.
    private void ShowPlaced(Region region)
    {
        (IEnumerable<JournalEntry> entries, IEnumerable<TimelineEvent> events) =
            LoreRules.PlacedIn(Session!.World, region.Id);
        List<JournalEntry> entryList = [.. entries];
        List<TimelineEvent> eventList = [.. events.OrderBy(e => e.StartDays)];
        string signature = string.Join("|", entryList.Select(e => $"{e.Id}{e.Title}"))
            + "#" + string.Join("|", eventList.Select(e => $"{e.Id}{e.Title}{e.StartDays}"))
            + $"#{region.Id}";
        if (signature == _placedSignature)
        {
            return;
        }

        _placedSignature = signature;
        foreach (Node child in _placed.GetChildren())
        {
            _placed.RemoveChild(child);
            child.QueueFree();
        }

        if (entryList.Count == 0 && eventList.Count == 0)
        {
            _placed.AddChild(new Label
            {
                Text = "Nothing yet. Choose this region as the place of a journal entry " +
                    "or an event.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Modulate = new Color(1, 1, 1, 0.6f),
            });
            return;
        }

        Body body = Session.SelectedBody;
        foreach (JournalEntry entry in entryList)
        {
            Guid id = entry.Id;
            _placed.AddChild(CreateButton($"Journal: {entry.Title}", () =>
            {
                Toolbar?.ShowJournal();
                Journal?.SelectEntry(id);
            }, "Open it in the Journal"));
        }

        foreach (TimelineEvent timelineEvent in eventList)
        {
            TimelineEvent opened = timelineEvent;
            string when = BodyClock.Describe(body, timelineEvent.StartDays);
            _placed.AddChild(CreateButton($"Event: {timelineEvent.Title}, {when}",
                () => Timeline?.EditEvent(opened), "Open its editor"));
        }
    }

    private void UpdateHighlight()
    {
        if (Renderer is not null)
        {
            Renderer.HighlightedRegionId = _open ? _selectedId : null;
        }
    }

    private void UpdateVisibility()
    {
        Visible = _open && (Toolbar?.Visible ?? true);
    }

    private static Button CreateButton(string text, Action pressed, string tooltip)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            FocusMode = Control.FocusModeEnum.None,
            Alignment = HorizontalAlignment.Left,
            ClipText = true,
        };
        button.Pressed += pressed;
        return button;
    }
}
