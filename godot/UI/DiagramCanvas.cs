using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The drawing area of a relationship diagram (VISION.md LORE-04): a box for each journal entry
/// on it, colored by the entry's kind, and a line for each relationship between two of them,
/// styled by its kind (an elbow from parents down to children, a double line for a marriage,
/// dashes for rivals, a heavy red line for a war, arrows for one-way ties). Ties show as they
/// stand at the world clock's time: those that ended fade, those not yet begun are hidden,
/// unless every tie is shown.
/// </summary>
/// <remarks>
/// Drag a box to move it; drag from the dot on its right edge onto another box to link them;
/// double-click a box to open its entry; click a line to edit the tie; Delete takes the
/// selected box off the diagram. Drag the background (or with the middle or right button) to
/// pan; the wheel zooms around the mouse.
/// </remarks>
public partial class DiagramCanvas : Control
{
    private const float BoxWidth = 160, BoxHeight = 54, HandleRadius = 6;
    private const float LineHitPixels = 6;
    private const float MinZoom = 0.25f, MaxZoom = 3f;

    private static readonly Color _background = new(0.11f, 0.12f, 0.14f);
    private static readonly Color _selected = new(1f, 0.85f, 0.35f);

    private Vector2 _pan;  // Screen pixels the diagram's middle sits off the canvas's middle
    private float _zoom = 1;
    private Guid? _selectedEntry;
    private Guid? _moving;
    private Vector2 _grabOffset;  // Diagram units from the moving box's middle to the mouse
    private Guid? _linkingFrom;
    private Vector2 _mouse;
    private bool _panning;

    /// <summary>The open world.</summary>
    public WorldSession Session { get; init; } = null!;

    /// <summary>The diagram shown, or null for none.</summary>
    public Guid? DiagramId { get; private set; }

    /// <summary>
    /// True to show every tie whatever its dates; false shows them as of the clock.
    /// </summary>
    public bool ShowAllTimes { get; set; }

    /// <summary>Two boxes were linked by a drag: from, then to.</summary>
    public event Action<Guid, Guid>? LinkRequested;

    /// <summary>A line was clicked: its relationship.</summary>
    public event Action<Relationship>? RelationshipClicked;

    /// <summary>A box was double-clicked: its entry.</summary>
    public event Action<Guid>? EntryOpened;

    /// <summary>The selected box, or null.</summary>
    public Guid? SelectedEntry => _selectedEntry;

    public override void _Ready()
    {
        ClipContents = true;
        FocusMode = FocusModeEnum.Click;
        MouseFilter = MouseFilterEnum.Stop;
        Session.Changed += QueueRedraw;
        Session.TimeChanged += QueueRedraw;
    }

    /// <summary>Shows a diagram (or none), fitted to the view.</summary>
    public void Show(Guid? diagramId)
    {
        if (diagramId != DiagramId)
        {
            DiagramId = diagramId;
            _selectedEntry = null;
            CallDeferred(MethodName.FitView);  // Once the canvas has its size
        }

        QueueRedraw();
    }

    /// <summary>
    /// Centers the diagram's boxes in the view, zoomed out if needed for them all to show (but
    /// never in past normal size).
    /// </summary>
    public void FitView()
    {
        _pan = Vector2.Zero;
        _zoom = 1;
        if (Diagram() is not { Placements.Count: > 0 } diagram)
        {
            QueueRedraw();
            return;
        }

        double left = diagram.Placements.Min(p => p.X), right = diagram.Placements.Max(p => p.X);
        double top = diagram.Placements.Min(p => p.Y), bottom = diagram.Placements.Max(p => p.Y);
        var extent = new Vector2((float)(right - left) + BoxWidth * 2,
            (float)(bottom - top) + BoxHeight * 3);
        _zoom = Math.Clamp(Math.Min(Size.X / extent.X, Size.Y / extent.Y), MinZoom, 1);
        _pan = -new Vector2((float)(left + right) / 2, (float)(top + bottom) / 2) * _zoom;
        QueueRedraw();
    }

