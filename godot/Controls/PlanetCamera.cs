using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Interop;

namespace NothicWorlds.Controls;

/// <summary>
/// Camera for viewing a single planet (VISION.md REN-02). It always looks at the planet's
/// center, and north stays up.
/// <list type="bullet">
/// <item>Orbit (left-drag): spins the globe at a steady rate, for moving quickly around it.</item>
/// <item>Pan (right-drag, WASD/arrows): slides across the surface at a speed matched to the
/// zoom level, so the ground follows the mouse.</item>
/// <item>Zoom (scroll wheel, E/Q, +/-). Home resets the view.</item>
/// </list>
/// Movement eases toward a target position instead of jumping, so it feels smooth.
/// Assumes the planet is centered at the world origin.
/// </summary>
public partial class PlanetCamera : Camera3D
{
    // Stops short of the poles so the view can't flip over them.
    private const double MaxLatitude = 89.0;

    // Limits how fast longitude changes near the poles, where it gets very compressed.
    private const double MinLongitudeScale = 0.1;

    /// <summary>Planet radius in world units.</summary>
    [Export] public float PlanetRadius { get; set; } = 1.0f;

    /// <summary>Closest the camera can get to the surface, as a fraction of the radius.</summary>
    [Export] public float MinAltitude { get; set; } = 0.05f;

    /// <summary>Farthest the camera can get from the surface, as a fraction of the radius.</summary>
    [Export] public float MaxAltitude { get; set; } = 8.0f;

    [Export] public float StartAltitude { get; set; } = 2.0f;
    [Export] public double StartLatitude { get; set; } = 20.0;
    [Export] public double StartLongitude { get; set; }

    /// <summary>Degrees the globe spins per pixel dragged when orbiting.</summary>
    [Export] public float OrbitDegreesPerPixel { get; set; } = 0.25f;

    /// <summary>Altitude change per scroll-wheel step (1.15 = 15%).</summary>
    [Export] public float ZoomStepFactor { get; set; } = 1.15f;

    /// <summary>Zoom steps per second while a zoom key is held.</summary>
    [Export] public float KeyboardZoomStepsPerSecond { get; set; } = 6.0f;

    /// <summary>Keyboard pan speed, in screen heights per second.</summary>
    [Export] public float KeyboardPanScreensPerSecond { get; set; } = 0.6f;

    /// <summary>How quickly the view catches up to its target. Higher is snappier.</summary>
    [Export] public float Smoothing { get; set; } = 12.0f;

    // Longitudes are kept unwrapped (e.g. 400°) so easing never takes the long way around.
    private double _targetLatitude;
    private double _targetLongitude;
    private float _targetAltitude;
    private double _currentLatitude;
    private double _currentLongitude;
    private float _currentAltitude;

    public override void _Ready()
    {
        ResetView();
        SnapToTarget();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion
                when motion.ButtonMask.HasFlag(MouseButtonMask.Left):
                Orbit(motion.Relative);
                break;
            case InputEventMouseMotion motion
                when motion.ButtonMask.HasFlag(MouseButtonMask.Right):
                Pan(motion.Relative);
                break;
            case InputEventMouseButton { Pressed: true } button
                when button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown:
                // Precision touchpads report fractional scroll amounts; mice report 0 or 1.
                float steps = button.Factor > 0 ? button.Factor : 1.0f;
                Zoom(button.ButtonIndex == MouseButton.WheelUp ? steps : -steps);
                break;
            case InputEvent action when action.IsActionPressed(InputActions.CameraReset):
                ResetView();
                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        HandleKeyboard((float)delta);
        EaseTowardTarget((float)delta);
        UpdateTransform();
    }

    /// <summary>
    /// Spins the globe as if dragged by <paramref name="screenDelta"/> pixels, at a steady rate
    /// regardless of zoom.
    /// </summary>
    public void Orbit(Vector2 screenDelta)
    {
        MoveTarget(screenDelta.Y * OrbitDegreesPerPixel, -screenDelta.X * OrbitDegreesPerPixel);
    }

