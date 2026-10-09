using Godot;
using NothicWorlds.Rendering;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The top bar (VISION.md UI-01): a slot for the File, Edit, View, and Add menus, then the panel
/// buttons (System, Map, Terrain, Journal, Regions, Diagrams, Timeline), plus a message line
/// underneath that
/// other parts of the app use. Success messages fade after a few seconds. Warnings and errors stay
/// until the next message. Along the bottom of the screen, the hint bar says how to use the tool
/// that's open (VISION.md UI-05).
/// </summary>
public partial class MapToolbar : CanvasLayer
{
    private const int ScreenMargin = 12;

    // How far down the hint bar sits: just under the toolbar's buttons.
    private const int HintBarTop = 56;
    private const double InfoMessageSeconds = 6.0;

    // How long the selected body's preparing goes on before it's mentioned (a quick one isn't).
    private const double PreparingMessageSeconds = 0.3;
    private const float MessageWidth = 460.0f;
    private const float HintWidth = 560.0f;

    private static readonly Color _infoColor = new(0.92f, 0.94f, 0.98f);
    private static readonly Color _warningColor = new(1.0f, 0.8f, 0.35f);
    private static readonly Color _errorColor = new(1.0f, 0.5f, 0.45f);

    private Button _systemButton = null!;
    private Button _journalButton = null!;
    private Button _calendarButton = null!;
    private Button _regionsButton = null!;
    private Button _diagramsButton = null!;
    private Label _message = null!;
    private PanelContainer _hintBar = null!;
    private Label _hint = null!;

    // The open tools' hints, oldest first: the newest one is shown.
    private readonly List<(object Tool, string Text)> _hints = [];

    // Increases with every message, so an old auto-hide timer doesn't hide a newer message.
    private int _messageVersion;
    private int _preparingMessage = -1;  // The message saying a body is being prepared
    private int _carvingMessage = -1;  // The message saying shapes are being carved

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

    /// <summary>
    /// The Calendar tab (VISION.md CAL-05), shown and hidden by the Calendar button; its
    /// Timeline view holds the timeline of events.
    /// </summary>
    [Export] public CalendarPanel? Calendar { get; set; }

    /// <summary>The Regions panel, shown and hidden by the Regions button.</summary>
    [Export] public RegionsPanel? RegionsPanel { get; set; }

    /// <summary>
    /// The relationship diagrams page (VISION.md LORE-04), shown and hidden by the Diagrams
    /// button. It takes the whole window, so it's one of the right-hand buttons: opening the
    /// Journal (to read an entry from a box) closes it, and opening it closes the panels.
    /// </summary>
    [Export] public DiagramPage? Diagrams { get; set; }

    /// <summary>
    /// The open world, for saying when the selected body's terrain is still being prepared
    /// (VISION.md REN-03).
    /// </summary>
    [Export] public WorldSession? Session { get; set; }

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
        _diagramsButton = CreateButton(
            "Diagrams", "Relationship diagrams: how characters, factions, and nations are tied");
        _calendarButton = CreateButton("Calendar",
            "The selected world's calendar: its months and years, moons, seasons, eclipses, " +
            "and events; and the timeline of your world's history");
        Button[] rightPanels = [mapButton, terrainButton, _journalButton, _regionsButton,
            _diagramsButton, _calendarButton];
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
        _diagramsButton.Toggled += open =>
        {
            if (open)
            {
                _systemButton.ButtonPressed = false;  // It would cover the diagram list
            }

            if (Diagrams is not null)
            {
                Diagrams.IsPageOpen = open;
            }
        };

        _calendarButton.Toggled += open =>
        {
            if (Calendar is not null)
            {
                Calendar.IsPanelOpen = open;
            }
        };

        _message = CreateLabel("");
        _message.Visible = false;
        _message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _message.CustomMinimumSize = new Vector2(MessageWidth, 0);
        layout.AddChild(_message);
        CreateHintBar();
        if (Session is not null)
        {
            Session.Preparing += SayPreparing;
        }

