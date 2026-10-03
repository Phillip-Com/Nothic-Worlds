using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Interop;
using NothicWorlds.Rendering;
using NothicWorlds.Session;
using NothicWorlds.UI;

namespace NothicWorlds.Controls;

/// <summary>
/// Paints terrain onto the selected planet or moon (VISION.md BOD-05) while the Terrain panel is
/// open: drag on the globe to paint (or erase), with a circle showing the brush under the mouse.
/// Each stroke is one undo step. Drags that start off the globe pass through, so the camera
/// still turns.
/// </summary>
/// <remarks>
/// Must come after the camera and the markers in the scene: later nodes get unhandled input
/// first, and while active, this one takes the clicks on its globe.
/// </remarks>
public partial class TerrainBrush : CanvasLayer
{
    // Points around the brush circle drawn under the mouse.
    private const int CircleSegments = 64;

    private static readonly Color _outlineColor = new(0, 0, 0, 0.6f);
    private static readonly Color _circleColor = new(1, 1, 1, 0.9f);

    private Control _overlay = null!;
    private bool _active;
    private bool _painting;
    private GeoCoordinate? _lastSpot;
    private Vector2? _mouse;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The system view, for the globe.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The camera, for turning the mouse position into a spot on the globe.</summary>
    [Export] public PlanetCamera? Camera { get; set; }

    /// <summary>The toolbar: the brush hides (and stops) whenever it does.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>The terrain code painted: a terrain type's code, or 0 to erase.</summary>
    public byte Code { get; set; }

    /// <summary>The brush's radius in degrees of arc on the globe.</summary>
    public double RadiusDegrees { get; set; } = 2.0;

    /// <summary>Whether the brush paints (the Terrain panel turns it on while it's open).</summary>
    public bool IsActive
    {
        get => _active;
        set
        {
            _active = value;
            if (!value)
            {
                EndStroke();
            }

            _overlay?.QueueRedraw();  // The panel may set this before the brush is ready.
        }
    }

    public override void _Ready()
    {
        _overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.Draw += DrawCircle;
        AddChild(_overlay);
        if (Session is null || System is null || Camera is null)
        {
            GD.PushError("TerrainBrush needs a world session, system view, and camera.");
            return;
        }

        // The globe moves under a still mouse as the camera flies or the body spins.
        System.Placed += () =>
        {
            if (_active)
            {
                _overlay.QueueRedraw();
            }
        };
        Session.SelectionChanged += () => EndStroke();
        Session.WorldClosed += _ => EndStroke();
        if (Toolbar is not null)
        {
            Toolbar.VisibilityChanged += () =>
            {
                Visible = Toolbar.Visible;
                if (!Visible)
                {
                    EndStroke();
                }
            };
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_active || !Visible || Session is not { SelectedBodyHasSurface: true })
        {
            return;
        }

        bool handled = @event switch
        {
            InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press =>
                StartStroke(press.Position),
            InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } =>
                EndStroke(),
            InputEventMouseMotion motion => Move(motion.Position),
            _ => false,
        };
        if (handled)
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private bool StartStroke(Vector2 mouse)
    {
        if (SpotAt(mouse) is not GeoCoordinate spot)
        {
            return false;  // Missed the globe: let the camera turn.
        }

        string action = Code == 0
            ? "Erase Terrain"
            : $"Paint {Session!.TerrainTypes.FirstOrDefault(t => t.Code == Code)?.Name}";
        Session!.BeginGesture(action);
        _painting = true;
        _lastSpot = spot;
        Session.PaintTerrain(spot, spot, RadiusDegrees, Code);
        return true;
    }

    private bool EndStroke()
    {
        if (!_painting)
        {
            return false;
        }

        _painting = false;
        _lastSpot = null;
        Session?.EndGesture();
        return true;
    }

    // Moves the circle with the mouse and, mid-stroke, paints from the last spot to this one.
    // Off the globe, the stroke pauses and picks up again where the mouse comes back.
    private bool Move(Vector2 mouse)
    {
        _mouse = mouse;
        _overlay.QueueRedraw();
        if (!_painting)
        {
            return false;
        }

        GeoCoordinate? spot = SpotAt(mouse);
        if (spot is GeoCoordinate here)
        {
            Session!.PaintTerrain(_lastSpot ?? here, here, RadiusDegrees, Code);
        }

        _lastSpot = spot;
        return true;
    }

    private void DrawCircle()
    {
        if (!_active || _mouse is not Vector2 mouse || SpotAt(mouse) is not GeoCoordinate center
            || Globe is not PlanetSurface globe)
        {
            return;
        }

        Vector2?[] points = [.. CirclePoints(center, RadiusDegrees)
            .Select(spot => GlobePicker.ScreenPositionOf(Camera!, globe, spot))];
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] is Vector2 from && points[(i + 1) % points.Length] is Vector2 to)
            {
                _overlay.DrawLine(from, to, _outlineColor, 3.0f, antialiased: true);
                _overlay.DrawLine(from, to, _circleColor, 1.5f, antialiased: true);
            }
        }
    }

    // Spots evenly around a circle on the globe, `radius` degrees of arc from `center`.
    private static IEnumerable<GeoCoordinate> CirclePoints(GeoCoordinate center, double radius)
    {
        Vector3D middle = SphericalPolygon.ToUnit(center);
        Vector3D across = Math.Abs(middle.Y) < 0.9 ? new(0, 1, 0) : new(1, 0, 0);
        Vector3D first = Unit(Cross(middle, across));
        Vector3D second = Cross(middle, first);
        double angle = double.DegreesToRadians(radius);
        for (int i = 0; i < CircleSegments; i++)
        {
            double turn = 2 * Math.PI * i / CircleSegments;
            Vector3D point = middle * Math.Cos(angle)
                + (first * Math.Cos(turn) + second * Math.Sin(turn)) * Math.Sin(angle);
            yield return SphericalCoordinates.FromDirection(
                new System.Numerics.Vector3((float)point.X, (float)point.Y, (float)point.Z));
        }
    }

    private static Vector3D Cross(Vector3D a, Vector3D b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    private static Vector3D Unit(Vector3D v) => v * (1 / v.Length);

    private PlanetSurface? Globe => System?.SurfaceFor(Session?.SelectedBodyId ?? Guid.Empty);

    private GeoCoordinate? SpotAt(Vector2 mouse) =>
        Globe is PlanetSurface globe
            ? GlobePicker.CoordinateAt(Camera!, globe, mouse)
            : null;
}
