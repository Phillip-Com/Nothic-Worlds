using Godot;

namespace NothicWorlds.UI;

/// <summary>
/// The top bar (VISION.md UI-01): a slot for the File, Edit, View, and Add menus, then the panel
/// buttons (System, Map, Terrain, Journal, Regions, Timeline), plus a message line underneath that
/// other parts of the app use. Success messages fade after a few seconds. Warnings and errors stay
/// until the next message.
/// </summary>
public partial class MapToolbar : CanvasLayer
{
    private const int ScreenMargin = 12;
    private const double InfoMessageSeconds = 6.0;
    private const float MessageWidth = 460.0f;

    private static readonly Color _infoColor = new(0.92f, 0.94f, 0.98f);
    private static readonly Color _warningColor = new(1.0f, 0.8f, 0.35f);
    private static readonly Color _errorColor = new(1.0f, 0.5f, 0.45f);

    private Button _systemButton = null!;
    private Button _journalButton = null!;
    private Button _timelineButton = null!;
    private Button _regionsButton = null!;
    private Label _message = null!;

    // Increases with every message, so an old auto-hide timer doesn't hide a newer message.
    private int _messageVersion;

    /// <summary>The System panel, shown and hidden by the System button.</summary>
    [Export] public SystemPanel? SystemPanel { get; set; }

    /// <summary>The Map panel, shown and hidden by the Map button.</summary>
    [Export] public MapPanel? MapPanel { get; set; }

    /// <summary>The Terrain panel, shown and hidden by the Terrain button.</summary>
    [Export] public TerrainPanel? Terrain { get; set; }

    /// <summary>
    /// The Journal panel, shown and hidden by the Journal button. It shares the right side
    /// with the Map, Terrain, and Regions panels, one at a time (owner's choice).
    /// </summary>
    [Export] public JournalPanel? Journal { get; set; }

    /// <summary>The timeline strip, shown and hidden by the Timeline button.</summary>
    [Export] public TimelineStrip? Timeline { get; set; }

    /// <summary>The Regions panel, shown and hidden by the Regions button.</summary>
    [Export] public RegionsPanel? RegionsPanel { get; set; }

    /// <summary>Space at the start of the toolbar row, where the menus go.</summary>
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

        controls.AddChild(new VSeparator());
        _systemButton = CreateButton(
            "System", "The star system: add, delete, and edit suns, planets, and moons");
        _systemButton.ToggleMode = true;
        _systemButton.Toggled += open =>
        {
            if (SystemPanel is not null)
            {
                SystemPanel.IsPanelOpen = open;
            }
        };
        controls.AddChild(_systemButton);

        // The panels on the right share it, one at a time (owner's choices): opening one
        // closes the others.
        var mapButton = CreateButton(
            "Map",
            "The planet's map: import it, line it up, and place pieces cut from it or other " +
            "images");
        var terrainButton = CreateButton(
            "Terrain", "Paint terrain onto the planet: oceans, forests, mountains, and more");
        _journalButton = CreateButton(
            "Journal", "The world's journal: write entries about places and history");
        _regionsButton = CreateButton(
            "Regions", "Outline and name regions on the planet: countries, forests, seas");
        Button[] rightPanels = [mapButton, terrainButton, _journalButton, _regionsButton];
        foreach (Button button in rightPanels)
        {
            button.ToggleMode = true;
            button.Toggled += open =>
            {
                if (open)
                {
                    foreach (Button other in rightPanels.Where(b => b != button))
                    {
                        other.ButtonPressed = false;
                    }
                }
            };
            controls.AddChild(button);
        }

        mapButton.Toggled += open =>
        {
            if (MapPanel is not null)
            {
                MapPanel.IsPanelOpen = open;
            }
        };
        terrainButton.Toggled += open =>
        {
            if (Terrain is not null)
            {
                Terrain.IsPanelOpen = open;
            }
        };
        _journalButton.Toggled += open =>
        {
            if (Journal is not null)
            {
                Journal.IsPanelOpen = open;
            }
        };
        _regionsButton.Toggled += open =>
        {
            if (RegionsPanel is not null)
            {
                RegionsPanel.IsPanelOpen = open;
            }
        };

        _timelineButton = CreateButton(
            "Timeline", "The timeline strip: your world's history, as lanes of events");
        _timelineButton.ToggleMode = true;
        _timelineButton.Toggled += open =>
        {
            if (Timeline is not null)
            {
                Timeline.IsStripOpen = open;
            }
        };
        controls.AddChild(_timelineButton);

        _message = CreateLabel("");
        _message.Visible = false;
        _message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _message.CustomMinimumSize = new Vector2(MessageWidth, 0);
        layout.AddChild(_message);
    }

    /// <summary>Opens the Journal panel (closing the others on the right).</summary>
    public void ShowJournal()
    {
        _journalButton.ButtonPressed = true;
    }

    /// <summary>Opens the System panel.</summary>
    public void ShowSystem()
    {
        _systemButton.ButtonPressed = true;
    }

    /// <summary>Shows the timeline strip.</summary>
    public void ShowTimeline()
    {
        _timelineButton.ButtonPressed = true;
    }

    /// <summary>Opens the Regions panel (closing the others on the right).</summary>
    public void ShowRegions()
    {
        _regionsButton.ButtonPressed = true;
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
