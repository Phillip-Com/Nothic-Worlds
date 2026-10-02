using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Interop;
using NothicWorlds.Rendering;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// Pins on the globes for journal entries and timeline events placed at a spot (VISION.md
/// LORE-02; owner's choices: always shown, with a Pins toggle; clicking one pops up what's
/// there). Pins that land close together on screen merge into one, with a count. Choosing an
/// item in the pop-up opens it: an entry in the Journal panel, an event in its editor.
/// </summary>
/// <remarks>
/// Pins show on planets and moons drawn large enough to see a spot on, only on the side facing
/// the camera. Events on hidden timelines have no pins. Must come after the body markers in the
/// scene, so a click on a pin is taken here first.
/// </remarks>
public partial class PinMarkers : CanvasLayer
{
    private const float PinRadius = 6.0f;
    private const float MergePixels = 14.0f;
    private const float MinGlobePixels = 24.0f;
    private const float ClickSlopPixels = 5.0f;

    private static readonly Color _entryColor = new(0.96f, 0.88f, 0.66f);
    private static readonly Color _mixedColor = new(0.95f, 0.95f, 0.95f);

    // Every pinned item, worked out again only when the world changes.
    private readonly List<Pin> _pins = [];

    // Where the pins were drawn this frame, merged into groups, for clicks.
    private readonly List<(Vector2 Center, List<Pin> Pins)> _groups = [];

    private Control _overlay = null!;
    private PopupPanel _popup = null!;
    private VBoxContainer _popupList = null!;
    private (Vector2 At, List<Pin> Pins)? _pressed;
    private bool _shown = true;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The system view, for the bodies' globes.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The camera, for projecting pins onto the screen.</summary>
    [Export] public PlanetCamera? Camera { get; set; }

    /// <summary>The toolbar: pins hide whenever it does, and it opens the Journal panel.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>The Journal panel, to open a pinned entry in.</summary>
    [Export] public JournalPanel? Journal { get; set; }

    /// <summary>The timeline strip, whose editor opens a pinned event.</summary>
    [Export] public TimelineStrip? Timeline { get; set; }

    // One pinned entry or event. Exactly one of Entry and Event is set.
    private sealed record Pin(
        Guid BodyId, GeoCoordinate Spot, Color Color, JournalEntry? Entry, TimelineEvent? Event);

    /// <summary>Whether pins are shown (the toolbar's Pins toggle).</summary>
    public bool ShowPins
    {
        get => _shown;
        set
        {
            _shown = value;
            _overlay.QueueRedraw();
        }
    }

