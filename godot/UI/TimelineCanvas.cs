using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Interop;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// Draws the timeline strip (VISION.md LORE-03): a ruler in the selected body's calendar, one
/// lane per shown timeline with its events (dots for moments, bars for spans), and a line at
/// the current time. The mouse wheel zooms around the pointer and dragging scrolls through
/// time. Clicking an event jumps the clock there; double-clicking opens its editor (owner's
/// choice).
/// </summary>
public partial class TimelineCanvas : Control
{
    private const float RulerHeight = 22;
    private const float LaneHeight = 24;
    private const float LaneNameWidth = 110;
    private const float PixelsPerTick = 110;
    private const float DotRadius = 5;
    private const float ClickSlopPixels = 4;

    // How far the view can zoom: a minute per pixel, up to about 27,000 years per pixel.
    private const double MinDaysPerPixel = 1.0 / 1440;
    private const double MaxDaysPerPixel = 1e7;

    private static readonly Color _background = new(0.07f, 0.07f, 0.09f, 0.94f);
    private static readonly Color _rulerText = new(0.75f, 0.78f, 0.85f);
    private static readonly Color _gridLine = new(1, 1, 1, 0.08f);
    private static readonly Color _nowLine = new(1.0f, 0.95f, 0.7f, 0.9f);
    private static readonly Color _textOnBar = new(0.05f, 0.05f, 0.07f);

    // Where each drawn event is, for clicks and tooltips (rebuilt with every draw).
    private readonly List<(Rect2 Area, TimelineEvent Event)> _drawn = [];
    private double _centerDays;
    private double _daysPerPixel = 365.25 * 2 / 800;
    private Vector2? _pressedAt;
    private double _pressedCenter;
    private bool _dragging;

    /// <summary>The open world. Set it before adding the canvas to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    /// <summary>Raised when an event is clicked (to jump to it).</summary>
    public event Action<TimelineEvent>? EventClicked;

    /// <summary>Raised when an event is double-clicked (to edit it).</summary>
    public event Action<TimelineEvent>? EventDoubleClicked;

    /// <summary>The event drawn highlighted (the last one clicked), or null.</summary>
    public Guid? HighlightedEventId { get; set; }

