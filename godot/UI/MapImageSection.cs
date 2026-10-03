using Godot;
using NothicWorlds.Core.Maps;
using NothicWorlds.Interop;
using NothicWorlds.Maps;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The top of the Map panel (VISION.md MAP-01, MAP-03 to MAP-05, UI-01): the planet's main map
/// image, with "Import Map…", "Clear Map", "Calibrate…", a "Map type" dropdown, and a
/// "Fill color" picker (for map types that don't cover the whole globe). Outcomes are shown in
/// the toolbar's message line.
/// </summary>
/// <remarks>
/// All edits go through <see cref="WorldSession"/>, so unsaved changes are tracked.
/// </remarks>
public partial class MapImageSection : VBoxContainer
{
    private Label _noMap = null!;
    private Button _importButton = null!;
    private Button _clearButton = null!;
    private Button _calibrateButton = null!;
    private OptionButton _mapType = null!;
    private ColorPickerButton _fillColor = null!;
    private Label _fillColorLabel = null!;
    private FileDialog _fileDialog = null!;

    // The imported file's name, for messages. Unknown for maps opened from a world file.
    private string? _mapFileName;

    /// <summary>The open world. Set it before adding the section to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    /// <summary>The Calibrate workspace, opened by the Calibrate… button.</summary>
    public CalibrationWorkspace? Calibration { get; init; }

    /// <summary>Where messages go.</summary>
    public MapToolbar? Toolbar { get; init; }

    public override void _Ready()
    {
        AddChild(new Label { Text = "Map Image" });
        _noMap = new Label
        {
            Text = "Stars have no map. Select a planet or moon.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        AddChild(_noMap);

        var buttons = new HFlowContainer();
        _importButton = CreateButton(
            "Import Map…", "Wrap a map image (PNG, JPG, WebP) onto the planet");
        _importButton.Pressed += () => _fileDialog.PopupCentered();
        _clearButton = CreateButton("Clear Map", "Remove the map and show the grid");
        _clearButton.Pressed += () => Session.ClearMap();
        _calibrateButton = CreateButton(
            "Calibrate…",
            "Line the map up with the globe by dragging its latitude/longitude lines");
        _calibrateButton.Pressed += () => Calibration?.Open();
        buttons.AddChild(_importButton);
        buttons.AddChild(_clearButton);
        buttons.AddChild(_calibrateButton);
        AddChild(buttons);

        var grid = new GridContainer { Columns = 2 };
        grid.AddChild(new Label { Text = "Map type" });
        _mapType = CreateMapTypeDropdown();
        grid.AddChild(_mapType);
        _fillColorLabel = new Label { Text = "Fill color" };
        grid.AddChild(_fillColorLabel);
        _fillColor = CreateFillColorPicker();
        grid.AddChild(_fillColor);
        AddChild(grid);

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

        Session.Changed += SyncWithWorld;
        Session.WorldClosed += OnWorldClosed;
        SyncWithWorld();
    }

    public override void _ExitTree()
    {
        Session.Changed -= SyncWithWorld;
        Session.WorldClosed -= OnWorldClosed;
    }

    /// <summary>
    /// Imports the map at <paramref name="path"/> and applies it to the planet, showing progress
    /// and the outcome in the message line. Never throws. Problems are shown to the user.
    /// </summary>
    public async Task ImportAsync(string path)
    {
        if (Session.IsBusy)
        {
            return;
        }

        string fileName = Path.GetFileName(path);
        Toolbar?.ShowInfo($"Loading {fileName}…", autoHide: false);
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
            Toolbar?.ShowError($"Couldn't import {fileName}: {error.Message}");
        }
        catch (Exception error)
        {
            GD.PushError($"Unexpected error importing {path}: {error}");
            Toolbar?.ShowError(
                $"Couldn't import {fileName} because of an unexpected error. " +
                "Details are in the log.");
        }
    }

    private void OnWorldClosed(Guid _)
    {
        _mapFileName = null;
    }

    // Updates the controls to match the open world, e.g. after opening a file.
    private void SyncWithWorld()
    {
        _mapType.Select(_mapType.GetItemIndex((int)Session.Projection));
        _fillColor.Color = Session.FillColor.ToGodot();
        bool hasSurface = Session.SelectedBodyHasSurface;  // Stars have no map.
        _noMap.Visible = !hasSurface;
        bool showFill = hasSurface && !MapProjections.CoversWholeGlobe(Session.Projection);
        _fillColorLabel.Visible = showFill;
        _fillColor.Visible = showFill;
        _importButton.Disabled = Session.IsBusy || !hasSurface;
        _mapType.Disabled = !hasSurface;
        _clearButton.Disabled = Session.IsBusy || Session.MapCheck is null;
        _calibrateButton.Disabled = Session.IsBusy || Session.MapCheck is null;
    }

    private void OnMapTypeSelected(long index)
    {
        Session.SetProjection((MapProjection)_mapType.GetItemId((int)index));
        if (Session.MapCheck is not null)
        {
            DescribeMap("Now showing");
        }
    }

    // Explains how the current map is shown. `lead` starts the sentence ("Map applied", ...).
    private void DescribeMap(string lead)
    {
        if (Session.MapCheck is not MapImageCheck check)
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
            Toolbar?.ShowInfo($"{description}{resizeNote} Press G to show the grid.");
            return;
        }

        double expected = MapProjections.ExpectedAspectRatio(type)!.Value;
        string advice = type == MapProjection.Equirectangular
            ? " Use a 2:1 equirectangular map, or switch Map type to Flat map."
            : " Check that the map was drawn in this layout.";
        Toolbar?.ShowWarning(
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

    private OptionButton CreateMapTypeDropdown()
    {
        var dropdown = new OptionButton
        {
            FocusMode = FocusModeEnum.None,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
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
            CustomMinimumSize = new Vector2(40, 28),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None,
            TooltipText = "Color used where the map doesn't cover the globe",
        };
        picker.ColorChanged += color => Session.SetFillColor(color.ToRgbColor());
        return picker;
    }

    private static Button CreateButton(string text, string tooltip)
    {
        // No keyboard focus, so arrow keys and Space keep controlling the camera.
        return new Button
        {
            Text = text,
            TooltipText = tooltip,
            FocusMode = FocusModeEnum.None,
        };
    }
}
