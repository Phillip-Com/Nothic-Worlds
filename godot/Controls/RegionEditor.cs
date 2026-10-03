using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Rendering;
using NothicWorlds.Session;
using NothicWorlds.UI;

namespace NothicWorlds.Controls;

/// <summary>
/// Draws and reshapes region outlines on the selected globe (VISION.md LORE-01; owner's choice:
/// click points, then adjust).
/// <list type="bullet">
/// <item><b>Drawing:</b> each click on the globe adds a corner. Enter, or clicking the first
/// corner, finishes (at least three); Backspace removes the last corner; Esc cancels.</item>
/// <item><b>Edit Points:</b> drag a corner to move it, drag the small handle in the middle of
/// an edge to add a corner there, right-click a corner to delete it (keeping at least three).
/// Each drag is one undo step. Esc stops editing.</item>
/// </list>
/// Clicks that miss the globe and its handles pass through, so the camera still turns.
/// </summary>
/// <remarks>
/// Must come last in the scene: later nodes get unhandled input first, and while drawing or
/// editing, this one takes the clicks on its globe.
/// </remarks>
public partial class RegionEditor : CanvasLayer
{
    private const float HandleRadius = 6.0f;
    private const float MidHandleRadius = 4.0f;
    private const float GrabPixels = 10.0f;

    private static readonly Color _handleColor = new(1, 1, 1);
    private static readonly Color _lineColor = new(1.0f, 0.9f, 0.5f);

    private readonly List<GeoCoordinate> _drawing = [];
    private Control _overlay = null!;
    private Guid? _drawingBodyId;
    private Guid? _editingRegionId;
    private int? _dragging;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The system view, for the globe.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The camera, for turning clicks into spots on the globe.</summary>
    [Export] public PlanetCamera? Camera { get; set; }

    /// <summary>Where hints go; the editor hides whenever the toolbar does.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>Raised when a new region has been drawn (with its ID).</summary>
    public event Action<Guid>? RegionDrawn;

    /// <summary>Raised when drawing or editing stops (finished, cancelled, or Esc).</summary>
    public event Action? Stopped;

    /// <summary>The region whose points are being edited, or null.</summary>
    public Guid? EditingRegionId => _editingRegionId;

    /// <summary>True while a new outline is being drawn.</summary>
    public bool IsDrawing => _drawingBodyId is not null;

    public override void _Ready()
    {
        Layer = 0;
        _overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.Draw += DrawHandles;
        AddChild(_overlay);
        if (Session is null || System is null || Camera is null)
        {
            GD.PushError("RegionEditor needs a world session, system view, and camera.");
            return;
        }

        // Only while drawing or editing: nothing is drawn otherwise.
        System.Placed += () =>
        {
            if (IsDrawing || _editingRegionId is not null)
            {
                _overlay.QueueRedraw();
            }
        };
        Session.Changed += () =>
        {
            if (_editingRegionId is Guid id && Session.World.Regions.All(r => r.Id != id))
            {
                Stop();  // The region was deleted (or undone away).
            }

            _overlay.QueueRedraw();
        };
        Session.SelectionChanged += Stop;
        Session.WorldClosed += _ => Stop();
        if (Toolbar is not null)
        {
            Toolbar.VisibilityChanged += () => Visible = Toolbar.Visible;
        }
    }

    /// <summary>Starts drawing a new region on the selected body (a planet or moon).</summary>
    public void StartDrawing()
    {
        Stop();
        if (Session?.SelectedBody is not { HasSurface: true } body)
        {
            return;
        }

        _drawingBodyId = body.Id;
        Toolbar?.ShowInfo($"Click corners around the region on {body.Name}. Enter or the first " +
            "corner finishes; Backspace undoes a corner; Esc cancels.", autoHide: false);
        _overlay.QueueRedraw();
    }

    /// <summary>Starts editing a region's points (it must be on the selected body).</summary>
    public void StartEditing(Guid regionId)
    {
        Stop();
        if (Session?.World.Regions.FirstOrDefault(r => r.Id == regionId) is not Region region
            || region.BodyId != Session.SelectedBodyId)
        {
            return;
        }

        _editingRegionId = regionId;
        Toolbar?.ShowInfo("Drag a corner to move it, drag a middle handle to add one, " +
            "right-click a corner to delete it. Esc stops.", autoHide: false);
        _overlay.QueueRedraw();
    }