    /// <summary>The time at the middle of the strip, in standard days.</summary>
    public double CenterDays => _centerDays;

    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
        _centerDays = Session.TimeDays;
        UpdateHeight();
    }

    /// <summary>Scrolls so the current time is in the middle.</summary>
    public void CenterOnNow()
    {
        _centerDays = Session.TimeDays;
        QueueRedraw();
    }

    /// <summary>Zooms in (positive) or out (negative) around the middle.</summary>
    public void Zoom(int steps)
    {
        ZoomAround(Size.X / 2, steps);
    }

    /// <summary>Sizes the strip for the number of shown lanes (at least one).</summary>
    public void UpdateHeight()
    {
        int lanes = Math.Max(1, Session.World.Timelines.Count(t => !t.Hidden));
        CustomMinimumSize = new Vector2(0, RulerHeight + lanes * LaneHeight + 4);
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true } up:
                ZoomAround(up.Position.X, +1);
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true } down:
                ZoomAround(down.Position.X, -1);
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } click:
                HandleClick(click);
                AcceptEvent();
                break;
            case InputEventMouseMotion motion when _pressedAt is Vector2 start:
                if (_dragging || motion.Position.DistanceTo(start) > ClickSlopPixels)
                {
                    _dragging = true;
                    _centerDays = _pressedCenter - (motion.Position.X - start.X) * _daysPerPixel;
                    QueueRedraw();
                }

                AcceptEvent();
                break;
        }
    }

    public override string _GetTooltip(Vector2 atPosition)
    {
        if (EventAt(atPosition) is not TimelineEvent timelineEvent)
        {
            return "";
        }

        Body body = Session.SelectedBody;
        string when = BodyClock.Describe(body, timelineEvent.StartDays);
        if (timelineEvent.EndDays is double end)
        {
            when += $"\nto {BodyClock.Describe(body, end)}";
        }

        string timeline = Session.World.Timelines
            .FirstOrDefault(t => t.Id == timelineEvent.TimelineId)?.Name ?? "";
        return $"{timelineEvent.Title}  ({timeline})\n{when}\n" +
            "Click to go there, double-click to edit";
    }

    public override void _Draw()
    {
        _drawn.Clear();
        DrawRect(new Rect2(Vector2.Zero, Size), _background);
        Font font = GetThemeDefaultFont();
        int fontSize = GetThemeDefaultFontSize() - 2;
        DrawRuler(font, fontSize);

        List<Timeline> lanes = [.. Session.World.Timelines.Where(t => !t.Hidden)];
        if (lanes.Count == 0)
        {
            DrawString(font, new Vector2(8, RulerHeight + 17),
                Session.World.Timelines.Count == 0
                    ? "No timelines yet: New Event starts one."
                    : "Every timeline is hidden (Timelines… shows them).",
                fontSize: fontSize, modulate: _rulerText);
        }

        for (int lane = 0; lane < lanes.Count; lane++)
        {
            DrawLane(lanes[lane], RulerHeight + lane * LaneHeight, font, fontSize);
        }

        float now = XOf(Session.TimeDays);
        if (now >= 0 && now <= Size.X)
        {
            DrawLine(new Vector2(now, 0), new Vector2(now, Size.Y), _nowLine, 2);
        }
    }

    private void DrawRuler(Font font, int fontSize)
    {
        double from = TimeOf(0);
        double to = TimeOf(Size.X);
        int maxTicks = Math.Max(1, (int)(Size.X / PixelsPerTick));
        foreach (RulerTick tick in TimeRuler.Ticks(Session.SelectedBody, from, to, maxTicks))
        {
            float x = XOf(tick.TimeDays);
            DrawLine(new Vector2(x, RulerHeight - 6), new Vector2(x, Size.Y), _gridLine);
            DrawString(font, new Vector2(x + 3, 15), tick.Label, fontSize: fontSize,
                modulate: _rulerText);
        }
    }

    private void DrawLane(Timeline timeline, float top, Font font, int fontSize)
    {
        Color color = timeline.Color.ToGodot();
        float middle = top + LaneHeight / 2;
        DrawLine(new Vector2(0, top), new Vector2(Size.X, top), _gridLine);

        // Events in time order, each label only where it doesn't run into the one before (the
        // tooltip still names every event). Then the lane's name on top at the left.
        double from = TimeOf(0);
        double to = TimeOf(Size.X);
        float labelsEnd = LaneNameWidth;
        foreach (TimelineEvent timelineEvent in Session.World.Events
            .Where(e => e.TimelineId == timeline.Id && (e.EndDays ?? e.StartDays) >= from
                && e.StartDays <= to)
            .OrderBy(e => e.StartDays))
        {
            labelsEnd = DrawEvent(timelineEvent, middle, color, font, fontSize, labelsEnd);
        }

        var nameArea = new Rect2(0, top + 1, LaneNameWidth, LaneHeight - 2);
        DrawRect(nameArea, _background);
        DrawString(font, new Vector2(6, middle + 5), timeline.Name, width: LaneNameWidth - 10,
            fontSize: fontSize, modulate: color);
    }

    // Draws one event, and its label if it fits after `labelsEnd` (where the lane's last label
    // ended). Returns where this event's label ends (or `labelsEnd` if it had none).
    private float DrawEvent(TimelineEvent timelineEvent, float middle, Color color, Font font,
        int fontSize, float labelsEnd)
    {
        bool highlighted = timelineEvent.Id == HighlightedEventId;
        float start = XOf(timelineEvent.StartDays);
        Rect2 area;
        if (timelineEvent.EndDays is double endDays)
        {
            float end = Math.Max(XOf(endDays), start + 4);
            float left = Math.Max(start, -10);
            float right = Math.Min(end, Size.X + 10);
            area = new Rect2(left, middle - 6, right - left, 12);
            DrawRect(area, color with { A = 0.75f });
        }
        else
        {
            area = new Rect2(start - DotRadius, middle - DotRadius, DotRadius * 2, DotRadius * 2);
            DrawCircle(new Vector2(start, middle), DotRadius, color);
        }

        if (highlighted)
        {
            DrawRect(area.Grow(2), Colors.White, filled: false, width: 1.5f);
        }

        _drawn.Add((area.Grow(3), timelineEvent));
        float width = Math.Min(160,
            font.GetStringSize(timelineEvent.Title, fontSize: fontSize).X);
        bool insideBar = timelineEvent.EndDays is not null && area.Size.X > width + 8;
        float labelX = insideBar ? area.Position.X + 4 : area.End.X + 4;
        labelX = Math.Max(labelX, LaneNameWidth + 4);
        if (labelX < labelsEnd + 6)
        {
            return labelsEnd;  // No room: it would run into the label before.
        }

        Color text = insideBar ? _textOnBar : highlighted ? Colors.White : color;
        if (!insideBar)
        {
            // A backing, so the label reads even over another event's bar.
            DrawRect(new Rect2(labelX - 2, middle - 8, width + 4, 16), _background);
        }

        DrawString(font, new Vector2(labelX, middle + 5), timelineEvent.Title,
            width: 160, fontSize: fontSize, modulate: text);
        return labelX + width;
    }

    private void HandleClick(InputEventMouseButton click)
    {
        if (click.Pressed)
        {
            if (click.DoubleClick && EventAt(click.Position) is TimelineEvent doubled)
            {
                EventDoubleClicked?.Invoke(doubled);
                _pressedAt = null;
                return;
            }

            _pressedAt = click.Position;
            _pressedCenter = _centerDays;
            _dragging = false;
            return;
        }

        if (!_dragging && _pressedAt is not null && EventAt(click.Position) is TimelineEvent hit)
        {
            HighlightedEventId = hit.Id;
            EventClicked?.Invoke(hit);
            QueueRedraw();
        }

        _pressedAt = null;
        _dragging = false;
    }

    // The event drawn under a point, the topmost (latest drawn) first.
    private TimelineEvent? EventAt(Vector2 position)
    {
        for (int i = _drawn.Count - 1; i >= 0; i--)
        {
            if (_drawn[i].Area.HasPoint(position))
            {
                return _drawn[i].Event;
            }
        }

        return null;
    }

    // Zooms by steps (positive is in), keeping the time under the pointer where it is.
    private void ZoomAround(float x, int steps)
    {
        double anchor = TimeOf(x);
        _daysPerPixel = Math.Clamp(_daysPerPixel * Math.Pow(0.8, steps),
            MinDaysPerPixel, MaxDaysPerPixel);
        _centerDays = anchor - (x - Size.X / 2) * _daysPerPixel;
        QueueRedraw();
    }

    private double TimeOf(float x) => _centerDays + (x - Size.X / 2) * _daysPerPixel;

    private float XOf(double timeDays) =>
        (float)Math.Clamp(Size.X / 2 + (timeDays - _centerDays) / _daysPerPixel, -1e6, 1e6);
}
