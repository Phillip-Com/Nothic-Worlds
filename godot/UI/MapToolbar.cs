using Godot;
using NothicWorlds.Core.Maps;
using NothicWorlds.Interop;
using NothicWorlds.Maps;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// Top-left toolbar (VISION.md MAP-01 to MAP-05, UI-02) with a slot for the File and Edit
/// menus, "System…", "Import Map…", "Clear Map", "Calibrate…", "Pieces…", a "Map type"
/// dropdown, and a "Fill color" picker (for map types that don't cover the whole globe), plus a
/// message line underneath that other parts of the app can use too. Success messages fade after
/// a few seconds. Warnings and errors stay until the next message.
/// </summary>
/// <remarks>
/// All edits go through <see cref="WorldSession"/>, so unsaved changes are tracked.
/// </remarks>
public partial class MapToolbar : CanvasLayer
{
    private const int ScreenMargin = 12;
    private const double InfoMessageSeconds = 6.0;
    private const float MessageWidth = 460.0f;

    private static readonly Color _infoColor = new(0.92f, 0.94f, 0.98f);
    private static readonly Color _warningColor = new(1.0f, 0.8f, 0.35f);
    private static readonly Color _errorColor = new(1.0f, 0.5f, 0.45f);

    private Button _importButton = null!;
    private Button _clearButton = null!;
    private Button _calibrateButton = null!;
    private OptionButton _mapType = null!;
    private ColorPickerButton _fillColor = null!;
    private Control _fillColorControls = null!;
    private Label _message = null!;
    private FileDialog _fileDialog = null!;

    // The imported file's name, for messages. Unknown for maps opened from a world file.
    private string? _mapFileName;

    // Increases with every message, so an old auto-hide timer doesn't hide a newer message.
    private int _messageVersion;

    /// <summary>The open world that maps are applied to.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The Calibrate workspace, opened by the Calibrate… button.</summary>
    [Export] public CalibrationWorkspace? Calibration { get; set; }

    /// <summary>The System panel, shown and hidden by the System… button.</summary>
    [Export] public SystemPanel? SystemPanel { get; set; }

    /// <summary>The Pieces panel, shown and hidden by the Pieces… button.</summary>
    [Export] public PiecesPanel? Pieces { get; set; }

    /// <summary>
    /// The Journal panel, shown and hidden by the Journal… button. It shares the right side
    /// with the Pieces panel, one at a time (owner's choice).
    /// </summary>
    [Export] public JournalPanel? Journal { get; set; }

    /// <summary>The timeline strip, shown and hidden by the Timeline… button.</summary>
    [Export] public TimelineStrip? Timeline { get; set; }

    /// <summary>Space at the start of the toolbar row, where the File menu goes.</summary>
    public HBoxContainer MenuArea { get; } = new();

    private enum MessageKind
    {
        Info,
        Warning,
        Error,
    }

    public override void _Ready()
    {
        var layout = new VBoxContainer { Position = new Vector2(ScreenMargin, ScreenMargin) };
        AddChild(layout);

        var controls = new HBoxContainer();
        layout.AddChild(controls);
        controls.AddChild(MenuArea);

        var systemButton = CreateButton(
            "System…", "The star system: add, delete, and edit suns, planets, and moons");
        systemButton.ToggleMode = true;
        systemButton.Toggled += open =>
        {
            if (SystemPanel is not null)
            {
                SystemPanel.IsPanelOpen = open;
            }
        };
        controls.AddChild(systemButton);

        _importButton = CreateButton(
            "Import Map…", "Wrap a map image (PNG, JPG, WebP) onto the planet");
        _importButton.Pressed += () => _fileDialog.PopupCentered();
        controls.AddChild(_importButton);

        _clearButton = CreateButton("Clear Map", "Remove the map and show the grid");
        _clearButton.Pressed += () => Session?.ClearMap();
        controls.AddChild(_clearButton);

        _calibrateButton = CreateButton(
            "Calibrate…",
            "Line the map up with the globe by dragging its latitude/longitude lines");
        _calibrateButton.Pressed += () => Calibration?.Open();
        controls.AddChild(_calibrateButton);

        var piecesButton = CreateButton(
            "Pieces…", "Cut pieces from the map or other images and place them on the globe");
        var journalButton = CreateButton(
            "Journal…", "The world's journal: write entries about places and history");
        piecesButton.ToggleMode = true;
        journalButton.ToggleMode = true;
        piecesButton.Toggled += open =>
        {
            if (Pieces is not null)
            {
                Pieces.IsPanelOpen = open;
            }

            if (open)
            {
                journalButton.ButtonPressed = false;  // One panel on the right at a time.
            }
        };
        journalButton.Toggled += open =>
        {
            if (Journal is not null)
            {
                Journal.IsPanelOpen = open;
            }

            if (open)
            {
                piecesButton.ButtonPressed = false;
            }
        };
        controls.AddChild(piecesButton);
        controls.AddChild(journalButton);

        var timelineButton = CreateButton(
            "Timeline…", "The timeline strip: your world's history, as lanes of events");
        timelineButton.ToggleMode = true;
        timelineButton.Toggled += open =>
        {
            if (Timeline is not null)
            {
                Timeline.IsStripOpen = open;
            }
        };
        controls.AddChild(timelineButton);

        controls.AddChild(CreateLabel("  Map type:"));
        _mapType = CreateMapTypeDropdown();
        controls.AddChild(_mapType);

        _fillColorControls = new HBoxContainer();
        _fillColorControls.AddChild(CreateLabel("  Fill color:"));
        _fillColor = CreateFillColorPicker();
        _fillColorControls.AddChild(_fillColor);
        controls.AddChild(_fillColorControls);

        _message = CreateLabel("");
        _message.Visible = false;
        _message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _message.CustomMinimumSize = new Vector2(MessageWidth, 0);
        layout.AddChild(_message);

        _fileDialog = new FileDialog
        {
            Title = "Import Map Image",
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true,
            Filters = ["*.png, *.jpg, *.jpeg, *.webp ; Map images"],
        };
        _fileDialog.FileSelected += path => _ = ImportAsync(path);
        AddChild(_fileDialog);

        if (Session is null)
        {
            GD.PushError("MapToolbar has no world session assigned.");
            _importButton.Disabled = true;
            _mapType.Disabled = true;
            return;
        }

        Session.Changed += SyncWithWorld;
        Session.WorldClosed += _ => _mapFileName = null;
        SyncWithWorld();
    }

