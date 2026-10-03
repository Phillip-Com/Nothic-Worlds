using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The timeline strip along the bottom of the screen, above the time bar (VISION.md LORE-03;
/// owner's choice), shown by the Timeline… toolbar button: the world's timelines as lanes of
/// events (see <see cref="TimelineCanvas"/>), with buttons to add an event at the current time,
/// manage the timelines, return to now, and zoom.
/// </summary>
public partial class TimelineStrip : CanvasLayer
{
    private const int ScreenMargin = 12;

    // Above the time bar's two panels in the bottom-right corner.
    private const int BottomOffset = 112;

    private TimelineCanvas _canvas = null!;
    private EventDialog _eventDialog = null!;
    private TimelinesDialog _timelinesDialog = null!;
    private bool _open;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The toolbar: the strip hides whenever it does.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>The time bar, for gliding the clock to an event.</summary>
    [Export] public TimeControls? Time { get; set; }

    /// <summary>Places pins by clicking on the globe (for the event editor).</summary>
    [Export] public Controls.PinPlacer? Placer { get; set; }

    /// <summary>Whether the strip is shown (it's still hidden while the toolbar is).</summary>
    public bool IsStripOpen
    {
        get => _open;
        set
        {
            _open = value;
            UpdateVisibility();
            if (value)
            {
                _canvas?.CenterOnNow();
            }
        }
    }

    public override void _Ready()
    {
        if (Session is null)
        {
            GD.PushError("TimelineStrip needs a world session.");
            return;
        }

        var panel = new PanelContainer();
        panel.AnchorLeft = 0;
        panel.AnchorRight = 1;
        panel.AnchorTop = 1;
        panel.AnchorBottom = 1;
        panel.OffsetLeft = ScreenMargin;
        panel.OffsetRight = -ScreenMargin;
        panel.OffsetBottom = -BottomOffset;
        panel.GrowVertical = Control.GrowDirection.Begin;  // Taller with more lanes, upward.
        AddChild(panel);

        var layout = new VBoxContainer();
        panel.AddChild(layout);
        layout.AddChild(BuildButtons());
        _canvas = new TimelineCanvas { Session = Session };
        _canvas.EventClicked += timelineEvent => Time?.GlideTo(timelineEvent.StartDays);
        _canvas.EventDoubleClicked += timelineEvent => _eventDialog.Edit(timelineEvent);
        layout.AddChild(_canvas);

        _eventDialog = new EventDialog
        {
            Session = Session,
            GlideTo = time => Time?.GlideTo(time),
            Placer = Placer,
        };
        AddChild(_eventDialog);
        _timelinesDialog = new TimelinesDialog { Session = Session };
        AddChild(_timelinesDialog);

        Session.Changed += () =>
        {
            _canvas.UpdateHeight();
            _canvas.QueueRedraw();
        };
        Session.TimeChanged += RedrawIfShown;
        Session.SelectionChanged += _canvas.QueueRedraw;  // Dates follow its calendar.
        Session.WorldClosed += _ => _canvas.HighlightedEventId = null;
        if (Toolbar is not null)
        {
            Toolbar.VisibilityChanged += UpdateVisibility;
        }

        UpdateVisibility();
    }

    private Control BuildButtons()
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = "Timeline" });
        row.AddChild(CreateButton("New Event", AddEvent,
            "Add an event at the current time, then edit it"));
        row.AddChild(CreateButton("Timelines…", () => _timelinesDialog.Open(),
            "Add, rename, recolor, show or hide, reorder, and delete timelines"));
        row.AddChild(CreateButton("Now", () => _canvas.CenterOnNow(),
            "Scroll the strip back to the current time"));
        row.AddChild(CreateButton("−", () => _canvas.Zoom(-2), "Zoom out (or use the wheel)"));
        row.AddChild(CreateButton("+", () => _canvas.Zoom(+2), "Zoom in (or use the wheel)"));
        row.AddChild(new Label
        {
            Text = "  Drag to scroll · click an event to go there · double-click to edit",
            Modulate = new Color(1, 1, 1, 0.55f),
        });
        return row;
    }

    /// <summary>Opens an event's editor (e.g. from a pin's pop-up).</summary>
    public void EditEvent(TimelineEvent timelineEvent)
    {
        _canvas.HighlightedEventId = timelineEvent.Id;
        _eventDialog.Edit(timelineEvent);
    }

    /// <summary>Adds an event at the current time and opens its editor.</summary>
    public void AddEvent()
    {
        TimelineEvent timelineEvent = Session!.AddEvent(Session.TimeDays);
        _canvas.HighlightedEventId = timelineEvent.Id;
        _eventDialog.Edit(timelineEvent);
    }

    private void RedrawIfShown()
    {
        if (Visible)
        {
            _canvas.QueueRedraw();
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
        };
        button.Pressed += pressed;
        return button;
    }
}