        ShapedGlobe.CarvingChanged += SayCarving;
    }

    public override void _ExitTree()
    {
        ShapedGlobe.CarvingChanged -= SayCarving;
    }

    // While shapes are being carved (VISION.md BOD-04), says so, since carving briefly pauses
    // the app; takes it away when done.
    private void SayCarving(bool underway)
    {
        if (underway)
        {
            ShowInfo("Carving shapes…", autoHide: false);
            _carvingMessage = _messageVersion;
        }
        else if (_carvingMessage == _messageVersion)
        {
            _message.Visible = false;
        }
    }

    // While the selected body takes a moment to prepare, says so; takes it away when done.
    private async void SayPreparing(Guid bodyId, bool underway)
    {
        if (Session is null || bodyId != Session.SelectedBodyId)
        {
            return;
        }

        if (!underway)
        {
            if (_preparingMessage == _messageVersion)
            {
                _message.Visible = false;
            }

            return;
        }

        await ToSignal(GetTree().CreateTimer(PreparingMessageSeconds),
            SceneTreeTimer.SignalName.Timeout);
        if (Session.IsPreparing(bodyId) && bodyId == Session.SelectedBodyId)
        {
            ShowInfo($"Preparing {Session.SelectedBody.Name}'s terrain…", autoHide: false);
            _preparingMessage = _messageVersion;
        }
    }

    /// <summary>
    /// Shows how to use a tool in the hint bar while it's open, or with null (or ""), takes its
    /// hint away. The tool given a hint last is the one shown; when it closes, the hint of the
    /// one before it comes back.
    /// </summary>
    /// <param name="tool">The tool the hint belongs to (usually its panel).</param>
    /// <param name="text">One line on how to use it; null or "" when it closes.</param>
    public void SetHint(object tool, string? text)
    {
        int index = _hints.FindIndex(hint => hint.Tool == tool);
        if (index >= 0 && !string.IsNullOrEmpty(text))
        {
            _hints[index] = (tool, text);  // Same tool, new words: it keeps its place.
        }
        else if (index >= 0)
        {
            _hints.RemoveAt(index);
        }
        else if (!string.IsNullOrEmpty(text))
        {
            _hints.Add((tool, text));
        }

        ShowHint();
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

    /// <summary>Opens the Calendar tab on its timeline of events.</summary>
    public void ShowTimeline()
    {
        _calendarButton.ButtonPressed = true;
        Calendar?.ShowTimelineView();
    }

    /// <summary>Closes the Calendar tab.</summary>
    public void CloseCalendar()
    {
        _calendarButton.ButtonPressed = false;
    }

    /// <summary>Opens the Calendar tab on the month the clock is in.</summary>
    public void ShowCalendar()
    {
        _calendarButton.ButtonPressed = true;
        Calendar?.ShowMonthView();
    }

    /// <summary>Closes the diagrams page, back to the globe.</summary>
    public void HideDiagrams()
    {
        _diagramsButton.ButtonPressed = false;
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

    /// <summary>
    /// Says that an edit has to wait while the world is saving, opening, or loading a map
    /// (VISION.md UI-05: tools say why they can't act).
    /// </summary>
    public void ShowBusyWarning()
    {
        ShowWarning("Wait a moment for the world to finish saving or loading, then try again.");
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

    // A dark strip centered along the bottom, between the camera text and the time bar.
    private void CreateHintBar()
    {
        _hintBar = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        _hintBar.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.06f, 0.09f, 0.78f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        });
        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        // Along the top, under the toolbar (VISION.md UI-07): the bottom is the time bar's and
        // the calendar's.
        row.SetAnchorsAndOffsetsPreset(
            Control.LayoutPreset.TopWide, Control.LayoutPresetMode.Minsize, ScreenMargin);
        row.OffsetTop = HintBarTop;
        row.AddChild(_hintBar);
        AddChild(row);

        _hint = CreateLabel("");
        _hint.MouseFilter = Control.MouseFilterEnum.Ignore;
        _hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        _hint.CustomMinimumSize = new Vector2(HintWidth, 0);
        _hintBar.AddChild(_hint);
    }

    private void ShowHint()
    {
        _hint.Text = _hints.Count > 0 ? _hints[^1].Text : "";
        _hintBar.Visible = _hints.Count > 0;
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
