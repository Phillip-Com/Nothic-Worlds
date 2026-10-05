using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Interop;
using NothicWorlds.Rendering;
using NothicWorlds.Session;
using NothicWorlds.UI;

namespace NothicWorlds.Controls;

/// <summary>
/// Placing and shaping shapes on the selected globe (VISION.md BOD-04; owner's choice: click to
/// place, drag handles to move, resize, and turn), while the Terrain panel is in Shapes mode:
/// <list type="bullet">
/// <item>Click the planet to place a new shape there (the panel picks which kind, and whether it
/// adds or cuts); it's then selected.</item>
/// <item>Drag the selected shape's middle to move it, its square to resize it (it keeps its
/// proportions), and its round handle to turn it (hold Shift to snap to 15°).</item>
/// <item>Press Esc to deselect.</item>
/// </list>
/// While a handle is dragged the shape shows as a see-through preview; the world is carved when
/// it's let go (owner's choice: carving takes a moment), as one undo step. Drags that start off
/// the handles go on to the camera, so orbiting still works.
/// </summary>
/// <remarks>
/// Must come after the camera in the scene: later nodes get unhandled input first.
/// </remarks>
public partial class ShapeHandles : CanvasLayer
{
    private const float HandleSize = 10.0f;
    private const float GrabDistance = 12.0f;
    private const float MinHandleSpread = 28.0f;  // Keeps a small shape's handles apart
    private const float ClickSlopPixels = 5.0f;
    private const double TurnHandleBeyond = 1.4;  // The turn handle, past the shape's end
    private const double TurnSnapDegrees = 15.0;
    private const double CarvingToleranceOfRadius = 0.001;  // Clicks this near count as ground

    private static readonly Color _lineColor = new(1.0f, 0.85f, 0.2f);
    private static readonly Color _outlineColor = new(0.0f, 0.0f, 0.0f, 0.8f);

    private Control _overlay = null!;
    private bool _active;
    private Guid? _selected;
    private Part _dragging = Part.None;
    private ShapeEdit? _dragged;
    private ShapeEdit? _startShape;
    private Vector2? _pressedAt;
    private float _grabbedFromMiddle;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The system view, for the globe.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The camera, for turning mouse positions into spots on the globe.</summary>
    [Export] public PlanetCamera? Camera { get; set; }

    /// <summary>The toolbar, for saying why a shape couldn't be placed or changed.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>Raised when another shape (or none) is selected.</summary>
    public event Action? SelectionChanged;

    private enum Part
    {
        None,
        Move,
        Size,
        Turn,
    }

    /// <summary>The kind of shape a click places.</summary>
    public ShapeKind PlaceKind { get; set; } = ShapeKind.Box;

    /// <summary>Whether a placed shape adds or cuts.</summary>
    public ShapeOperation PlaceOperation { get; set; } = ShapeOperation.Add;

    /// <summary>
    /// Whether the handles work (the Terrain panel turns them on in Shapes mode).
    /// </summary>
    public bool IsActive
    {
        get => _active;
        set
        {
            _active = value;
            if (!value)
            {
                CancelDrag();
                _overlay?.QueueRedraw();  // Clears the handles.
            }
        }
    }

    /// <summary>The selected shape on the selected body, or null.</summary>
    public Guid? SelectedShapeId
    {
        get => _selected;
        set
        {
            if (value != _selected)
            {
                CancelDrag();
                _selected = value;
                SelectionChanged?.Invoke();
            }
        }
    }

    public override void _Ready()
    {
        // Under the toolbar and panels, over the 3D view.
        Layer = 0;
        _overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.Draw += DrawHandles;
        AddChild(_overlay);
        if (Session is null || System is null || Camera is null)
        {
            GD.PushError("ShapeHandles needs a world session, system view, and camera.");
            SetProcessUnhandledInput(false);
            SetProcess(false);
            return;
        }

        Session.SelectionChanged += () => SelectedShapeId = null;
        Session.WorldClosed += _ => SelectedShapeId = null;
    }

