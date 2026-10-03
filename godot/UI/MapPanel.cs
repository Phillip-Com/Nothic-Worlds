using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Model;
using NothicWorlds.Maps;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The Map panel (VISION.md MAP-01 to MAP-05, UI-01), on the right of the screen. At the top,
/// the planet's main map image (<see cref="MapImageSection"/>). Below it, the map pieces (the
/// top of the list is drawn on top): new cuts, and the selected piece's name, position,
/// rotation, and size with exact numbers. The Delete key removes the selected piece (Ctrl+Z
/// brings it back).
/// </summary>
/// <remarks>
/// All edits go through <see cref="WorldSession"/>, so unsaved changes are tracked.
/// </remarks>
public partial class MapPanel : CanvasLayer
{
    private const int ScreenMargin = 12;

    // Below the toolbar row, and above the time bar (like the Journal and Regions panels).
    private const int TopOffset = 56;
    private const int BottomOffset = 130;
    private const float PanelWidth = 320.0f;

    private MapImageSection _mapImage = null!;

    private Label _heading = null!;
    private Button _cutMapButton = null!;
    private Button _cutImageButton = null!;
    private ItemList _list = null!;
    private Control _details = null!;
    private LineEdit _name = null!;
    private SpinBox _latitude = null!;
    private SpinBox _longitude = null!;
    private SpinBox _rotation = null!;
    private SpinBox _width = null!;
    private Button _upButton = null!;
    private Button _editPointsButton = null!;
    private Button _resetPointsButton = null!;
    private Button _downButton = null!;
    private FileDialog _fileDialog = null!;
    private bool _open;

    // The pieces as listed (top of the list = drawn on top), and which one is selected.
    private List<MapPiece> _listed = [];
    private Guid? _selectedId;

    // What the list shows (id and name of each row), to skip rebuilding it when only a piece's
    // position changed, e.g. on every step of a drag.
    private List<(Guid, string)> _shownRows = [];

    /// <summary>The open world whose pieces are shown.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The Calibrate workspace, opened by the Calibrate… button.</summary>
    [Export] public CalibrationWorkspace? Calibration { get; set; }

    /// <summary>The Cut editor, opened to cut new pieces.</summary>
    [Export] public CutEditor? Cutter { get; set; }

    /// <summary>Where messages go. The panel hides whenever the toolbar does.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>Raised when a different piece (or none) is selected.</summary>
    public event Action? SelectionChanged;

    /// <summary>The selected piece's id, or null.</summary>
    public Guid? SelectedPieceId => _selectedId;

    /// <summary>
    /// True while the selected piece shows a handle on every point of its cut (Edit Points),
    /// instead of its move/resize/rotate handles.
    /// </summary>
    public bool IsEditingPoints { get; private set; }

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
        if (Session is null)
        {
            GD.PushError("MapPanel has no world session assigned.");
            return;
        }

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

        // Scrolls when the window is too short for everything (owner: must work small).
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

        _mapImage = new MapImageSection
        {
            Session = Session,
            Calibration = Calibration,
            Toolbar = Toolbar,
        };
        layout.AddChild(_mapImage);
        layout.AddChild(new HSeparator());

        _heading = new Label();
        layout.AddChild(_heading);

        var cutButtons = new HFlowContainer();
        _cutMapButton = CreateButton("Cut from Map…", () => _ = CutFromMainMapAsync());
        _cutMapButton.TooltipText = "Cut a piece out of the main map, to move it independently";
        _cutImageButton = CreateButton("Cut from Image…", () => _fileDialog.PopupCentered());
        _cutImageButton.TooltipText = "Cut a piece out of another image (PNG, JPG, WebP)";
        cutButtons.AddChild(_cutMapButton);
        cutButtons.AddChild(_cutImageButton);
        layout.AddChild(cutButtons);