    public override void _Ready()
    {
        Layer = 0;  // Over the 3D view, under the toolbar and panels.
        _overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.Draw += DrawPins;
        AddChild(_overlay);
        _popup = new PopupPanel();
        _popup.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.09f, 0.09f, 0.11f, 0.97f),
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        });
        _popupList = new VBoxContainer();
        _popup.AddChild(_popupList);
        AddChild(_popup);

        if (Session is null || System is null || Camera is null)
        {
            GD.PushError("PinMarkers needs a world session, system view, and camera.");
            SetProcessUnhandledInput(false);
            return;
        }

        System.Placed += _overlay.QueueRedraw;
        Session.Changed += CollectPins;
        if (Toolbar is not null)
        {
            Toolbar.VisibilityChanged += () => Visible = Toolbar.Visible;
        }

        CollectPins();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !_shown
            || @event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } click)
        {
            return;
        }

        if (click.Pressed)
        {
            _pressed = GroupAt(click.Position) is List<Pin> pins ? (click.Position, pins) : null;
            if (_pressed is not null)
            {
                GetViewport().SetInputAsHandled();  // Not a camera drag or a body click.
            }

            return;
        }

        if (_pressed is (Vector2 start, List<Pin> pressedPins))
        {
            _pressed = null;
            GetViewport().SetInputAsHandled();
            if (start.DistanceTo(click.Position) <= ClickSlopPixels)
            {
                ShowPopup(click.Position, pressedPins);
            }
        }
    }

    // Gathers every pinned entry and event (events on hidden timelines aren't shown).
    private void CollectPins()
    {
        _pins.Clear();
        World world = Session!.World;
        foreach (JournalEntry entry in world.Journal)
        {
            if (entry.Location is { Pin: GeoCoordinate spot } place)
            {
                _pins.Add(new Pin(place.BodyId, spot, _entryColor, entry, null));
            }
        }

        var shownTimelines = world.Timelines.Where(t => !t.Hidden)
            .ToDictionary(t => t.Id, t => t.Color.ToGodot());
        foreach (TimelineEvent timelineEvent in world.Events)
        {
            if (timelineEvent.Location is { Pin: GeoCoordinate spot } place
                && shownTimelines.TryGetValue(timelineEvent.TimelineId, out Color color))
            {
                _pins.Add(new Pin(place.BodyId, spot, color, null, timelineEvent));
            }
        }

        _overlay.QueueRedraw();
    }

    private void DrawPins()
    {
        _groups.Clear();
        if (!Visible || !_shown || _pins.Count == 0)
        {
            return;
        }

        foreach (Pin pin in _pins)
        {
            if (ScreenPositionOf(pin) is not Vector2 at)
            {
                continue;
            }

            int group = _groups.FindIndex(g => g.Center.DistanceTo(at) <= MergePixels);
            if (group < 0)
            {
                _groups.Add((at, [pin]));
            }
            else
            {
                _groups[group].Pins.Add(pin);
            }
        }

        Font font = _overlay.GetThemeDefaultFont();
        foreach ((Vector2 center, List<Pin> pins) in _groups)
        {
            Color color = pins.All(p => p.Color == pins[0].Color) ? pins[0].Color : _mixedColor;
            DrawPin(center, color);
            if (pins.Count > 1)
            {
                string count = pins.Count.ToString();
                Vector2 at = center + new Vector2(PinRadius + 3, -PinRadius - 2);
                _overlay.DrawString(font, at + Vector2.One, count, modulate: Colors.Black);
                _overlay.DrawString(font, at, count, modulate: Colors.White);
            }
        }
    }

    // A pin: a stem down to the spot, with a round head above it.
    private void DrawPin(Vector2 spot, Color color)
    {
        Vector2 head = spot + new Vector2(0, -PinRadius * 2);
        _overlay.DrawLine(spot, head, Colors.Black, 3);
        _overlay.DrawLine(spot, head, color, 1.5f);
        _overlay.DrawCircle(head, PinRadius + 1.5f, Colors.Black);
        _overlay.DrawCircle(head, PinRadius, color);
    }

    // Where a pin's spot is on screen, or null if it can't be seen there.
    private Vector2? ScreenPositionOf(Pin pin)
    {
        if (System!.SurfaceFor(pin.BodyId) is not PlanetSurface globe
            || Camera!.IsPositionBehind(globe.GlobalPosition))
        {
            return null;
        }

        // Too small on screen to point at a spot (far away, or at true scale).
        Vector2 center = Camera.UnprojectPosition(globe.GlobalPosition);
        Vector3 edge = globe.GlobalPosition
            + Camera.GlobalBasis.X * globe.GlobalTransform.Basis.X.Length();
        if (center.DistanceTo(Camera.UnprojectPosition(edge)) < MinGlobePixels)
        {
            return null;
        }

        Vector2? at = GlobePicker.ScreenPositionOf(Camera, globe, pin.Spot);
        return at is Vector2 spot && _overlay.GetRect().Grow(20).HasPoint(spot) ? spot : null;
    }

    // The pins drawn at a screen position (the pin's head or its spot), or null.
    private List<Pin>? GroupAt(Vector2 position)
    {
        foreach ((Vector2 center, List<Pin> pins) in _groups)
        {
            Vector2 head = center + new Vector2(0, -PinRadius * 2);
            if (position.DistanceTo(head) <= PinRadius + 4
                || position.DistanceTo(center) <= PinRadius)
            {
                return pins;
            }
        }

        return null;
    }

    private void ShowPopup(Vector2 at, List<Pin> pins)
    {
        foreach (Node child in _popupList.GetChildren())
        {
            _popupList.RemoveChild(child);
            child.QueueFree();
        }

        Body body = Session!.SelectedBody;
        _popupList.AddChild(new Label
        {
            Text = PlaceText.Describe(pins[0].Spot),
            Modulate = new Color(1, 1, 1, 0.6f),
        });
        foreach (Pin pin in pins.OrderBy(p => p.Event is null ? 0 : 1)
            .ThenBy(p => p.Event?.StartDays ?? 0))
        {
            string text = pin.Entry is JournalEntry entry
                ? $"Journal: {entry.Title}"
                : $"Event: {pin.Event!.Title}, {BodyClock.Describe(body, pin.Event.StartDays)}";
            var button = new Button
            {
                Text = text,
                Alignment = HorizontalAlignment.Left,
                FocusMode = Control.FocusModeEnum.None,
            };
            button.Pressed += () =>
            {
                _popup.Hide();
                Open(pin);
            };
            _popupList.AddChild(button);
        }

        _popup.ResetSize();
        _popup.Position = (Vector2I)(at + new Vector2(12, -12));
        _popup.Popup();
    }

    private void Open(Pin pin)
    {
        if (pin.Entry is JournalEntry entry)
        {
            Toolbar?.ShowJournal();
            Journal?.SelectEntry(entry.Id);
        }
        else if (pin.Event is TimelineEvent timelineEvent)
        {
            Timeline?.EditEvent(timelineEvent);
        }
    }
}