    /// <summary>Shows an informational message that fades after a few seconds.</summary>
    public void ShowInfo(string text, bool autoHide = true)
    {
        ShowMessage(text, MessageKind.Info, autoHide);
    }

    /// <summary>Shows a warning that stays until the next message.</summary>
    public void ShowWarning(string text)
    {
        ShowMessage(text, MessageKind.Warning);
    }

    /// <summary>Shows an error that stays until the next message.</summary>
    public void ShowError(string text)
    {
        ShowMessage(text, MessageKind.Error);
    }

    /// <summary>
    /// Imports the map at <paramref name="path"/> and applies it to the planet, showing progress
    /// and the outcome in the message line. Never throws. Problems are shown to the user.
    /// </summary>
    public async Task ImportAsync(string path)
    {
        if (Session is null || Session.IsBusy)
        {
            return;
        }

        string fileName = Path.GetFileName(path);
        ShowInfo($"Loading {fileName}…", autoHide: false);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await Session.ImportMapAsync(path);
            GD.Print($"Map imported: {fileName} in {stopwatch.Elapsed.TotalSeconds:0.00} s");
            _mapFileName = fileName;
            DescribeMap("Map applied");
        }
        catch (MapLoadException error)
        {
            ShowError($"Couldn't import {fileName}: {error.Message}");
        }
        catch (Exception error)
        {
            GD.PushError($"Unexpected error importing {path}: {error}");
            ShowError(
                $"Couldn't import {fileName} because of an unexpected error. " +
                "Details are in the log.");
        }
    }

    // Updates the controls to match the open world, e.g. after opening a file.
    private void SyncWithWorld()
    {
        if (Session is null)
        {
            return;
        }

        _mapType.Select(_mapType.GetItemIndex((int)Session.Projection));
        _fillColor.Color = Session.FillColor.ToGodot();
        bool hasSurface = Session.SelectedBodyHasSurface;  // Stars have no map.
        _fillColorControls.Visible =
            hasSurface && !MapProjections.CoversWholeGlobe(Session.Projection);
        _importButton.Disabled = Session.IsBusy || !hasSurface;
        _mapType.Disabled = !hasSurface;
        _clearButton.Disabled = Session.IsBusy || Session.MapCheck is null;
        _calibrateButton.Disabled = Session.IsBusy || Session.MapCheck is null;
    }

    private void OnMapTypeSelected(long index)
    {
        if (Session is null)
        {
            return;
        }

        Session.SetProjection((MapProjection)_mapType.GetItemId((int)index));
        if (Session.MapCheck is not null)
        {
            DescribeMap("Now showing");
        }
    }

    // Explains how the current map is shown. `lead` starts the sentence ("Map applied", ...).
    private void DescribeMap(string lead)
    {
        if (Session?.MapCheck is not MapImageCheck check)
        {
            return;
        }

        MapProjection type = Session.Projection;
        string resizeNote = check.NeedsResize
            ? $" It was shrunk from {check.OriginalWidth} × {check.OriginalHeight} to fit the " +
              $"{MapImageRules.MaxWidth} × {MapImageRules.MaxHeight} size limit."
            : "";
        string description = $"{lead}: {_mapFileName ?? "the map"} ({check.TargetWidth} × " +
            $"{check.TargetHeight}) as a {DescriptiveName(type)}{CoverageNote(type, check)}";

        if (MapProjections.ShapeMatches(type, check.AspectRatio))
        {
            ShowInfo($"{description}{resizeNote} Press G to show the grid.");
            return;
        }

        double expected = MapProjections.ExpectedAspectRatio(type)!.Value;
        string advice = type == MapProjection.Equirectangular
            ? " Use a 2:1 equirectangular map, or switch Map type to Flat map."
            : " Check that the map was drawn in this layout.";
        ShowWarning(
            $"{description} But its shape is {check.AspectRatio:0.##}:1, and this map type " +
            $"expects {expected:0.##}:1, so it will look stretched.{advice}{resizeNote}");
    }

    // The rest of the sentence after the map type: what the map covers, and what uses the fill.
    private static string CoverageNote(MapProjection type, MapImageCheck check)
    {
        switch (type)
        {
            case MapProjection.Mercator:
                double limit = MapProjections.MercatorLatitudeLimit(check.AspectRatio);
                return $", covering about {limit:0}°N to {limit:0}°S. The poles beyond that " +
                    "use the Fill color.";
            case MapProjection.Polar:
                return ", covering the northern hemisphere. The southern hemisphere uses the " +
                    "Fill color.";
            default:
                return ".";
        }
    }

    // How the map type reads in a sentence ("as a Robinson map").
    private static string DescriptiveName(MapProjection type)
    {
        return type switch
        {
            MapProjection.Equirectangular => "globe map",
            MapProjection.Mercator => "flat map",
            MapProjection.Robinson => "Robinson map",
            MapProjection.WinkelTripel => "Winkel tripel map",
            MapProjection.Mollweide => "Mollweide map",
            MapProjection.GallPeters => "Gall–Peters map",
            MapProjection.Polar => "polar map",
            MapProjection.TwoHemispheres => "two-hemisphere map",
            _ => "map",
        };
    }

    private async void ShowMessage(string text, MessageKind kind, bool autoHide = true)
    {
        int version = ++_messageVersion;
        _message.Text = text;
        _message.AddThemeColorOverride("font_color", kind switch
        {
            MessageKind.Warning => _warningColor,
            MessageKind.Error => _errorColor,
            _ => _infoColor,
        });
        _message.Visible = true;

        if (kind != MessageKind.Info || !autoHide)
        {
            return;
        }

        SceneTreeTimer timer = GetTree().CreateTimer(InfoMessageSeconds);
        await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        if (version == _messageVersion)
        {
            _message.Visible = false;
        }
    }

    private OptionButton CreateMapTypeDropdown()
    {
        var dropdown = new OptionButton
        {
            FocusMode = Control.FocusModeEnum.None,
            TooltipText =
                "Match the layout your map was drawn in:\n" +
                "Flat map: hand-drawn and fantasy-tool maps; keeps shapes as drawn.\n" +
                "Globe map: 2:1 equirectangular maps made for globes.\n" +
                "Atlas types and circular types: maps drawn in those layouts.",
        };
        dropdown.AddItem("Flat map", (int)MapProjection.Mercator);
        dropdown.AddItem("Globe map", (int)MapProjection.Equirectangular);
        dropdown.AddSeparator("Atlas");
        dropdown.AddItem("Robinson", (int)MapProjection.Robinson);
        dropdown.AddItem("Winkel tripel", (int)MapProjection.WinkelTripel);
        dropdown.AddItem("Mollweide", (int)MapProjection.Mollweide);
        dropdown.AddItem("Gall–Peters", (int)MapProjection.GallPeters);
        dropdown.AddSeparator("Circular");
        dropdown.AddItem("Polar (north)", (int)MapProjection.Polar);
        dropdown.AddItem("Two hemispheres", (int)MapProjection.TwoHemispheres);
        dropdown.ItemSelected += OnMapTypeSelected;
        return dropdown;
    }

    private ColorPickerButton CreateFillColorPicker()
    {
        var picker = new ColorPickerButton
        {
            EditAlpha = false,
            CustomMinimumSize = new Vector2(40, 0),
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Color used where the map doesn't cover the globe",
        };
        picker.ColorChanged += color => Session?.SetFillColor(color.ToRgbColor());
        return picker;
    }

    private static Button CreateButton(string text, string tooltip)
    {
        // No keyboard focus, so arrow keys and Space keep controlling the camera.
        return new Button
        {
            Text = text,
            TooltipText = tooltip,
            FocusMode = Control.FocusModeEnum.None,
        };
    }

    private static Label CreateLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", _infoColor);
        label.AddThemeColorOverride("font_shadow_color", Colors.Black);
        return label;
    }
}