    /// <summary>
    /// Slides across the surface as if the ground were dragged by <paramref name="screenDelta"/>
    /// pixels. Speed scales with altitude so the ground stays under the mouse.
    /// </summary>
    public void Pan(Vector2 screenDelta)
    {
        double degreesPerPixel = GroundDegreesPerPixel();
        double longitudeScale = Math.Max(
            Math.Cos(double.DegreesToRadians(_currentLatitude)), MinLongitudeScale);

        MoveTarget(
            screenDelta.Y * degreesPerPixel,
            -screenDelta.X * degreesPerPixel / longitudeScale);
    }

    /// <summary>
    /// Zooms by a number of steps. Positive moves closer. Each step changes the altitude by a
    /// percentage, so zooming feels the same at every distance.
    /// </summary>
    public void Zoom(float steps)
    {
        float altitude = _targetAltitude / Mathf.Pow(ZoomStepFactor, steps);
        _targetAltitude = Mathf.Clamp(altitude, MinAltitude, MaxAltitude);
    }

    /// <summary>Returns to the starting view.</summary>
    public void ResetView()
    {
        _targetLatitude = StartLatitude;
        _targetLongitude = StartLongitude;
        _targetAltitude = Mathf.Clamp(StartAltitude, MinAltitude, MaxAltitude);
    }

    private void MoveTarget(double northDegrees, double eastDegrees)
    {
        _targetLatitude = Math.Clamp(_targetLatitude + northDegrees, -MaxLatitude, MaxLatitude);
        _targetLongitude += eastDegrees;
    }

    private void HandleKeyboard(float delta)
    {
        Vector2 pan = Input.GetVector(
            InputActions.CameraPanWest,
            InputActions.CameraPanEast,
            InputActions.CameraPanSouth,
            InputActions.CameraPanNorth);
        if (pan != Vector2.Zero)
        {
            float screenHeight = GetViewport().GetVisibleRect().Size.Y;
            float pixels = KeyboardPanScreensPerSecond * screenHeight * delta;
            // Moving the view east is the same as dragging the ground west (to the left).
            Pan(new Vector2(-pan.X, pan.Y) * pixels);
        }

        float zoom = Input.GetAxis(InputActions.CameraZoomOut, InputActions.CameraZoomIn);
        if (zoom != 0.0f)
        {
            Zoom(zoom * KeyboardZoomStepsPerSecond * delta);
        }
    }

    // How many degrees of the surface one screen pixel covers, directly below the camera.
    private double GroundDegreesPerPixel()
    {
        // Altitude is measured in planet radii, so the visible ground height in radii is also
        // the arc it covers in radians.
        float viewportHeight = GetViewport().GetVisibleRect().Size.Y;
        double visibleArc = 2.0 * _currentAltitude * Math.Tan(double.DegreesToRadians(Fov) / 2.0);
        return double.RadiansToDegrees(visibleArc / viewportHeight);
    }

    private void EaseTowardTarget(float delta)
    {
        // Frame-rate independent easing: the same fraction of the gap closes per second.
        float t = 1.0f - Mathf.Exp(-Smoothing * delta);

        _currentLatitude += (_targetLatitude - _currentLatitude) * t;
        _currentLongitude += (_targetLongitude - _currentLongitude) * t;
        // Ease altitude in log space so zooming in and out feel equally smooth.
        _currentAltitude = Mathf.Exp(
            Mathf.Lerp(Mathf.Log(_currentAltitude), Mathf.Log(_targetAltitude), t));
    }

    private void SnapToTarget()
    {
        _currentLatitude = _targetLatitude;
        _currentLongitude = _targetLongitude;
        _currentAltitude = _targetAltitude;
        UpdateTransform();
    }

    private void UpdateTransform()
    {
        var coordinate = new GeoCoordinate(_currentLatitude, _currentLongitude);
        Vector3 direction = SphericalCoordinates.ToDirection(coordinate).ToGodot();

        Position = direction * PlanetRadius * (1.0f + _currentAltitude);
        LookAt(Vector3.Zero, Vector3.Up);
    }
}