        _list = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 120),
            FocusMode = Control.FocusModeEnum.None,
        };
        _list.ItemSelected += index => Select(_listed[(int)index].Id);
        layout.AddChild(_list);

        _details = BuildDetails();
        layout.AddChild(_details);

        _fileDialog = new FileDialog
        {
            Title = "Cut a Piece from an Image",
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true,
            Filters = ["*.png, *.jpg, *.jpeg, *.webp ; Images"],
        };
        _fileDialog.FileSelected += path => _ = CutFromFileAsync(path);
        AddChild(_fileDialog);

        if (Toolbar is not null)
        {
            Toolbar.VisibilityChanged += UpdateVisibility;
        }

        if (Cutter is not null)
        {
            Cutter.PieceAdded += piece => Select(piece.Id);
        }

        Session.Changed += SyncWithWorld;
        SyncWithWorld();
        UpdateVisibility();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Text fields get the Delete key first, so it only reaches here when none is being
        // edited.
        if (Visible && _selectedId is not null
            && @event.IsActionPressed(InputActions.DeleteSelection))
        {
            DeleteSelected();
            GetViewport().SetInputAsHandled();
        }
    }

    // Name, position, rotation, and size of the selected piece, plus layer and delete buttons.
    private Control BuildDetails()
    {
        var details = new VBoxContainer();

        _name = new LineEdit { PlaceholderText = "Piece name" };
        _name.TextSubmitted += _ => CommitName();
        _name.FocusExited += CommitName;
        details.AddChild(_name);

        var grid = new GridContainer { Columns = 2 };
        _latitude = AddNumberField(grid, "Latitude", -90, 90, 0.01, "°");
        _longitude = AddNumberField(grid, "Longitude", -180, 180, 0.01, "°");
        _rotation = AddNumberField(grid, "Rotation", 0, 360, 0.1, "°");
        _width = AddNumberField(grid, "Width", PieceProjection.MinimumWidthDegrees,
            PieceProjection.MaximumWidthDegrees, 0.1, "°");
        _longitude.WithWrapAround();  // E.g. 190° becomes -170°.
        _rotation.WithWrapAround();   // E.g. 370° becomes 10°.
        _latitude.TooltipText = "Where the piece's center is: north positive, south negative";
        _longitude.TooltipText = "Where the piece's center is: east positive, west negative";
        _rotation.TooltipText = "Clockwise turn; 0° keeps the top of the piece facing north";
        _width.TooltipText = "How much of the globe the piece spans, left to right";
        details.AddChild(grid);

        var buttons = new HFlowContainer();
        _upButton = CreateButton("Move Up", () => MoveSelected(+1));
        _upButton.TooltipText = "Draw this piece above the one over it";
        _downButton = CreateButton("Move Down", () => MoveSelected(-1));
        _downButton.TooltipText = "Draw this piece below the one under it";
        buttons.AddChild(_upButton);
        buttons.AddChild(_downButton);
        Button delete = CreateButton("Delete", DeleteSelected);
        delete.TooltipText = "Remove this piece (Delete key). Ctrl+Z brings it back.";
        buttons.AddChild(delete);
        details.AddChild(buttons);

        var pointButtons = new HFlowContainer();
        _editPointsButton = new Button
        {
            Text = "Edit Points",
            ToggleMode = true,
            FocusMode = Control.FocusModeEnum.None,
        };
        _editPointsButton.Toggled += SetEditingPoints;
        _editPointsButton.TooltipText =
            "Drag the points of the cut on the globe; the image stretches to follow " +
            "(or double-click the piece). Esc to finish.";
        _resetPointsButton = CreateButton("Reset Points", ResetPoints);
        _resetPointsButton.TooltipText = "Undo all stretching: back to the cut as drawn";
        pointButtons.AddChild(_editPointsButton);
        pointButtons.AddChild(_resetPointsButton);
        details.AddChild(pointButtons);
        return details;
    }

    private SpinBox AddNumberField(
        GridContainer grid, string label, double min, double max, double step, string suffix)
    {
        grid.AddChild(new Label { Text = label });
        var field = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Suffix = suffix,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        field.WithLiveTyping(ShowSelected).ValueChanged += _ => CommitPlacement();
        grid.AddChild(field);
        return field;
    }

    private async Task CutFromMainMapAsync()
    {
        if (Session?.MainMapAssetName is string assetName && Cutter is not null)
        {
            await Cutter.OpenAsync(assetName, "the main map");
        }
    }

    private async Task CutFromFileAsync(string path)
    {
        if (Session is null || Cutter is null)
        {
            return;
        }

        try
        {
            string assetName = Session.AddPieceSource(path);
            await Cutter.OpenAsync(assetName, Path.GetFileName(path));
        }
        catch (MapLoadException error)
        {
            Toolbar?.ShowError($"Couldn't use {Path.GetFileName(path)}: {error.Message}");
        }
    }

    // Rebuilds the list and fields to match the world (after any edit, open, or new world).
    private void SyncWithWorld()
    {
        if (Session is null)
        {
            return;
        }

        _listed = [.. Session.Pieces.Reverse()];
        if (_selectedId is Guid id && _listed.All(p => p.Id != id))
        {
            _selectedId = null;
            SelectionChanged?.Invoke();
        }

        List<(Guid, string)> rows = [.. _listed.Select(p => (p.Id, p.Name))];
        if (!rows.SequenceEqual(_shownRows))
        {
            _list.Clear();
            foreach (MapPiece piece in _listed)
            {
                _list.AddItem(piece.Name);
            }

            _shownRows = rows;
        }

        int selectedRow = _listed.FindIndex(p => p.Id == _selectedId);
        if (selectedRow >= 0)
        {
            _list.Select(selectedRow);
            _list.EnsureCurrentIsVisible();
        }
        else
        {
            _list.DeselectAll();
        }

        _heading.Text = $"Map Pieces ({_listed.Count} of {SurfaceSettings.MaxPieces})";
        bool full = _listed.Count >= SurfaceSettings.MaxPieces;
        _cutMapButton.Disabled = Session.IsBusy || full || Session.MainMapAssetName is null;
        _cutImageButton.Disabled = Session.IsBusy || full || !Session.SelectedBodyHasSurface;
        ShowSelected();
    }

    /// <summary>Turns Edit Points on or off for the selected piece.</summary>
    public void SetEditingPoints(bool editing)
    {
        IsEditingPoints = editing && _selectedId is not null;
        ShowSelected();
    }

    /// <summary>
    /// Imports the map at <paramref name="path"/> onto the selected planet (see
    /// <see cref="MapImageSection.ImportAsync"/>). Never throws.
    /// </summary>
    public Task ImportAsync(string path) => _mapImage.ImportAsync(path);

    /// <summary>Selects a piece (null for none), in the list and on the globe.</summary>
    public void Select(Guid? id)
    {
        if (id == _selectedId)
        {
            return;
        }

        _selectedId = id;
        IsEditingPoints = false;
        SyncWithWorld();
        SelectionChanged?.Invoke();
    }

    // Fills the fields from the selected piece, without triggering edits.
    private void ShowSelected()
    {
        MapPiece? piece = SelectedPiece();
        _details.Visible = piece is not null;
        if (piece is null)
        {
            return;
        }

        if (!_name.HasFocus())
        {
            _name.Text = piece.Name;
        }

        _latitude.ShowValue(piece.Center.LatitudeDegrees);
        _longitude.ShowValue(piece.Center.LongitudeDegrees);
        _rotation.ShowValue(piece.RotationDegrees);
        _width.ShowValue(piece.WidthDegrees);
        _editPointsButton.SetPressedNoSignal(IsEditingPoints);
        _resetPointsButton.Disabled = piece.WarpedPoints is null;
        int position = _listed.IndexOf(piece);
        _upButton.Disabled = position == 0;
        _downButton.Disabled = position == _listed.Count - 1;
    }

    private void CommitPlacement()
    {
        if (SelectedPiece() is MapPiece piece)
        {
            Session?.PlacePiece(
                piece.Id,
                new GeoCoordinate(_latitude.Value, _longitude.Value),
                _rotation.Value,
                _width.Value);
        }
    }

    private void CommitName()
    {
        if (SelectedPiece() is MapPiece piece)
        {
            Session?.RenamePiece(piece.Id, _name.Text);
            _name.Text = piece.Name;  // Shows the old name again if the new one was empty.
        }
    }

    private void MoveSelected(int steps)
    {
        if (SelectedPiece() is MapPiece piece)
        {
            Session?.MovePieceInOrder(piece.Id, steps);
        }
    }

    private void ResetPoints()
    {
        if (SelectedPiece() is MapPiece piece)
        {
            Session?.ResetPiecePoints(piece.Id);
        }
    }

    // No confirmation: deleting can be undone (owner decision).
    private void DeleteSelected()
    {
        if (SelectedPiece() is MapPiece piece && Session is { IsBusy: false })
        {
            Session.RemovePiece(piece.Id);
            Toolbar?.ShowInfo($"Deleted {piece.Name} (Ctrl+Z to undo).");
        }
    }

    private MapPiece? SelectedPiece() => _listed.Find(p => p.Id == _selectedId);

    private void UpdateVisibility()
    {
        Visible = _open && (Toolbar?.Visible ?? true);
    }

    private static Button CreateButton(string text, Action pressed)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.Pressed += pressed;
        return button;
    }
}