    /// <summary>Stops drawing or editing (a new outline in progress is dropped).</summary>
    public void Stop()
    {
        bool wasActive = IsDrawing || _editingRegionId is not null;
        EndDrag();
        _drawing.Clear();
        _drawingBodyId = null;
        _editingRegionId = null;
        _overlay?.QueueRedraw();
        if (wasActive)
        {
            Toolbar?.ShowInfo("");
            Stopped?.Invoke();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || (!IsDrawing && _editingRegionId is null))
        {
            return;
        }

        bool handled = @event switch
        {
            InputEventKey { Pressed: true, Echo: false } key => HandleKey(key.Keycode),
            InputEventMouseButton { ButtonIndex: MouseButton.Left } click when IsDrawing =>
                click.Pressed && AddCorner(click.Position),
            InputEventMouseButton { ButtonIndex: MouseButton.Left } click =>
                HandleEditClick(click),
            InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } click =>
                !IsDrawing && DeleteCornerAt(click.Position),
            InputEventMouseMotion motion when _dragging is int corner =>
                DragCorner(corner, motion.Position),
            _ => false,
        };
        if (handled)
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private bool HandleKey(Key key)
    {
        switch (key)
        {
            case Key.Escape:
                Stop();
                return true;
            case Key.Enter or Key.KpEnter when IsDrawing:
                Finish();
                return true;
            case Key.Backspace when IsDrawing && _drawing.Count > 0:
                _drawing.RemoveAt(_drawing.Count - 1);
                _overlay.QueueRedraw();
                return true;
            default:
                return false;
        }
    }

    // Drawing: a click on the first corner finishes; elsewhere on the globe it adds a corner.
    private bool AddCorner(Vector2 mouse)
    {
        if (_drawing.Count >= 3 && ScreenOf(_drawing[0]) is Vector2 first
            && first.DistanceTo(mouse) <= GrabPixels)
        {
            Finish();
            return true;
        }

        if (SpotAt(mouse, nearestWhenMissed: false) is not GeoCoordinate spot)
        {
            return false;  // Missed the globe: let the camera have it.
        }

        _drawing.Add(spot);
        _overlay.QueueRedraw();
        return true;
    }

    private void Finish()
    {
        if (_drawingBodyId is not Guid bodyId || _drawing.Count < 3)
        {
            Toolbar?.ShowWarning("A region needs at least three corners.");
            return;
        }

        (Region? region, string? problem) = Session!.AddRegion(bodyId, _drawing);
        if (region is null)
        {
            Toolbar?.ShowWarning($"Couldn't add the region: {problem}.");
            return;
        }

        _drawing.Clear();
        _drawingBodyId = null;
        Toolbar?.ShowInfo($"Added {region.Name}.");
        RegionDrawn?.Invoke(region.Id);
        Stopped?.Invoke();
        _overlay.QueueRedraw();
    }

    // Editing: press on a corner to drag it, on a middle handle to add a corner and drag it.
    private bool HandleEditClick(InputEventMouseButton click)
    {
        if (!click.Pressed)
        {
            bool wasDragging = _dragging is not null;
            EndDrag();
            return wasDragging;
        }

        if (EditedRegion() is not Region region)
        {
            return false;
        }

        if (CornerAt(region, click.Position) is int corner)
        {
            StartDrag(region, corner);
            return true;
        }

        if (MiddleAt(region, click.Position) is int edge
            && SpotAt(click.Position, nearestWhenMissed: true) is GeoCoordinate spot)
        {
            Session!.BeginGesture($"Edit {region.Name}'s Outline");
            List<GeoCoordinate> corners = [.. region.Corners];
            corners.Insert(edge + 1, spot);
            if (Session.UpdateRegion(region with { Corners = corners }) is null)
            {
                _dragging = edge + 1;
                return true;
            }

            Session.EndGesture();
        }

        return false;
    }

    private void StartDrag(Region region, int corner)
    {
        Session!.BeginGesture($"Edit {region.Name}'s Outline");
        _dragging = corner;
    }

    private bool DragCorner(int corner, Vector2 mouse)
    {
        if (EditedRegion() is not Region region || corner >= region.Corners.Count
            || SpotAt(mouse, nearestWhenMissed: true) is not GeoCoordinate spot)
        {
            return true;
        }

        List<GeoCoordinate> corners = [.. region.Corners];
        corners[corner] = spot;
        string? problem = Session!.UpdateRegion(region with { Corners = corners });
        if (problem is not null)
        {
            Toolbar?.ShowWarning($"Can't move it there: {problem}.");
        }

        return true;
    }

