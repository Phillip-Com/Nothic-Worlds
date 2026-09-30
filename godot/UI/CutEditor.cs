using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Model;
using NothicWorlds.Interop;
using NothicWorlds.Maps;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The Cut editor (VISION.md MAP-02): a full-screen view of an image where the user draws a
/// rectangle or freeform cut, then adds it to the globe as a map piece. A cut from the main map
/// starts exactly where that part of the map already shows on the globe; a cut from another
/// image starts in the middle of the current view, 30° wide. Esc cancels.
/// </summary>
public partial class CutEditor : CanvasLayer
{
    private const float PanelPadding = 10.0f;
    private const double DefaultWidthDegrees = 30.0;

    private CutCanvas _canvas = null!;
    private Label _title = null!;
    private Button _rectangleTool = null!;
    private Button _freeformTool = null!;
    private Button _addButton = null!;
    private Label _hint = null!;
    private string? _assetName;
    private bool _fromMainMap;
    private bool _opening;  // An image is still loading; ignore further requests to open.

    /// <summary>The open world that pieces are added to.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>Where the outcome is reported. Hidden while the editor is open.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>Raised after a piece is added, with the new piece.</summary>
    public event Action<MapPiece>? PieceAdded;

    /// <summary>True while the editor is open.</summary>
    public bool IsOpen => Visible;

