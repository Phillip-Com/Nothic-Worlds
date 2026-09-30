using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Model;
using NothicWorlds.Session;
using NothicWorlds.UI;

namespace NothicWorlds.Controls;

/// <summary>
/// Moving, resizing, and rotating map pieces directly on the globe (VISION.md MAP-02), like
/// stamps in other map makers. Works while the Pieces panel is open:
/// <list type="bullet">
/// <item>Click a piece to select it; drag it to move it.</item>
/// <item>Drag a corner square to resize it (it keeps its proportions).</item>
/// <item>Drag the round handle above it to rotate it (hold Shift to snap to 15°).</item>
/// <item>Click empty space, or press Esc, to deselect.</item>
/// </list>
/// Clicks that miss every piece and handle go on to the camera, so orbiting still works.
/// </summary>
/// <remarks>
/// Must come after the camera in the scene: later nodes get unhandled input first.
/// </remarks>
public partial class PieceHandles : CanvasLayer
{
    private const float HandleSize = 10.0f;
    private const float GrabDistance = 12.0f;
    private const float RotateHandleOffset = 32.0f;
    private const float RotateHandleRadius = 6.0f;
    private const int EdgeSteps = 16;
    private const double RotationSnapDegrees = 15.0;

    private static readonly Color _lineColor = new(1.0f, 0.85f, 0.2f);
    private static readonly Color _outlineColor = new(0.0f, 0.0f, 0.0f, 0.8f);

    private Control _overlay = null!;
    private Part _dragging = Part.None;
    private Part _hovered = Part.None;
    private Guid _dragId;
    private GeoCoordinate _grabbed;
    private GeoCoordinate _startCenter;
    private double _startRotation;
    private double _startWidth;

    /// <summary>The open world whose pieces are edited.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The camera, for turning mouse positions into globe positions.</summary>
    [Export] public PlanetCamera? Camera { get; set; }

    /// <summary>The Pieces panel, which holds the selection. Handles work while it shows.</summary>
    [Export] public PiecesPanel? Panel { get; set; }

    private enum Part
    {
        None,
        Body,
        Corner,
        Rotate,
    }

    // Handles work only while the Pieces panel shows (it hides during calibration and cutting).
    private bool IsActive => Panel is { Visible: true } && Session is { IsBusy: false };

    public override void _Ready()
    {
        // Under the toolbar and panels (layer 1), over the 3D view.
        Layer = 0;
        _overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.Draw += DrawHandles;
        AddChild(_overlay);

        if (Session is null || Camera is null || Panel is null)
        {
            GD.PushError("PieceHandles needs a world session, camera, and Pieces panel.");
            SetProcessUnhandledInput(false);
            SetProcess(false);
        }
    }

    public override void _Process(double delta)
    {
        // The camera may be easing, so handles are redrawn every frame while something's
        // selected (cheap: a few dozen points).
        _overlay.QueueRedraw();

        // The cursor shape is app-wide, so put it back when the mouse moves onto a panel.
        bool overInterface = GetViewport().GuiGetHoveredControl() is not null;
        if ((!IsActive || overInterface) && _hovered != Part.None && _dragging == Part.None)
        {
            SetHovered(Part.None);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsActive)
        {
            _dragging = Part.None;
            return;
        }

        bool handled = @event switch
        {
            InputEventMouseButton { ButtonIndex: MouseButton.Left } button => HandleClick(button),
            InputEventMouseMotion motion => HandleMotion(motion),
            InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }
                when Panel!.SelectedPieceId is not null && _dragging == Part.None =>
                Deselect(),
            _ => false,
        };

        if (handled)
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private bool HandleClick(InputEventMouseButton button)
    {
        if (!button.Pressed)
        {
            bool wasDragging = _dragging != Part.None;
            _dragging = Part.None;
            return wasDragging;
        }

        // Handles of the selected piece come first; they can stick out past the piece.
        if (SelectedPiece() is MapPiece selected
            && HandleAt(selected, button.Position) is Part handle and not Part.None)
        {
            return BeginDrag(selected, handle, button.Position);
        }

        GeoCoordinate? point =
            GlobePicker.CoordinateAt(Camera!, Session!.Surface!, button.Position);
        MapPiece? clicked = point is GeoCoordinate spot
            ? PieceManipulation.PieceAt(Session.Pieces, spot)
            : null;
        Panel!.Select(clicked?.Id);
        return clicked is not null && BeginDrag(clicked, Part.Body, button.Position);
    }

    private bool BeginDrag(MapPiece piece, Part part, Vector2 mouse)
    {
        if (GlobePicker.CoordinateAt(Camera!, Session!.Surface!, mouse, nearestWhenMissed: true)
            is not GeoCoordinate grabbed)
        {
            return false;
        }

        _dragging = part;
        _dragId = piece.Id;
        _grabbed = grabbed;
        _startCenter = piece.Center;
        _startRotation = piece.RotationDegrees;
        _startWidth = piece.WidthDegrees;
        return true;
    }

    private bool HandleMotion(InputEventMouseMotion motion)
    {
        if (_dragging == Part.None)
        {
            SetHovered(SelectedPiece() is MapPiece piece
                ? HandleAt(piece, motion.Position)
                : Part.None);
            return false;
        }

        if (GlobePicker.CoordinateAt(Camera!, Session!.Surface!, motion.Position,
            nearestWhenMissed: true) is not GeoCoordinate target)
        {
            return true;
        }

        switch (_dragging)
        {
            case Part.Body:
                (GeoCoordinate center, double rotation) = PieceManipulation.Move(
                    _startCenter, _startRotation, _grabbed, target);
                Session.PlacePiece(_dragId, center, rotation, _startWidth);
                break;
            case Part.Rotate:
                double turned = PieceManipulation.Rotate(
                    _startCenter, _startRotation, _grabbed, target);
                if (motion.ShiftPressed)
                {
                    turned = Math.Round(turned / RotationSnapDegrees) * RotationSnapDegrees;
                }

                Session.PlacePiece(_dragId, _startCenter, turned, _startWidth);
                break;
            case Part.Corner:
                double width = PieceManipulation.Resize(
                    _startCenter, _startWidth, _grabbed, target);
                Session.PlacePiece(_dragId, _startCenter, _startRotation, width);
                break;
        }

        return true;
    }

