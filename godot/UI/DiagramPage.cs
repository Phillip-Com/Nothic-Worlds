using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Model;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The relationship diagrams page (VISION.md LORE-04; owner's choice: a full-window page from
/// the toolbar's Diagrams button). On the left: the world's diagrams (new, rename, delete), the
/// journal entries to add to the one shown (with a button, or by dragging them onto it), the
/// selected box's kind and buttons, Arrange, Save as Image, and whether to show every tie or
/// only those standing at the clock's time. The rest is the
/// <see cref="DiagramCanvas"/>. While it's open the 3D view stops drawing and its camera
/// pauses; the toolbar and the clock stay on top, so the clock can still be run.
/// </summary>
public partial class DiagramPage : CanvasLayer
{
    private const float SideWidth = 250, TopMargin = 64;

    // The longest side a saved picture can have, in pixels: a size every graphics chip can
    // draw.
    private const float MaxPicturePixels = 8_192;
    private const string NoDiagram = "There's no diagram yet: New makes one";
    private const string DeleteTip =
        "Delete this diagram (Ctrl+Z brings it back); its entries and ties stay";
    private const string AddTip = "Put the chosen entries on the diagram, in the middle of the " +
        "view (or drag them onto it, or double-click one)";
    private const string ArrangeTip =
        "Lay the diagram out tidily: a family tree, then the rest below (Ctrl+Z puts it back)";
    private const string SaveImageTip =
        "Save the whole diagram as a PNG picture, to share or print";

    private Control _root = null!;
    private ItemList _diagrams = null!;
    private LineEdit _name = null!;
    private Button _delete = null!;
    private LineEdit _search = null!;
    private ItemList _entries = null!;
    private Button _add = null!;
    private VBoxContainer _selection = null!;
    private Label _selectedTitle = null!;
    private OptionButton _kind = null!;
    private Button _arrange = null!;
    private Button _saveImage = null!;
    private FileDialog _imageDialog = null!;
    private CheckBox _allTimes = null!;
    private DiagramCanvas _canvas = null!;
    private RelationshipDialog _dialog = null!;
    private bool _refreshing;
    private bool _isOpen;
    private ProcessModeEnum _savedCameraMode;
    private readonly HashSet<Node> _hidden = [];

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>
    /// The toolbar, to open an entry in the Journal panel and to close this page.
    /// </summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>The Journal panel, which shows an entry opened from a box.</summary>
    [Export] public JournalPanel? Journal { get; set; }

    /// <summary>
    /// The globe's camera, paused while the page is open (its keys would move it).
    /// </summary>
    [Export] public PlanetCamera? GlobeCamera { get; set; }

    /// <summary>The globe's markers and hints, hidden while the page is open.</summary>
    [Export] public Godot.Collections.Array<Node> HideWhileOpen { get; set; } = [];

    /// <summary>Shows or hides the page (the toolbar's Diagrams button sets it).</summary>
    public bool IsPageOpen
    {
        get => _isOpen;
        set
        {
            if (value == _isOpen || Session is null)
            {
                return;
            }

            _isOpen = value;
            Visible = value;
            Toolbar?.SetHint(this, value
                ? "New makes a diagram. Add journal entries to it from the list, then drag " +
                    "from a box's dot onto another box to link them."
                : null);
            GetViewport().Disable3D = value;
            if (GlobeCamera is not null)
            {
                if (value)
                {
                    _savedCameraMode = GlobeCamera.ProcessMode;
                }

                GlobeCamera.ProcessMode = value ? ProcessModeEnum.Disabled : _savedCameraMode;
            }

            ShowOverlays(!value);

            if (value)
            {
                Refresh();
                _canvas.FitView();
                _canvas.GrabFocus();
            }
        }
    }

    public override void _Ready()
    {
        Visible = false;
        if (Session is null)
        {
            return;
        }

        _root = new Panel { MouseFilter = Control.MouseFilterEnum.Stop };
        // Opaque, so nothing of the globe's overlays shows through.
        _root.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.17f, 0.19f),
        });
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _canvas = new DiagramCanvas { Session = Session };
        _canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _canvas.OffsetLeft = SideWidth;
        _canvas.LinkRequested += (from, to) => _dialog.New(from, to);
        _canvas.RelationshipClicked += relationship => _dialog.Edit(relationship);
        _canvas.EntryOpened += OpenInJournal;
        _canvas.EntriesDropped += AddEntries;
        _canvas.GuiInput += _ => ShowSelection();
        _root.AddChild(_canvas);

        var side = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            OffsetLeft = 10,
            OffsetTop = TopMargin,
            OffsetRight = SideWidth - 8,
            AnchorBottom = 1,
            OffsetBottom = -10,
        };
        _root.AddChild(side);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        side.AddChild(list);
        BuildDiagramList(list);
        list.AddChild(new HSeparator());
        BuildEntryList(list);
        list.AddChild(new HSeparator());
        BuildSelection(list);
        list.AddChild(new HSeparator());
        BuildOptions(list);

        _dialog = new RelationshipDialog { Session = Session };
        AddChild(_dialog);
        Session.Changed += () =>
        {
            if (_isOpen)
            {
                Refresh();
            }
        };
        Session.WorldClosed += _ => _canvas.Show(null);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (_isOpen && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Toolbar?.HideDiagrams();
            GetViewport().SetInputAsHandled();
        }
    }

    // Hides the globe's overlays, remembering which were showing, or puts them back as they
    // were (one switched off by its own toggle stays off).
    private void ShowOverlays(bool show)
    {
        if (!show)
        {
            _hidden.Clear();
        }

        foreach (Node node in HideWhileOpen)
        {
            switch (node)
            {
                case CanvasItem item:
                    if (!show && item.Visible)
                    {
                        _hidden.Add(node);
                    }

                    item.Visible = show && _hidden.Contains(node);
                    break;
                case CanvasLayer layer:
                    if (!show && layer.Visible)
                    {
                        _hidden.Add(node);
                    }

                    layer.Visible = show && _hidden.Contains(node);
                    break;
            }
        }
    }

    private void BuildDiagramList(VBoxContainer list)
    {
        list.AddChild(new Label { Text = "Diagrams" });
        _diagrams = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 120),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _diagrams.ItemSelected += index =>
        {
            if (!_refreshing)
            {
                _canvas.Show(DiagramIdAt((int)index));
                Refresh();
            }
        };
        list.AddChild(_diagrams);

        var buttons = new HBoxContainer();
        var add = new Button { Text = "New", TooltipText = "Make a new, empty diagram" };
        add.Pressed += () =>
        {
            LoreDiagram diagram = Session!.AddDiagram();
            _canvas.Show(diagram.Id);
            Refresh();
            _name.GrabFocus();
            _name.SelectAll();
        };
        buttons.AddChild(add);
        _delete = new Button
        {
            Text = "Delete",
            TooltipText = DeleteTip,
        };
        _delete.Pressed += () =>
        {
            if (_canvas.DiagramId is Guid id)
            {
                Session!.DeleteDiagram(id);
            }
        };
        buttons.AddChild(_delete);
        list.AddChild(buttons);

        _name = new LineEdit
        {
            PlaceholderText = "Diagram name",
            MaxLength = LoreDiagram.MaxNameLength,
        };
        _name.TextChanged += text =>
        {
            if (!_refreshing && _canvas.DiagramId is Guid id && text.Trim().Length > 0)
            {
                Session!.RenameDiagram(id, text.Trim());
            }
        };
        list.AddChild(_name);
    }

    private void BuildEntryList(VBoxContainer list)
    {
        list.AddChild(new Label { Text = "Add journal entries" });
        _search = new LineEdit { PlaceholderText = "Search titles", ClearButtonEnabled = true };
        _search.TextChanged += _ => ShowEntries();
        list.AddChild(_search);
        _entries = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 160),
            SelectMode = ItemList.SelectModeEnum.Multi,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _entries.ItemActivated += _ => AddSelectedEntries();
        _entries.SetDragForwarding(Callable.From<Vector2, Variant>(DragEntries),
            Callable.From<Vector2, Variant, bool>((_, _) => false),
            Callable.From<Vector2, Variant>((_, _) => { }));
        list.AddChild(_entries);
        _add = new Button
        {
            Text = "Add to Diagram",
            TooltipText = AddTip,
        };
        _add.Pressed += AddSelectedEntries;
        list.AddChild(_add);
    }

    private void BuildSelection(VBoxContainer list)
    {
        _selection = new VBoxContainer();
        list.AddChild(_selection);
        _selectedTitle = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(SideWidth - 30, 0),
        };
        _selection.AddChild(_selectedTitle);
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = "Kind" });
        _kind = new Dropdown { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _kind.AddItem("Plain writing");
        foreach (LoreKind kind in LoreWords.EntryKinds)
        {
            _kind.AddItem(LoreWords.Kind(kind));
        }

        _kind.ItemSelected += index => SetSelectedKind((int)index);
        row.AddChild(_kind);
        _selection.AddChild(row);
        var open = new Button { Text = "Open in Journal", TooltipText = "Read or edit the entry" };
        open.Pressed += () =>
        {
            if (_canvas.SelectedEntry is Guid entry)
            {
                OpenInJournal(entry);
            }
        };
        _selection.AddChild(open);
        var remove = new Button
        {
            Text = "Take off Diagram",
            TooltipText = "Remove the box (Delete key); the entry and its ties stay",
        };
        remove.Pressed += () =>
        {
            if (_canvas.DiagramId is Guid diagram && _canvas.SelectedEntry is Guid entry)
            {
                Session!.RemoveFromDiagram(diagram, entry);
            }
        };
        _selection.AddChild(remove);
    }

    private void BuildOptions(VBoxContainer list)
    {
        _arrange = new Button
        {
            Text = "Arrange",
            TooltipText = ArrangeTip,
        };
        _arrange.Pressed += () =>
        {
            if (_canvas.DiagramId is Guid id)
            {
                Session!.ArrangeDiagram(id);
                _canvas.FitView();
            }
        };
        list.AddChild(_arrange);
        _saveImage = new Button
        {
            Text = "Save as Image…",
            TooltipText = SaveImageTip,
        };
        _saveImage.Pressed += AskWhereToSaveImage;
        list.AddChild(_saveImage);
        _imageDialog = new FileDialog
        {
            Title = "Save Diagram as Image",
            FileMode = FileDialog.FileModeEnum.SaveFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true,
            Filters = ["*.png ; PNG images"],
        };
        _imageDialog.FileSelected += path => _ = SaveImageAsync(path);
        AddChild(_imageDialog);
        _allTimes = new CheckBox
        {
            Text = "Show every tie",
            TooltipText = "Show ties whatever their dates. Off: only ties that stand at the " +
                "clock's time, with ended ones faded",
        };
        _allTimes.Toggled += on =>
        {
            _canvas.ShowAllTimes = on;
            _canvas.QueueRedraw();
        };
        list.AddChild(_allTimes);
        list.AddChild(new Label
        {
            Text = "Drag a box to move it. Drag from its dot onto another box to link them. " +
                "Double-click a box to open it, click a line to edit it. Drag the background " +
                "to pan; the wheel zooms. Esc goes back.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(SideWidth - 30, 0),
            Modulate = new Color(1, 1, 1, 0.6f),
        });
    }

    // Shows the world's diagrams, keeping the one shown (or the first, if it's gone).
    private void Refresh()
    {
        _refreshing = true;
        List<LoreDiagram> diagrams = Session!.World.Diagrams;
        if (_canvas.DiagramId is not Guid shown || !diagrams.Any(d => d.Id == shown))
        {
            _canvas.Show(diagrams.Count == 0 ? null : diagrams[0].Id);
        }

        _diagrams.Clear();
        foreach (LoreDiagram diagram in diagrams)
        {
            _diagrams.AddItem(diagram.Name);
            if (diagram.Id == _canvas.DiagramId)
            {
                _diagrams.Select(_diagrams.ItemCount - 1);
            }
        }

        LoreDiagram? current = diagrams.FirstOrDefault(d => d.Id == _canvas.DiagramId);
        if (current is not null && !_name.HasFocus())
        {
            _name.Text = current.Name;
        }

        string? noDiagram = current is null ? NoDiagram : null;
        DisabledTip.Apply(_delete, DeleteTip, noDiagram);
        _name.Editable = current is not null;
        string? empty = noDiagram
            ?? (current!.Placements.Count == 0 ? "The diagram is empty: add entries first" : null);
        DisabledTip.Apply(_arrange, ArrangeTip, empty);
        DisabledTip.Apply(_saveImage, SaveImageTip, empty);
        _refreshing = false;
        ShowEntries();
        ShowSelection();
        _canvas.QueueRedraw();
    }

    // The journal entries not yet on the diagram, matching the search.
    private void ShowEntries()
    {
        _entries.Clear();
        LoreDiagram? diagram = CurrentDiagram();
        DisabledTip.Apply(_add, AddTip, diagram is null ? NoDiagram : null);
        if (diagram is null)
        {
            return;
        }

        var shown = diagram.Placements.Select(p => p.EntryId).ToHashSet();
        string search = _search.Text.Trim();
        foreach (JournalEntry entry in Session!.World.Journal
            .Where(e => !shown.Contains(e.Id))
            .Where(e => search.Length == 0
                || e.Title.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase))
        {
            string kind = entry.Kind is LoreKind k ? $"  ({LoreWords.Kind(k)})" : "";
            _entries.AddItem(entry.Title + kind);
            _entries.SetItemMetadata(_entries.ItemCount - 1, entry.Id.ToString());
        }
    }

    // Puts the chosen entries in a row in the middle of the view.
    private void AddSelectedEntries() => AddEntries(ChosenEntries(), _canvas.MiddleSpot());

    // The entries chosen in the list. Read before adding any: each addition refreshes the
    // list, which renumbers it.
    private Guid[] ChosenEntries() => [.. _entries.GetSelectedItems()
        .Select(index => Guid.Parse(_entries.GetItemMetadata(index).AsString()))];

    // Dragging from the list carries the chosen entries (or the one under the mouse, if it
    // isn't chosen), with their titles shown under the mouse.
    private Variant DragEntries(Vector2 at)
    {
        int under = _entries.GetItemAtPosition(at, exact: true);
        if (under < 0)
        {
            return default;
        }

        if (!_entries.IsSelected(under))
        {
            _entries.Select(under);
        }

        Guid[] chosen = ChosenEntries();
        var titles = new Label
        {
            Text = chosen.Length == 1 ? _entries.GetItemText(under) : $"{chosen.Length} entries",
        };
        _entries.SetDragPreview(titles);
        return new Godot.Collections.Array([.. chosen.Select(id => Variant.From(id.ToString()))]);
    }

    // Puts entries in a row around a diagram spot, as one undo step.
    private void AddEntries(Guid[] chosen, Vector2 middle)
    {
        if (CurrentDiagram() is not LoreDiagram diagram || chosen.Length == 0)
        {
            return;
        }

        Session!.BeginGesture("Add to Diagram");
        for (int i = 0; i < chosen.Length; i++)
        {
            Guid entry = chosen[i];
            double x = middle.X + (i - (chosen.Length - 1) / 2.0) * DiagramLayout.ColumnSpacing;
            if (Session.PlaceOnDiagram(diagram.Id, entry, Math.Round(x), Math.Round(middle.Y))
                is string problem)
            {
                Toolbar?.ShowError($"Can't add to the diagram: {problem}.");
                break;
            }
        }

        Session.EndGesture();
    }

    private void ShowSelection()
    {
        JournalEntry? entry = Session!.World.Journal
            .FirstOrDefault(e => e.Id == _canvas.SelectedEntry);
        _selection.Visible = entry is not null;
        if (entry is null)
        {
            return;
        }

        _selectedTitle.Text = entry.Title;
        _kind.Select(entry.Kind is LoreKind kind
            ? Array.IndexOf(LoreWords.EntryKinds, kind) + 1
            : 0);
    }

    private void SetSelectedKind(int index)
    {
        if (Session!.World.Journal.FirstOrDefault(e => e.Id == _canvas.SelectedEntry)
            is not JournalEntry entry)
        {
            return;
        }

        LoreKind? kind = index == 0 ? null : LoreWords.EntryKinds[index - 1];
        Session.UpdateJournalEntry(entry with { Kind = kind });
    }

    private void AskWhereToSaveImage()
    {
        if (CurrentDiagram() is not LoreDiagram diagram)
        {
            return;
        }

        _imageDialog.CurrentDir = OS.GetSystemDir(OS.SystemDir.Pictures);
        _imageDialog.CurrentFile = $"{SafeFileName(diagram.Name)}.png";
        _imageDialog.PopupCentered();
    }

    // Draws the whole diagram into a picture off screen (owner's choice: every box, fitted with
    // a margin, at twice normal detail, on the page's background, titled) and saves it.
    private async Task SaveImageAsync(string path)
    {
        if (CurrentDiagram() is not { Placements.Count: > 0 } diagram)
        {
            return;
        }

        // Twice normal detail, or less if that would make a picture too big to save.
        float zoom = 2;
        (Vector2 size, Vector2 pan) = DiagramCanvas.PictureOf(diagram, zoom);
        float fit = Math.Min(1, MaxPicturePixels / Math.Max(size.X, size.Y));
        if (fit < 1)
        {
            zoom *= fit;
            (size, pan) = DiagramCanvas.PictureOf(diagram, zoom);
        }

        var picture = new SubViewport
        {
            Size = new Vector2I((int)Math.Ceiling(size.X), (int)Math.Ceiling(size.Y)),
            Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
        };
        var canvas = new DiagramCanvas
        {
            Session = Session!,
            ForExport = true,
            Title = diagram.Name,
            ShowAllTimes = _canvas.ShowAllTimes,
            Size = size,
        };
        picture.AddChild(canvas);
        AddChild(picture);
        canvas.ShowAt(diagram.Id, zoom, pan);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Error result = picture.GetTexture().GetImage().SavePng(path);
        picture.QueueFree();
        if (result == Error.Ok)
        {
            Toolbar?.ShowInfo($"Saved {System.IO.Path.GetFileName(path)}.");
        }
        else
        {
            Toolbar?.ShowError($"Couldn't save the picture ({result}).");
        }
    }

    // A diagram's name as a file name: characters files can't have become dashes.
    private static string SafeFileName(string name)
    {
        char[] bad = System.IO.Path.GetInvalidFileNameChars();
        string safe = new([.. name.Select(c => bad.Contains(c) ? '-' : c)]);
        return safe.Trim().Length == 0 ? "Diagram" : safe.Trim();
    }

    // Goes back to the globe with the entry open in the Journal panel.
    private void OpenInJournal(Guid entryId)
    {
        Toolbar?.ShowJournal();  // Closes this page: they share the right-hand buttons
        Journal?.SelectEntry(entryId);
    }

    private LoreDiagram? CurrentDiagram() =>
        Session!.World.Diagrams.FirstOrDefault(d => d.Id == _canvas.DiagramId);

    private Guid? DiagramIdAt(int index) =>
        index >= 0 && index < Session!.World.Diagrams.Count
            ? Session.World.Diagrams[index].Id
            : null;
}