    public override void _Ready()
    {
        Layer = 10;  // Above the toolbar and other overlays.
        Visible = false;

        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.AddThemeStyleboxOverride(
            "panel", new StyleBoxFlat { BgColor = new Color(0.1f, 0.11f, 0.14f) });
        AddChild(panel);

        var margin = new MarginContainer();
        foreach (string side in new[] { "left", "top", "right", "bottom" })
        {
            margin.AddThemeConstantOverride($"margin_{side}", (int)PanelPadding);
        }

        panel.AddChild(margin);
        var layout = new VBoxContainer();
        margin.AddChild(layout);

        _title = new Label();
        layout.AddChild(_title);

        var buttons = new HFlowContainer();
        var tools = new ButtonGroup();
        _rectangleTool = CreateButton("Rectangle", () => SetTool(CutCanvas.CutTool.Rectangle));
        _freeformTool = CreateButton("Freeform", () => SetTool(CutCanvas.CutTool.Freeform));
        foreach (Button tool in new[] { _rectangleTool, _freeformTool })
        {
            tool.ToggleMode = true;
            tool.ButtonGroup = tools;
            buttons.AddChild(tool);
        }

        buttons.AddChild(CreateButton("Fit to View", () => _canvas.FitToView()));
        buttons.AddChild(CreateButton("Cancel", Close));
        _addButton = CreateButton("Add Piece", () => _ = AddPieceAsync());
        buttons.AddChild(_addButton);
        layout.AddChild(buttons);

        _hint = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        layout.AddChild(_hint);

        _canvas = new CutCanvas { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _canvas.CutChanged += UpdateButtons;
        layout.AddChild(_canvas);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (IsOpen && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>
    /// Opens the editor on an image the session knows (the main map, or one added with
    /// <see cref="WorldSession.AddPieceSource"/>). Problems are shown in the toolbar.
    /// </summary>
    public async Task OpenAsync(string assetName, string displayName)
    {
        if (IsOpen || _opening || Session is null || Session.IsBusy)
        {
            return;
        }

        if (Session.Pieces.Count >= SurfaceSettings.MaxPieces)
        {
            Toolbar?.ShowWarning(
                $"This planet already has the most pieces ({SurfaceSettings.MaxPieces}).");
            return;
        }

        Toolbar?.ShowInfo($"Opening {displayName}…", autoHide: false);
        LoadedMap preview;
        double sourceAspectRatio;
        _opening = true;
        Session.IsCutting = true;  // Keeps the world from being replaced while this loads.
        try
        {
            Image source = await Session.GetPieceSourceAsync(assetName);
            sourceAspectRatio = (double)source.GetWidth() / source.GetHeight();
            preview = await MapImageLoader.CreatePreviewAsync(source);
        }
        catch (MapLoadException error)
        {
            Session.IsCutting = false;
            Session.ReleaseDecodedSource();
            Toolbar?.ShowError($"Couldn't open {displayName}: {error.Message}");
            return;
        }
        finally
        {
            _opening = false;
        }

        _assetName = assetName;
        _fromMainMap = assetName == Session.MainMapAssetName;
        _title.Text = $"Cut a Piece from {displayName}";
        _canvas.Load(preview.Texture, sourceAspectRatio);
        _rectangleTool.ButtonPressed = true;
        SetTool(CutCanvas.CutTool.Rectangle);

        SetToolbarVisible(false);
        GetTree().Root.Disable3D = true;  // The globe is hidden; don't spend time drawing it.
        Visible = true;
        _canvas.GrabFocus();
    }

    private async Task AddPieceAsync()
    {
        if (Session is null || _assetName is null || _canvas.Outline is not PieceOutline outline)
        {
            return;
        }

        (GeoCoordinate center, double width) = StartingPlacement(outline);
        _addButton.Disabled = true;
        try
        {
            MapPiece piece = await Session.AddPieceAsync(_assetName, outline, center, width);
            Close();
            Toolbar?.ShowInfo(
                $"Added {piece.Name}. Adjust it in the Pieces panel, and save (Ctrl+S) to " +
                "keep it.");
            PieceAdded?.Invoke(piece);
        }
        catch (Exception error) when (error is MapLoadException or InvalidOperationException)
        {
            UpdateButtons();
            Toolbar?.ShowError($"Couldn't add the piece: {error.Message}");
        }
    }

    // Main-map cuts start right over the part of the map they came from, so nothing appears
    // to move. Anything else (or a cut off the edge of the map) starts facing the camera.
    private (GeoCoordinate Center, double WidthDegrees) StartingPlacement(PieceOutline outline)
    {
        if (_fromMainMap && Session?.MapCheck is MapImageCheck check)
        {
            var placement = PieceStartingPlacement.FromMainMap(
                outline, Session.Projection, check.AspectRatio, Session.Calibration);
            if (placement is not null)
            {
                return placement.Value;
            }
        }

        return (PointFacingCamera(), DefaultWidthDegrees);
    }

    // The point on the planet at the middle of the screen, or the nearest point on its edge if
    // the middle of the screen misses the planet.
    private GeoCoordinate PointFacingCamera()
    {
        if (Session?.Camera is not PlanetCamera camera || Session.Surface is not Node3D planet)
        {
            return new GeoCoordinate(0, 0);
        }

        Vector2 middle = GetViewport().GetVisibleRect().Size / 2;
        return GlobePicker.CoordinateAt(camera, planet, middle, nearestWhenMissed: true)
            ?? new GeoCoordinate(0, 0);
    }

    private void SetTool(CutCanvas.CutTool tool)
    {
        _canvas.Tool = tool;
        _hint.Text = tool == CutCanvas.CutTool.Rectangle
            ? "Drag a box around the part you want. Scroll to zoom, right-drag to pan."
            : "Click to place points around the part you want, then click the first point (or " +
              "press Enter) to close the shape. Backspace removes the last point. Scroll to " +
              "zoom, right-drag to pan.";
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        _addButton.Disabled = _canvas.Outline is null;
    }

    private void Close()
    {
        Visible = false;
        _assetName = null;
        _canvas.Clear();
        if (Session is not null)
        {
            Session.IsCutting = false;
            Session.ReleaseDecodedSource();  // Can be hundreds of MB.
        }

        GetTree().Root.Disable3D = false;
        SetToolbarVisible(true);
    }

    private void SetToolbarVisible(bool visible)
    {
        if (Toolbar is not null)
        {
            Toolbar.Visible = visible;
        }
    }

    private static Button CreateButton(string text, Action pressed)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.Pressed += pressed;
        return button;
    }
}