    /// <summary>The diagram spot at the middle of the view, where new boxes go.</summary>
    public Vector2 MiddleSpot() => ToDiagram(Size / 2);

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), _background);
        if (Diagram() is not LoreDiagram diagram)
        {
            DrawCentered("No diagram yet: press New to make one.");
            return;
        }

        if (diagram.Placements.Count == 0)
        {
            DrawCentered("Add journal entries from the list on the left.");
        }

        var boxes = diagram.Placements.ToDictionary(p => p.EntryId,
            p => ToScreen(new Vector2((float)p.X, (float)p.Y)));
        foreach ((Relationship tie, int lane, int lanes) in VisibleTies(boxes.Keys))
        {
            DrawTie(tie, boxes[tie.FromEntryId], boxes[tie.ToEntryId], lane, lanes);
        }

        foreach (DiagramPlacement placement in diagram.Placements)
        {
            DrawBox(placement.EntryId, boxes[placement.EntryId]);
        }

        if (_linkingFrom is Guid from && boxes.TryGetValue(from, out Vector2 start))
        {
            DrawLine(Handle(start), _mouse, _selected, 2, antialiased: true);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true } wheel:
                ZoomAt(wheel.Position, 1.15f);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true } wheel:
                ZoomAt(wheel.Position, 1 / 1.15f);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                OnLeftButton(button);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Middle or MouseButton.Right }
                button:
                _panning = button.Pressed;
                break;
            case InputEventMouseMotion motion:
                OnMotion(motion);
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Delete or Key.Backspace }
                when _selectedEntry is Guid entry && DiagramId is Guid diagram:
                Session.RemoveFromDiagram(diagram, entry);
                _selectedEntry = null;
                break;
            default:
                return;
        }

        AcceptEvent();
        QueueRedraw();
    }

    public override string _GetTooltip(Vector2 atPosition)
    {
        if (Diagram() is not LoreDiagram diagram || BoxAt(atPosition) is not null)
        {
            return "";
        }

        Relationship? tie = TieAt(atPosition, diagram);
        if (tie is null)
        {
            return "";
        }

        string when = (tie.StartDays, tie.EndDays) switch
        {
            (double start, double end) => $"\n{Date(start)} to {Date(end)}",
            (double start, null) => $"\nSince {Date(start)}",
            (null, double end) => $"\nUntil {Date(end)}",
            _ => "",
        };
        return $"{LoreWords.Sentence(tie, Session.World.Journal)}{when}\nClick to edit";
    }

    private void OnLeftButton(InputEventMouseButton button)
    {
        GrabFocus();
        if (!button.Pressed)
        {
            FinishDrag(button.Position);
            return;
        }

        if (Diagram() is not LoreDiagram diagram)
        {
            return;
        }

        if (HandleAt(button.Position, diagram) is Guid linking)
        {
            _linkingFrom = linking;
            _selectedEntry = linking;
            return;
        }

        if (BoxAt(button.Position) is DiagramPlacement box)
        {
            _selectedEntry = box.EntryId;
            if (button.DoubleClick)
            {
                EntryOpened?.Invoke(box.EntryId);
                return;
            }

            _moving = box.EntryId;
            _grabOffset = ToDiagram(button.Position) - new Vector2((float)box.X, (float)box.Y);
            Session.BeginGesture("Move Box");
            return;
        }

        _selectedEntry = null;
        if (TieAt(button.Position, diagram) is Relationship tie)
        {
            RelationshipClicked?.Invoke(tie);
            return;
        }

        _panning = true;
    }

    private void OnMotion(InputEventMouseMotion motion)
    {
        _mouse = motion.Position;
        if (_panning)
        {
            _pan += motion.Relative;
        }
        else if (_moving is Guid entry && DiagramId is Guid diagram)
        {
            Vector2 spot = ToDiagram(motion.Position) - _grabOffset;
            Session.PlaceOnDiagram(diagram, entry, Math.Round(spot.X), Math.Round(spot.Y));
        }
    }

    private void FinishDrag(Vector2 at)
    {
        if (_moving is not null)
        {
            Session.EndGesture();
        }

        if (_linkingFrom is Guid from && BoxAt(at) is DiagramPlacement target
            && target.EntryId != from)
        {
            LinkRequested?.Invoke(from, target.EntryId);
        }

        _moving = null;
        _linkingFrom = null;
        _panning = false;
    }

    private void ZoomAt(Vector2 at, float factor)
    {
        Vector2 spot = ToDiagram(at);
        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        _pan += at - ToScreen(spot);
    }

    // The ties to draw, with their place among the ties between the same two boxes (so
    // several between one pair sit side by side): as of the clock, or all of them.
    private List<(Relationship Tie, int Lane, int Lanes)> VisibleTies(IEnumerable<Guid> shown)
    {
        var onDiagram = shown.ToHashSet();
        double now = Session.World.TimeDays;
        List<Relationship> ties = [.. Session.World.Relationships.Where(r =>
            onDiagram.Contains(r.FromEntryId) && onDiagram.Contains(r.ToEntryId)
            && (ShowAllTimes || r.StartDays is not double start || start <= now))];
        return [.. ties.GroupBy(PairKey).SelectMany(group =>
            group.Select((tie, lane) => (tie, lane, group.Count())))];
    }

    private void DrawTie(Relationship tie, Vector2 from, Vector2 to, int lane, int lanes)
    {
        bool past = !ShowAllTimes && tie.EndDays is double end && end < Session.World.TimeDays;
        Color color = LoreWords.LineColor(tie.Kind) with { A = past ? 0.3f : 0.95f };
        float width = (tie.Kind == RelationshipKind.AtWarWith ? 3.5f : 2f) * Mathf.Sqrt(_zoom);
        if (tie.Kind == RelationshipKind.ParentOf && lanes == 1)
        {
            DrawElbow(from, to, color, width);
            DrawTieLabel(tie, (from + to) / 2, color);
            return;
        }

        Vector2 along = (to - from).Normalized();
        Vector2 side = Lane(tie, along, lane, lanes);
        Vector2 start = EdgeOfBox(from, to) + side, finish = EdgeOfBox(to, from) + side;
        switch (tie.Kind)
        {
            case RelationshipKind.MarriedTo:
                Vector2 apart = new Vector2(-along.Y, along.X) * 2.5f;
                DrawLine(start + apart, finish + apart, color, width * 0.7f, antialiased: true);
                DrawLine(start - apart, finish - apart, color, width * 0.7f, antialiased: true);
                break;
            case RelationshipKind.RivalOf or RelationshipKind.SiblingOf:
                DrawDashedLine(start, finish, color, width, 8 * _zoom, antialiased: true);
                break;
            default:
                DrawLine(start, finish, color, width, antialiased: true);
                break;
        }

        if (!tie.IsMutual)
        {
            DrawArrowHead(finish, along, color, width);
        }

        DrawTieLabel(tie, (start + finish) / 2, color);
    }

    // From a parent's bottom down to halfway, across, and down to the child's top.
    private void DrawElbow(Vector2 parent, Vector2 child, Color color, float width)
    {
        Vector2 top = parent + new Vector2(0, BoxHeight / 2 * _zoom);
        Vector2 bottom = child - new Vector2(0, BoxHeight / 2 * _zoom);
        float middle = (top.Y + bottom.Y) / 2;
        DrawPolyline([top, new Vector2(top.X, middle), new Vector2(bottom.X, middle), bottom],
            color, width, antialiased: true);
    }

    private void DrawArrowHead(Vector2 tip, Vector2 along, Color color, float width)
    {
        Vector2 side = new Vector2(-along.Y, along.X) * (4 + width);
        Vector2 back = tip - along * (10 + 2 * width);
        DrawColoredPolygon([tip, back + side, back - side], color);
    }

    // Only ties in their own words, or with words added, are labeled: the rest read from
    // their style, and labels on every line would crowd the diagram.
    private void DrawTieLabel(Relationship tie, Vector2 at, Color color)
    {
        if (tie.Label.Length == 0 || _zoom < 0.5f)
        {
            return;
        }

        Font font = GetThemeDefaultFont();
        int size = (int)(12 * Mathf.Clamp(_zoom, 0.75f, 1.5f));
        Vector2 extent = font.GetStringSize(tie.Label, HorizontalAlignment.Left, -1, size);
        var back = new Rect2(at - extent / 2 - new Vector2(4, 2), extent + new Vector2(8, 4));
        DrawRect(back, _background with { A = 0.85f });
        DrawString(font, at + new Vector2(-extent.X / 2, extent.Y / 2 - 3), tie.Label,
            HorizontalAlignment.Left, -1, size, color);
    }

    private void DrawBox(Guid entryId, Vector2 middle)
    {
        JournalEntry? entry = Session.World.Journal.FirstOrDefault(e => e.Id == entryId);
        Rect2 rect = BoxRect(middle);
        var style = new StyleBoxFlat
        {
            BgColor = LoreWords.BoxColor(entry?.Kind),
            BorderColor = entryId == _selectedEntry ? _selected : new Color(1, 1, 1, 0.25f),
        };
        style.SetCornerRadiusAll((int)(6 * _zoom));
        style.SetBorderWidthAll(entryId == _selectedEntry ? 2 : 1);
        DrawStyleBox(style, rect);

        Font font = GetThemeDefaultFont();
        if (_zoom >= 0.4f)
        {
            int small = (int)(10 * _zoom), size = (int)(14 * _zoom);
            DrawString(font, rect.Position + new Vector2(8, 4 + small),
                LoreWords.Mark(entry?.Kind), HorizontalAlignment.Left, rect.Size.X - 16, small,
                new Color(1, 1, 1, 0.55f));
            DrawString(font, rect.Position + new Vector2(8, rect.Size.Y - 12 * _zoom),
                entry?.Title ?? "(missing)", HorizontalAlignment.Left, rect.Size.X - 16, size,
                Colors.White, TextServer.JustificationFlag.None);
        }

        DrawCircle(Handle(middle), HandleRadius * Mathf.Sqrt(_zoom), _selected with { A = 0.9f });
    }

    private void DrawCentered(string text)
    {
        Font font = GetThemeDefaultFont();
        int size = GetThemeDefaultFontSize();
        Vector2 extent = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
        DrawString(font, (Size - extent) / 2 + new Vector2(0, extent.Y), text,
            HorizontalAlignment.Left, -1, size, new Color(1, 1, 1, 0.5f));
    }

    private LoreDiagram? Diagram() =>
        Session.World.Diagrams.FirstOrDefault(d => d.Id == DiagramId);

    private DiagramPlacement? BoxAt(Vector2 at) => Diagram()?.Placements.LastOrDefault(p =>
        BoxRect(ToScreen(new Vector2((float)p.X, (float)p.Y))).HasPoint(at));

    private Guid? HandleAt(Vector2 at, LoreDiagram diagram)
    {
        float reach = HandleRadius * Mathf.Sqrt(_zoom) + 3;
        return diagram.Placements.LastOrDefault(p =>
            Handle(ToScreen(new Vector2((float)p.X, (float)p.Y))).DistanceTo(at) <= reach)
            ?.EntryId;
    }

    // The tie whose line passes within a few pixels of a spot.
    private Relationship? TieAt(Vector2 at, LoreDiagram diagram)
    {
        var boxes = diagram.Placements.ToDictionary(p => p.EntryId,
            p => ToScreen(new Vector2((float)p.X, (float)p.Y)));
        foreach ((Relationship tie, int lane, int lanes) in VisibleTies(boxes.Keys))
        {
            Vector2 from = boxes[tie.FromEntryId], to = boxes[tie.ToEntryId];
            if (tie.Kind == RelationshipKind.ParentOf && lanes == 1)
            {
                float middle = (from.Y + to.Y) / 2;
                Vector2[] elbow = [from, new(from.X, middle), new(to.X, middle), to];
                if (Enumerable.Range(0, 3).Any(i => Near(at, elbow[i], elbow[i + 1])))
                {
                    return tie;
                }

                continue;
            }

            Vector2 side = Lane(tie, (to - from).Normalized(), lane, lanes);
            if (Near(at, from + side, to + side))
            {
                return tie;
            }
        }

        return null;
    }

    // How far to one side a tie's line sits, so several between one pair run side by side.
    // Measured the same way whichever end a tie runs from, so they never land on each other.
    private Vector2 Lane(Relationship tie, Vector2 along, int lane, int lanes)
    {
        float flip = tie.FromEntryId == PairKey(tie).Item1 ? 1 : -1;
        return new Vector2(-along.Y, along.X) * (flip * (lane - (lanes - 1) / 2f) * 10 * _zoom);
    }

    private static bool Near(Vector2 at, Vector2 a, Vector2 b) =>
        Geometry2D.GetClosestPointToSegment(at, a, b).DistanceTo(at) <= LineHitPixels;

    // Where the line from a box's middle toward another point leaves the box.
    private Vector2 EdgeOfBox(Vector2 middle, Vector2 toward)
    {
        Vector2 direction = toward - middle;
        if (direction.IsZeroApprox())
        {
            return middle;
        }

        float halfWidth = BoxWidth / 2 * _zoom, halfHeight = BoxHeight / 2 * _zoom;
        float scale = Math.Min(halfWidth / Math.Max(Math.Abs(direction.X), 1e-6f),
            halfHeight / Math.Max(Math.Abs(direction.Y), 1e-6f));
        return middle + direction * Math.Min(scale, 1);
    }

    private Rect2 BoxRect(Vector2 middle)
    {
        var size = new Vector2(BoxWidth, BoxHeight) * _zoom;
        return new Rect2(middle - size / 2, size);
    }

    private Vector2 Handle(Vector2 middle) => middle + new Vector2(BoxWidth / 2 * _zoom, 0);

    private Vector2 ToScreen(Vector2 spot) => Size / 2 + _pan + spot * _zoom;

    private Vector2 ToDiagram(Vector2 screen) => (screen - Size / 2 - _pan) / _zoom;

    private string Date(double days) => BodyClock.Describe(Session.SelectedBody, days);

    private static (Guid, Guid) PairKey(Relationship tie) =>
        tie.FromEntryId.CompareTo(tie.ToEntryId) < 0
            ? (tie.FromEntryId, tie.ToEntryId)
            : (tie.ToEntryId, tie.FromEntryId);
}
