using System.Diagnostics;
using Godot;
using NothicWorlds.Core.Maps;
using NothicWorlds.Maps;
using NothicWorlds.Rendering;

namespace NothicWorlds.UI;

/// <summary>
/// Top-left toolbar with "Import Map…" and "Clear Map" (VISION.md MAP-01), and a message line
/// underneath. Success messages fade after a few seconds. Warnings and errors stay until the
/// next action.
/// </summary>
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
    private Label _message = null!;
    private FileDialog _fileDialog = null!;
    private bool _isLoading;

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

        var buttons = new HBoxContainer();
        layout.AddChild(buttons);

        _importButton = CreateButton(
            "Import Map…", "Wrap a 2:1 map image (PNG, JPG, WebP) onto the planet");
        _importButton.Pressed += () => _fileDialog.PopupCentered();
        buttons.AddChild(_importButton);

        _clearButton = CreateButton("Clear Map", "Remove the map and show the grid");
        _clearButton.Disabled = true;
        _clearButton.Pressed += ClearMap;
        buttons.AddChild(_clearButton);

        _message = new Label
        {
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(MessageWidth, 0),
        };
        _message.AddThemeColorOverride("font_shadow_color", Colors.Black);
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
        }
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
            ShowResult(fileName, map.Check);
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

    private void ClearMap()
    {
        Planet?.ClearMap();
        _clearButton.Disabled = true;
        ShowMessage("Map cleared.", MessageKind.Info);
    }

    private void ShowResult(string fileName, MapImageCheck check)
    {
        string size = $"{check.TargetWidth} × {check.TargetHeight}";
        string resizeNote = check.NeedsResize
            ? $" It was shrunk from {check.OriginalWidth} × {check.OriginalHeight} to fit the " +
              $"{MapImageRules.MaxWidth} × {MapImageRules.MaxHeight} size limit."
            : "";

        if (check.IsSupportedLayout)
        {
            ShowMessage(
                $"Map applied: {fileName} ({size}).{resizeNote} Press G to show the grid.",
                MessageKind.Info);
            return;
        }

        ShowMessage(
            $"Map applied, but {fileName} is {check.OriginalWidth} × {check.OriginalHeight}, " +
            "not the 2:1 shape a globe needs, so it will look stretched. For best results, use " +
            $"an equirectangular map twice as wide as it is tall.{resizeNote}",
            MessageKind.Warning);
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

        await ToSignal(GetTree().CreateTimer(InfoMessageSeconds), SceneTreeTimer.SignalName.Timeout);
        if (version == _messageVersion)
        {
            _message.Visible = false;
        }
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
}
