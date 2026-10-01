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
/// <item>Double-click a piece (or use the panel's Edit Points) to show a handle on every point
/// of its cut; dragging one stretches the image to follow. Esc leaves Edit Points.</item>
/// </list>
/// Clicks that miss every piece and handle go on to the camera, so orbiting still works.
/// Each drag is one undo step.
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
    private const float PointHandleRadius = 5.0f;
    private const int EdgeSteps = 16;
    private const double RotationSnapDegrees = 15.0;

    private static readonly Color _lineColor = new(1.0f, 0.85f, 0.2f);
    private static readonly Color _outlineColor = new(0.0f, 0.0f, 0.0f, 0.8f);

    private Control _overlay = null!;
    private Part _dragging = Part.None;
    private Part _hovered = Part.None;
    private Guid _dragId;
    private int _dragPoint;
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
        Point,
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
        // A release can be missed (e.g. the window lost focus mid-drag); don't leave the drag,
        // and its undo step, hanging.
        if (_dragging != Part.None
            && (!IsActive || !Input.IsMouseButtonPressed(MouseButton.Left)))
        {
            EndDrag();
        }

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
            return;
        }

        bool handled = @event switch
        {
            InputEventMouseButton { ButtonIndex: MouseButton.Left } button => HandleClick(button),
            InputEventMouseMotion motion => HandleMotion(motion),
            InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }
                when Panel!.SelectedPieceId is not null && _dragging == Part.None =>
                StepBack(),
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
            EndDrag();
            return wasDragging;
        }

        // Handles of the selected piece come first; they can stick out past the piece.
        if (!button.DoubleClick && SelectedPiece() is MapPiece selected
            && HandleAt(selected, button.Position) is (Part handle and not Part.None, int index))
        {
            _dragPoint = index;
            return BeginDrag(selected, handle, button.Position);
        }

        MapPiece? clicked = PieceUnder(button.Position);
        Panel!.Select(clicked?.Id);
        if (button.DoubleClick && clicked is not null)
        {
            Panel.SetEditingPoints(true);
            return true;
        }

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
        Session.BeginGesture(part switch
        {
            Part.Rotate => $"Rotate {piece.Name}",
            Part.Corner => $"Resize {piece.Name}",
            Part.Point => $"Edit Points of {piece.Name}",
            _ => $"Move {piece.Name}",
        });
        _dragId = piece.Id;
        _grabbed = grabbed;
        _startCenter = piece.Center;
        _startRotation = piece.RotationDegrees;
        _startWidth = piece.WidthDegrees;
        return true;
    }

    private void EndDrag()
    {
        if (_dragging != Part.None)
        {
            _dragging = Part.None;
            Session?.EndGesture();
        }
    }

    private bool HandleMotion(InputEventMouseMotion motion)
    {
        if (_dragging == Part.None)
        {
            SetHovered(SelectedPiece() is MapPiece piece
                ? HandleAt(piece, motion.Position).Part
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
            case Part.Point when Session.Pieces.FirstOrDefault(p => p.Id == _dragId)
                is MapPiece piece:
                MapImagePosition box = PieceProjection.For(piece).ToBoxPosition(target);
                Session.MovePiecePoint(_dragId, _dragPoint, new ImagePoint(box.U, box.V));
                break;
        }

        return true;
    }

    // Esc leaves Edit Points first, then deselects.
    private bool StepBack()
    {
        if (Panel!.IsEditingPoints)
        {
            Panel.SetEditingPoints(false);
        }
        else
        {
            Panel.Select(null);
        }

        return true;
    }

    // The topmost piece under a screen position, following warps and cut shapes.
    private MapPiece? PieceUnder(Vector2 mouse)
    {
        return GlobePicker.CoordinateAt(Camera!, Session!.Surface!, mouse) is GeoCoordinate spot
            ? PieceManipulation.PieceAt(Session.Pieces, spot, Session.WarpLookupFor)
            : null;
    }

    // Which handle of the piece (if any) is at a screen position, and for a point handle, which
    // point. Returns Body over the piece itself.
    private (Part Part, int Index) HandleAt(MapPiece piece, Vector2 mouse)
    {
        HandleLayout layout = Layout(piece);
        int nearest = -1;
        float best = GrabDistance;
        for (int i = 0; i < layout.Points.Length; i++)
        {
            if (layout.Points[i] is Vector2 point && point.DistanceTo(mouse) <= best)
            {
                best = point.DistanceTo(mouse);
                nearest = i;
            }
        }

        if (nearest >= 0)
        {
            return (Part.Point, nearest);
        }

        if (layout.Rotate is Vector2 rotate && rotate.DistanceTo(mouse) <= GrabDistance)
        {
            return (Part.Rotate, 0);
        }

        if (layout.Corners.Any(
            c => c is Vector2 corner && corner.DistanceTo(mouse) <= GrabDistance))
        {
            return (Part.Corner, 0);
        }

        return GlobePicker.CoordinateAt(Camera!, Session!.Surface!, mouse) is GeoCoordinate spot
            && PieceManipulation.PieceAt([piece], spot, Session.WarpLookupFor) is not null
            ? (Part.Body, 0)
            : (Part.None, 0);
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
            Part.Point => Input.CursorShape.Drag,
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

        foreach (Vector2? point in layout.Points)
        {
            if (point is Vector2 position)
            {
                _overlay.DrawCircle(position, PointHandleRadius + 1.5f, _outlineColor);
                _overlay.DrawCircle(position, PointHandleRadius, _lineColor);
            }
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

    // Where the selected piece's handles are on screen. Normally that's a frame around the
    // piece (around its stretched shape, if it's warped) with corner and rotate handles; in Edit
    // Points, it's the cut's outline with a handle on every point. Parts on the far side of the
    // planet are left out.
    private HandleLayout Layout(MapPiece piece)
    {
        var projection = PieceProjection.For(piece);
        if (Panel!.IsEditingPoints)
        {
            IReadOnlyList<ImagePoint> points =
                piece.WarpedPoints ?? PieceWarp.UnwarpedPoints(piece.Outline);
            Vector2?[] pointHandles =
                [.. points.Select(p => ToScreen(projection.FromBoxPosition(p.U, p.V)))];
            return new HandleLayout(
                OutlineOnScreen(projection, points), [], null, null, pointHandles);
        }

        (double left, double top, double right, double bottom) =
            Session!.WarpLookupFor(piece) is WarpLookup warp
                ? (warp.MinU, warp.MinV, warp.MaxU, warp.MaxV)
                : (0, 0, 1, 1);
        ImagePoint[] corners =
            [new(left, top), new(right, top), new(right, bottom), new(left, bottom)];
        Vector2?[] cornerPoints =
            [.. corners.Select(c => ToScreen(projection.FromBoxPosition(c.U, c.V)))];

        // The rotate handle sits a fixed distance beyond the top edge's middle, on screen.
        Vector2? topMiddle = ToScreen(projection.FromBoxPosition((left + right) / 2, top));
        Vector2? center = ToScreen(piece.Center);
        Vector2? rotate = null;
        if (topMiddle is Vector2 topPoint)
        {
            Vector2 outward = center is Vector2 middle && topPoint.DistanceTo(middle) > 0.5f
                ? (topPoint - middle).Normalized()
                : Vector2.Up;
            rotate = topPoint + outward * RotateHandleOffset;
        }

        return new HandleLayout(
            OutlineOnScreen(projection, corners), cornerPoints, topMiddle, rotate, []);
    }

    // A closed outline of box positions, drawn along the globe: each side is split into steps
    // so it curves with the surface, and broken where it goes behind the planet.
    private List<Vector2[]> OutlineOnScreen(
        PieceProjection projection, IReadOnlyList<ImagePoint> outline)
    {
        int steps = outline.Count > 32 ? 2 : EdgeSteps;  // Long outlines are smooth already.
        var lines = new List<Vector2[]>();
        var run = new List<Vector2>();
        for (int i = 0; i < outline.Count; i++)
        {
            ImagePoint from = outline[i];
            ImagePoint to = outline[(i + 1) % outline.Count];
            for (int step = i == 0 ? 0 : 1; step <= steps; step++)
            {
                double t = (double)step / steps;
                Vector2? point = ToScreen(projection.FromBoxPosition(
                    from.U + (to.U - from.U) * t, from.V + (to.V - from.V) * t));
                if (point is Vector2 visible)
                {
                    run.Add(visible);
                }
                else if (run.Count > 0)
                {
                    lines.Add([.. run]);
                    run.Clear();
                }
            }
        }

        if (run.Count > 0)
        {
            lines.Add([.. run]);
        }

        return lines;
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
        List<Vector2[]> Edges,
        Vector2?[] Corners,
        Vector2? TopMiddle,
        Vector2? Rotate,
        Vector2?[] Points);
}