    public override void _Process(double delta)
    {
        // A release can be missed (the window lost focus mid-drag): don't leave a drag hanging.
        if (_dragging != Part.None && (!_active || !Input.IsMouseButtonPressed(MouseButton.Left)))
        {
            EndDrag();
        }

        // The globe turns and the camera eases, so the handles are redrawn every frame.
        if (_active)
        {
            _overlay.QueueRedraw();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_active || Session is not { SelectedBodyCanBeSculpted: true, IsBusy: false })
        {
            return;
        }

        bool handled = @event switch
        {
            InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press =>
                Press(press.Position),
            InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release =>
                Release(release.Position),
            InputEventMouseMotion motion when _dragging != Part.None =>
                Drag(motion.Position, motion.ShiftPressed),
            InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }
                when _selected is not null && _dragging == Part.None => Deselect(),
            _ => false,
        };
        if (handled)
        {
            GetViewport().SetInputAsHandled();
        }
    }

    // A press on a handle starts dragging it; elsewhere it may be a click that places a shape,
    // so it's noted and passed on (a drag there turns the camera).
    private bool Press(Vector2 mouse)
    {
        if (Selected is ShapeEdit shape && PartAt(shape, mouse) is var part && part != Part.None)
        {
            _grabbedFromMiddle = ScreenAt(shape.Spot) is Vector2 middle
                ? Math.Max(middle.DistanceTo(mouse), 1) : 1;
            _dragging = part;
            _startShape = shape;
            _dragged = shape;
            return true;
        }

        _pressedAt = mouse;
        return false;
    }

    private bool Release(Vector2 mouse)
    {
        if (_dragging != Part.None)
        {
            EndDrag();
            return true;
        }

        bool click = _pressedAt is Vector2 pressed && pressed.DistanceTo(mouse) <= ClickSlopPixels;
        _pressedAt = null;
        if (!click || Globe is not PlanetSurface globe
            || GlobePicker.PointAt(Camera!, globe, mouse) is not Vector3 point)
        {
            return false;
        }

        // On a carved globe the click may land in a hole or hollow: the shape goes there. (The
        // carving's flat facets sit a little off the round ground, so near it, the ground.)
        GeoCoordinate spot = SphericalCoordinates.FromDirection(
            new System.Numerics.Vector3(point.X, point.Y, point.Z));
        double radiusKm = Session!.SelectedBody.RadiusKm;
        double? groundKm = globe.IsCarved ? globe.TrueHeightKmOf(point, radiusKm) : null;
        if (groundKm is double clicked
            && Math.Abs(clicked - GroundKm(spot)) < CarvingToleranceOfRadius * radiusKm)
        {
            groundKm = null;
        }

        (ShapeEdit? added, string? problem) =
            Session!.AddShape(spot, PlaceKind, PlaceOperation, groundKm);
        if (added is not null)
        {
            SelectedShapeId = added.Id;
        }
        else if (problem is not null)
        {
            Toolbar?.ShowError($"Can't place a shape: {problem}.");
        }

        return true;
    }

    // Moves, resizes, or turns the dragged shape; shown as a preview until it's let go.
    private bool Drag(Vector2 mouse, bool snap)
    {
        if (_startShape is not ShapeEdit start)
        {
            return true;
        }

        ShapeEdit? changed = _dragging == Part.Size
            ? Resized(start, mouse)
            : SpotAt(mouse) is not GeoCoordinate spot ? null
            : _dragging == Part.Move ? Moved(start, spot)
            : Turned(start, spot, snap);
        if (changed is null)
        {
            return true;
        }

        _dragged = changed;
        Globe?.SetShapePreview(_dragged);
        return true;
    }

    // Keeps the shape's height above the ground as it moves onto higher or lower ground.
    private ShapeEdit Moved(ShapeEdit shape, GeoCoordinate spot) => shape with
    {
        Spot = spot,
        DepthKm = shape.DepthKm - GroundKm(shape.Spot) + GroundKm(spot),
    };

    // Scales the whole shape, keeping its proportions and how it sits on the ground, by how much
    // farther from its middle the mouse is than where the handle was grabbed. (The handle may
    // have been pushed out past the shape's edge, so the distance on the globe won't do.)
    private ShapeEdit? Resized(ShapeEdit shape, Vector2 mouse)
    {
        if (ScreenAt(shape.Spot) is not Vector2 middle)
        {
            return null;
        }

        double radiusKm = Session!.SelectedBody.RadiusKm;
        double scale = Math.Clamp(middle.DistanceTo(mouse) / _grabbedFromMiddle, 1 / shape.WidthKm,
            ShapeEdit.MaxRadii * radiusKm / Math.Max(shape.WidthKm,
                Math.Max(shape.HeightKm, shape.LengthKm)));
        double ground = GroundKm(shape.Spot);
        return shape with
        {
            WidthKm = shape.WidthKm * scale,
            HeightKm = shape.HeightKm * scale,
            LengthKm = shape.LengthKm * scale,
            DepthKm = ground + (shape.DepthKm - ground) * scale,
        };
    }

    // Points the shape's length toward the mouse (clockwise from north).
    private ShapeEdit Turned(ShapeEdit shape, GeoCoordinate spot, bool snap)
    {
        ShapeFrame north = (shape with { TurnDegrees = 0 }).FrameOn(1);
        Vector3D toward = Direction(spot) - north.Up * Direction(spot).Dot(north.Up);
        double turn = double.RadiansToDegrees(
            Math.Atan2(toward.Dot(north.Across), toward.Dot(north.Along)));
        if (snap)
        {
            turn = Math.Round(turn / TurnSnapDegrees) * TurnSnapDegrees;
        }

        return shape with { TurnDegrees = (turn + 360) % 360 };
    }

    private void EndDrag()
    {
        if (_dragging == Part.None)
        {
            return;
        }

        ShapeEdit? dragged = _dragged;
        CancelDrag();
        if (dragged is not null && dragged != _startShape && Session?.UpdateShape(
            dragged, mergeWithLast: false) is string problem)
        {
            Toolbar?.ShowError($"Can't use that: {problem}.");
        }
    }

    private void CancelDrag()
    {
        _dragging = Part.None;
        _dragged = null;
        _startShape = null;
        Globe?.SetShapePreview(null);
    }

    private bool Deselect()
    {
        SelectedShapeId = null;
        return true;
    }

    private void DrawHandles()
    {
        if (!_active || (_dragged ?? Selected) is not ShapeEdit shape || Globe is null)
        {
            return;
        }

        (Vector2? middle, Vector2? size, Vector2? turn) = HandlePositions(shape);
        if (middle is Vector2 at && turn is Vector2 knob)
        {
            _overlay.DrawLine(at, knob, _outlineColor, 3.0f, antialiased: true);
            _overlay.DrawLine(at, knob, _lineColor, 1.5f, antialiased: true);
        }

        foreach (Vector2? point in new[] { middle, turn })
        {
            if (point is Vector2 center)
            {
                _overlay.DrawCircle(center, HandleSize / 2 + 1.5f, _outlineColor);
                _overlay.DrawCircle(center, HandleSize / 2, _lineColor);
            }
        }

        if (size is Vector2 corner)
        {
            var box = new Rect2(corner - Vector2.One * HandleSize / 2, Vector2.One * HandleSize);
            _overlay.DrawRect(box.Grow(1.5f), _outlineColor);
            _overlay.DrawRect(box, _lineColor);
        }
    }

    // Where the handles are on screen: the shape's middle, the edge of its width (resize), and
    // a little past the end of its length (turn), those two kept a grabbable distance out.
    private (Vector2? Middle, Vector2? Size, Vector2? Turn) HandlePositions(ShapeEdit shape)
    {
        double radiusKm = Session!.SelectedBody.RadiusKm;
        ShapeFrame frame = shape.FrameOn(radiusKm);
        double halfLength = (shape.Kind == ShapeKind.Box ? shape.LengthKm : shape.WidthKm) / 2;
        Vector3D sizeAt = frame.Up + frame.Across * (shape.WidthKm / 2 / radiusKm);
        Vector3D turnAt = frame.Up + frame.Along * (halfLength * TurnHandleBeyond / radiusKm);
        Vector2? middle = ScreenAt(shape.Spot);
        return (middle, Spread(middle, ScreenAt(ToSpot(sizeAt))),
            Spread(middle, ScreenAt(ToSpot(turnAt))));
    }

    private static Vector2? Spread(Vector2? middle, Vector2? handle)
    {
        if (middle is not Vector2 from || handle is not Vector2 to
            || from.DistanceTo(to) >= MinHandleSpread || from.DistanceTo(to) < 0.001f)
        {
            return handle;
        }

        return from + (to - from).Normalized() * MinHandleSpread;
    }

    private Part PartAt(ShapeEdit shape, Vector2 mouse)
    {
        (Vector2? middle, Vector2? size, Vector2? turn) = HandlePositions(shape);
        foreach ((Vector2? at, Part part) in new[]
            { (size, Part.Size), (turn, Part.Turn), (middle, Part.Move) })
        {
            if (at is Vector2 point && point.DistanceTo(mouse) <= GrabDistance)
            {
                return part;
            }
        }

        return Part.None;
    }

    private ShapeEdit? Selected => Session?.SelectedBody.Surface.Shapes
        .FirstOrDefault(shape => shape.Id == _selected);

    private PlanetSurface? Globe => System?.SurfaceFor(Session?.SelectedBodyId ?? Guid.Empty);

    private double GroundKm(GeoCoordinate spot) => Session!.HeightAt(spot) / 1000;

    private GeoCoordinate? SpotAt(Vector2 mouse) =>
        Globe is PlanetSurface globe ? GlobePicker.CoordinateAt(Camera!, globe, mouse) : null;

    private Vector2? ScreenAt(GeoCoordinate spot) =>
        Globe is PlanetSurface globe ? GlobePicker.ScreenPositionOf(Camera!, globe, spot) : null;

    private static Vector3D Direction(GeoCoordinate spot) =>
        SphericalCoordinates.ToDirection(spot).ToVector3D();

    private static GeoCoordinate ToSpot(Vector3D direction) =>
        SphericalCoordinates.FromDirection(new System.Numerics.Vector3(
            (float)direction.X, (float)direction.Y, (float)direction.Z));
}
