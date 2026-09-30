using System.Diagnostics;
using Godot;
using NothicWorlds.Core.Maps;
using NothicWorlds.Maps;
using NothicWorlds.Rendering;

namespace NothicWorlds.UI;

/// <summary>
/// Top-left toolbar (VISION.md MAP-01, MAP-03, MAP-04) with "Import Map…", "Clear Map", a
/// "Map type" dropdown, and a "Fill color" picker (for map types that don't cover the whole
/// globe), plus a message line underneath. Success messages fade after a few seconds. Warnings
/// and errors stay until the next action.
/// </summary>
public partial class MapToolbar : CanvasLayer
{
    private const int ScreenMargin = 12;
    private const double InfoMessageSeconds = 6.0;
    private const float MessageWidth = 460.0f;

    // New imports start as flat maps (owner decision), which suits hand-drawn maps.
    private const MapProjection DefaultProjection = MapProjection.Mercator;

    private static readonly Color _infoColor = new(0.92f, 0.94f, 0.98f);
    private static readonly Color _warningColor = new(1.0f, 0.8f, 0.35f);
    private static readonly Color _errorColor = new(1.0f, 0.5f, 0.45f);

    private Button _importButton = null!;
    private Button _clearButton = null!;
    private OptionButton _mapType = null!;
    private Control _fillColorControls = null!;
    private Label _message = null!;
    private FileDialog _fileDialog = null!;
    private bool _isLoading;

    // The applied map, so its message can be updated when the map type changes.
    private string? _mapFileName;
    private MapImageCheck? _mapCheck;

    // Increases with every message, so an old auto-hide timer doesn't hide a newer message.
    private int _messageVersion;

    /// <summary>The planet that maps are applied to.</summary>
    [Export] public PlanetSurface? Planet { get; set; }

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

        _importButton = CreateButton(
            "Import Map…", "Wrap a map image (PNG, JPG, WebP) onto the planet");
        _importButton.Pressed += () => _fileDialog.PopupCentered();
        controls.AddChild(_importButton);

        _clearButton = CreateButton("Clear Map", "Remove the map and show the grid");
        _clearButton.Disabled = true;
        _clearButton.Pressed += ClearMap;
        controls.AddChild(_clearButton);

        controls.AddChild(CreateLabel("  Map type:"));
        _mapType = CreateMapTypeDropdown();
        controls.AddChild(_mapType);

        _fillColorControls = new HBoxContainer();
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

        if (Planet is null)
        {
            GD.PushError("MapToolbar has no planet assigned.");
            _importButton.Disabled = true;
            _mapType.Disabled = true;
            return;
        }

        Planet.Projection = DefaultProjection;
        _fillColorControls.AddChild(CreateLabel("  Fill color:"));
        _fillColorControls.AddChild(CreateFillColorPicker(Planet));
        UpdateFillColorVisibility();
    }

    /// <summary>
    /// Imports the map at <paramref name="path"/> and applies it to the planet, showing progress
    /// and the outcome in the message line. Never throws. Problems are shown to the user.
    /// </summary>
    public async Task ImportAsync(string path)
    {
        if (_isLoading || Planet is null)
        {
            return;
        }

        string fileName = Path.GetFileName(path);
        SetLoading(true);
        ShowMessage($"Loading {fileName}…", MessageKind.Info, autoHide: false);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            LoadedMap map = await MapImageLoader.LoadAsync(path);
            Planet.SetMap(map.Texture);
            GD.Print($"Map imported: {fileName} in {stopwatch.Elapsed.TotalSeconds:0.00} s");
            _mapFileName = fileName;
            _mapCheck = map.Check;
            DescribeMap("Map applied");
        }
        catch (MapLoadException error)
        {
            ShowMessage($"Couldn't import {fileName}: {error.Message}", MessageKind.Error);
        }
        catch (Exception error)
        {
            GD.PushError($"Unexpected error importing {path}: {error}");
            ShowMessage(
                $"Couldn't import {fileName} because of an unexpected error. " +
                "Details are in the log.",
                MessageKind.Error);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void OnMapTypeSelected(long index)
    {
        if (Planet is null)
        {
            return;
        }

        Planet.Projection = (MapProjection)_mapType.GetItemId((int)index);
        UpdateFillColorVisibility();
        if (Planet.HasMap)
        {
            DescribeMap("Now showing");
        }
    }

    private void ClearMap()
    {
        Planet?.ClearMap();
        _mapFileName = null;
        _mapCheck = null;
        _clearButton.Disabled = true;
        ShowMessage("Map cleared.", MessageKind.Info);
    }

    // Explains how the current map is shown. `lead` starts the sentence ("Map applied", ...).
    private void DescribeMap(string lead)
    {
        if (Planet is null || _mapFileName is null || _mapCheck is null)
        {
            return;
        }

        MapImageCheck check = _mapCheck;
        MapProjection type = Planet.Projection;
        string resizeNote = check.NeedsResize
            ? $" It was shrunk from {check.OriginalWidth} × {check.OriginalHeight} to fit the " +
              $"{MapImageRules.MaxWidth} × {MapImageRules.MaxHeight} size limit."
            : "";
        string description = $"{lead}: {_mapFileName} ({check.TargetWidth} × " +
            $"{check.TargetHeight}) as a {DescriptiveName(type)}{CoverageNote(type, check)}";

        if (MapProjections.ShapeMatches(type, check.AspectRatio))
        {
            ShowMessage(
                $"{description}{resizeNote} Press G to show the grid.", MessageKind.Info);
            return;
        }

        double expected = MapProjections.ExpectedAspectRatio(type)!.Value;
        string advice = type == MapProjection.Equirectangular
            ? " Use a 2:1 equirectangular map, or switch Map type to Flat map."
            : " Check that the map was drawn in this layout.";
        ShowMessage(
            $"{description} But its shape is {check.AspectRatio:0.##}:1, and this map type " +
            $"expects {expected:0.##}:1, so it will look stretched.{advice}{resizeNote}",
            MessageKind.Warning);
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

    private void SetLoading(bool isLoading)
    {
        _isLoading = isLoading;
        _importButton.Disabled = isLoading;
        _clearButton.Disabled = isLoading || Planet is not { HasMap: true };
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
        dropdown.Select(dropdown.GetItemIndex((int)DefaultProjection));
        dropdown.ItemSelected += OnMapTypeSelected;
        return dropdown;
    }

    // The picker only matters for map types that leave part of the globe uncovered.
    private void UpdateFillColorVisibility()
    {
        _fillColorControls.Visible =
            Planet is not null && !MapProjections.CoversWholeGlobe(Planet.Projection);
    }

    private static ColorPickerButton CreateFillColorPicker(PlanetSurface planet)
    {
        var picker = new ColorPickerButton
        {
            Color = planet.FillColor,
            EditAlpha = false,
            CustomMinimumSize = new Vector2(40, 0),
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Color used where the map doesn't cover the globe",
        };
        picker.ColorChanged += color => planet.FillColor = color;
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