    private bool Deselect()
    {
        Panel!.Select(null);
        return true;
    }

    // Which handle of the piece (if any) is at a screen position. Returns Body over the piece.
    private Part HandleAt(MapPiece piece, Vector2 mouse)
    {
        HandleLayout layout = Layout(piece);
        if (layout.Rotate is Vector2 rotate && rotate.DistanceTo(mouse) <= GrabDistance)
        {
            return Part.Rotate;
        }

        if (layout.Corners.Any(
            c => c is Vector2 corner && corner.DistanceTo(mouse) <= GrabDistance))
        {
            return Part.Corner;
        }

        return GlobePicker.CoordinateAt(Camera!, Session!.Surface!, mouse) is GeoCoordinate spot
            && PieceManipulation.PieceAt([piece], spot) is not null
            ? Part.Body
            : Part.None;
    }

    private void SetHovered(Part part)
    {
        if (part == _hovered)
        {
            return;
        }

        _hovered = part;
        Input.SetDefaultCursorShape(part switch
        {
            Part.Body => Input.CursorShape.Move,
            Part.Corner => Input.CursorShape.Fdiagsize,
            Part.Rotate => Input.CursorShape.PointingHand,
            _ => Input.CursorShape.Arrow,
        });
    }

    private void DrawHandles()
    {
        if (!IsActive || SelectedPiece() is not MapPiece piece)
        {
            return;
        }

        HandleLayout layout = Layout(piece);
        foreach (Vector2[] edge in layout.Edges)
        {
            DrawLine(edge);
        }

        if (layout.Rotate is Vector2 rotate && layout.TopMiddle is Vector2 top)
        {
            DrawLine([top, rotate]);
            _overlay.DrawCircle(rotate, RotateHandleRadius + 1.5f, _outlineColor);
            _overlay.DrawCircle(rotate, RotateHandleRadius, _lineColor);
        }

        foreach (Vector2? corner in layout.Corners)
        {
            if (corner is Vector2 position)
            {
                var square = new Rect2(position - Vector2.One * HandleSize / 2,
                    Vector2.One * HandleSize);
                _overlay.DrawRect(square.Grow(1.5f), _outlineColor);
                _overlay.DrawRect(square, _lineColor);
            }
        }
    }

    private void DrawLine(Vector2[] points)
    {
        if (points.Length >= 2)
        {
            _overlay.DrawPolyline(points, _outlineColor, 3.5f, antialiased: true);
            _overlay.DrawPolyline(points, _lineColor, 1.5f, antialiased: true);
        }
    }

    // Where the selected piece's box edges, corners, and rotate handle are on screen. Parts on
    // the far side of the planet are left out.
    private HandleLayout Layout(MapPiece piece)
    {
        var projection = PieceProjection.For(piece);
        (double U, double V)[] corners = [(0, 0), (1, 0), (1, 1), (0, 1)];

        var edges = new List<Vector2[]>();
        for (int i = 0; i < corners.Length; i++)
        {
            (double U, double V) from = corners[i];
            (double U, double V) to = corners[(i + 1) % corners.Length];
            var run = new List<Vector2>();
            for (int step = 0; step <= EdgeSteps; step++)
            {
                double t = (double)step / EdgeSteps;
                Vector2? point = ToScreen(projection.FromBoxPosition(
                    from.U + (to.U - from.U) * t, from.V + (to.V - from.V) * t));
                if (point is Vector2 visible)
                {
                    run.Add(visible);
                }
                else if (run.Count > 0)
                {
                    edges.Add([.. run]);  // The edge goes behind the planet here.
                    run.Clear();
                }
            }

            if (run.Count > 0)
            {
                edges.Add([.. run]);
            }
        }

        Vector2?[] cornerPoints =
            [.. corners.Select(c => ToScreen(projection.FromBoxPosition(c.U, c.V)))];

        // The rotate handle sits a fixed distance beyond the top edge's middle, on screen.
        Vector2? topMiddle = ToScreen(projection.FromBoxPosition(0.5, 0));
        Vector2? center = ToScreen(piece.Center);
        Vector2? rotate = null;
        if (topMiddle is Vector2 top)
        {
            Vector2 outward = center is Vector2 middle && top.DistanceTo(middle) > 0.5f
                ? (top - middle).Normalized()
                : Vector2.Up;
            rotate = top + outward * RotateHandleOffset;
        }

        return new HandleLayout(edges, cornerPoints, topMiddle, rotate);
    }

    private Vector2? ToScreen(GeoCoordinate coordinate)
    {
        return GlobePicker.ScreenPositionOf(Camera!, Session!.Surface!, coordinate);
    }

    private MapPiece? SelectedPiece()
    {
        return Panel?.SelectedPieceId is Guid id
            ? Session?.Pieces.FirstOrDefault(p => p.Id == id)
            : null;
    }

    private sealed record HandleLayout(
        List<Vector2[]> Edges, Vector2?[] Corners, Vector2? TopMiddle, Vector2? Rotate);
}
