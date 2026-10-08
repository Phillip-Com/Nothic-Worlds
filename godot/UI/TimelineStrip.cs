using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The timeline of events (VISION.md LORE-03): the world's timelines as lanes of events (see
/// <see cref="TimelineCanvas"/>), with buttons to add an event at the current time, manage the
/// timelines, return to now, and zoom. It's shown as the Timeline view of the Calendar tab
/// (<see cref="CalendarPanel"/>, CAL-05), which takes its content; this keeps the event and
/// timeline editors.
/// </summary>
public partial class TimelineStrip : CanvasLayer
{
    private const int ScreenMargin = 12;

    // Above the time bar's two panels in the bottom-right corner.
    private const int BottomOffset = 112;

    private PanelContainer _panel = null!;
    private VBoxContainer _content = null!;
    private bool _embedded;  // Its content is shown elsewhere (DetachContent)
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

        var panel = _panel = new PanelContainer();
        panel.AnchorLeft = 0;
        panel.AnchorRight = 1;
        panel.AnchorTop = 1;
        panel.AnchorBottom = 1;
        panel.OffsetLeft = ScreenMargin;
        panel.OffsetRight = -ScreenMargin;
        panel.OffsetBottom = -BottomOffset;
        panel.GrowVertical = Control.GrowDirection.Begin;  // Taller with more lanes, upward.
        AddChild(panel);

        var layout = _content = new VBoxContainer();
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

    /// <summary>
    /// Hands over the strip's content (its buttons and lanes) to be shown elsewhere (the
    /// Calendar tab's Timeline view), or null if it isn't built. The strip's own panel stays
    /// hidden from then on; its editors still open from here.
    /// </summary>
    public Control? DetachContent()
    {
        if (_content is null || _embedded)
        {
            return null;
        }

        _embedded = true;
        _panel.RemoveChild(_content);
        _panel.Visible = false;
        UpdateVisibility();
        return _content;
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
        if (_open)
        {
            _canvas.QueueRedraw();
        }
    }

    private void UpdateVisibility()
    {
        // Shown inside the Calendar tab, which gives the hint; the editors here still open.
        if (_embedded)
        {
            Visible = true;
            return;
        }

        Visible = _open && (Toolbar?.Visible ?? true);
        Toolbar?.SetHint(this, _open
            ? "New Event adds one at the current date. Drag the strip to scroll through time; " +
                "click an event to go to it, double-click it to edit it."
            : null);
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