    private void EndDrag()
    {
        if (_dragging is not null)
        {
            _dragging = null;
            Session?.EndGesture();
        }
    }

    private bool DeleteCornerAt(Vector2 mouse)
    {
        if (EditedRegion() is not Region region || CornerAt(region, mouse) is not int corner)
        {
            return false;
        }

        if (region.Corners.Count <= 3)
        {
            Toolbar?.ShowWarning("A region needs at least three corners.");
            return true;
        }

        List<GeoCoordinate> corners = [.. region.Corners];
        corners.RemoveAt(corner);
        Session!.UpdateRegion(region with { Corners = corners });
        return true;
    }

    private Region? EditedRegion() =>
        Session?.World.Regions.FirstOrDefault(r => r.Id == _editingRegionId);

    private int? CornerAt(Region region, Vector2 mouse)
    {
        for (int i = 0; i < region.Corners.Count; i++)
        {
            if (ScreenOf(region.Corners[i]) is Vector2 at && at.DistanceTo(mouse) <= GrabPixels)
            {
                return i;
            }
        }

        return null;
    }

    // The edge whose middle handle is under the mouse.
    private int? MiddleAt(Region region, Vector2 mouse)
    {
        for (int i = 0; i < region.Corners.Count; i++)
        {
            if (ScreenOf(Middle(region.Corners, i)) is Vector2 at
                && at.DistanceTo(mouse) <= GrabPixels)
            {
                return i;
            }
        }

        return null;
    }

    // The middle of the edge from corner i to the next, along the great circle.
    private static GeoCoordinate Middle(IReadOnlyList<GeoCoordinate> corners, int i)
    {
        return SphericalPolygon.FromUnit(SphericalPolygon.ToUnit(corners[i])
            + SphericalPolygon.ToUnit(corners[(i + 1) % corners.Count]));
    }

    private void DrawHandles()
    {
        if (!Visible)
        {
            return;
        }

        if (IsDrawing)
        {
            DrawPath(_drawing, closed: false);
            for (int i = 0; i < _drawing.Count; i++)
            {
                DrawCorner(_drawing[i], i == 0 && _drawing.Count >= 3 ? 1.5f : 1f);
            }
        }
        else if (EditedRegion() is Region region)
        {
            for (int i = 0; i < region.Corners.Count; i++)
            {
                if (ScreenOf(Middle(region.Corners, i)) is Vector2 middle)
                {
                    _overlay.DrawArc(middle, MidHandleRadius, 0, Mathf.Tau, 16, _handleColor, 1.5f);
                }

                DrawCorner(region.Corners[i], 1f);
            }
        }
    }

    // The outline so far, following the globe between corners (hidden parts skipped).
    private void DrawPath(List<GeoCoordinate> corners, bool closed)
    {
        if (corners.Count < 2)
        {
            return;
        }

        List<GeoCoordinate> path = SphericalPolygon.EdgePath(corners, 1.5, closed);
        for (int i = 1; i < path.Count; i++)
        {
            if (ScreenOf(path[i - 1]) is Vector2 a && ScreenOf(path[i]) is Vector2 b)
            {
                _overlay.DrawLine(a, b, _lineColor, 2);
            }
        }
    }

    private void DrawCorner(GeoCoordinate corner, float scale)
    {
        if (ScreenOf(corner) is Vector2 at)
        {
            _overlay.DrawCircle(at, HandleRadius * scale + 1.5f, Colors.Black);
            _overlay.DrawCircle(at, HandleRadius * scale, _handleColor);
        }
    }

    private PlanetSurface? Globe =>
        System?.SurfaceFor(_drawingBodyId ?? EditedRegion()?.BodyId ?? Guid.Empty);

    private Vector2? ScreenOf(GeoCoordinate spot) =>
        Globe is PlanetSurface globe ? GlobePicker.ScreenPositionOf(Camera!, globe, spot) : null;

    private GeoCoordinate? SpotAt(Vector2 mouse, bool nearestWhenMissed) =>
        Globe is PlanetSurface globe
            ? GlobePicker.CoordinateAt(Camera!, globe, mouse, nearestWhenMissed)
            : null;
}
