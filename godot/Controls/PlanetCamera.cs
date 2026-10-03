using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Interop;

namespace NothicWorlds.Controls;

/// <summary>
/// Camera for viewing a single planet (VISION.md REN-02). North always stays up.
/// <list type="bullet">
/// <item>Orbit (left-drag): rotates around the point being looked at, at a steady rate.</item>
/// <item>Pan (right-drag, WASD/arrows): switches automatically with zoom (see
/// <see cref="PanMode"/>). Zoomed out, it slides the whole view so the planet moves across the
/// screen. Zoomed in close, it slides across the surface at a speed matched to altitude, and the
/// view re-centers on the planet.</item>
/// <item>Zoom (scroll wheel, E/Q, +/-). Home resets the view.</item>
/// </list>
/// Movement eases toward a target position instead of jumping, so it feels smooth.
/// Assumes the planet is centered at the world origin.
/// <para>
/// Close to the ground (below <see cref="LocalViewMaxAltitude"/>) the camera is in the
/// <b>local view</b> (VISION.md REN-04): it rides with the planet, staying over the same ground
/// as it spins, with the planet's north up on screen. Its latitude and longitude are then the
/// planet's own. Farther out they're fixed in space, and the planet turns beneath. Crossing
/// between the two keeps the camera where it is; only which way is "up" eases round.
/// </para>
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

    /// <summary>
    /// Farthest the camera can get from its focus point, as a fraction of the radius. Lowering
    /// it pulls the camera in if it's farther out.
    /// </summary>
    [Export]
    public float MaxAltitude
    {
        get => _maxAltitude;
        set
        {
            _maxAltitude = value;
            _targetAltitude = Mathf.Clamp(_targetAltitude, MinAltitude, value);
        }
    }

    /// <summary>
    /// Below this altitude (in radii), panning slides across the surface. Above it, panning
    /// slides the whole view. At 1.0, the planet roughly fills the screen at the switch point.
    /// </summary>
    [Export] public float SurfacePanMaxAltitude { get; set; } = 1.0f;

    /// <summary>
    /// Below this altitude (in radii) the camera is in the local view (see the class notes).
    /// At 0.25 an Earth-sized planet's view is about 2,500 km across.
    /// </summary>
    [Export] public float LocalViewMaxAltitude { get; set; } = 0.25f;

    /// <summary>
    /// How the focused planet is turned right now (its spin and tilt), which the local view
    /// rides with. Set every frame by whatever places the planet.
    /// </summary>
    public Basis SurfaceFrame { get; set; } = Basis.Identity;

    /// <summary>
    /// Whether the local view may be used. Off while flying between bodies, whose frames differ.
    /// </summary>
    public bool AllowLocalView { get; set; } = true;

    [Export] public float StartAltitude { get; set; } = 2.0f;
    [Export] public double StartLatitude { get; set; } = 20.0;
    [Export] public double StartLongitude { get; set; }

    /// <summary>Degrees the view rotates per pixel dragged when orbiting.</summary>
    [Export] public float OrbitDegreesPerPixel { get; set; } = 0.25f;

    /// <summary>Altitude change per scroll-wheel step (1.15 = 15%).</summary>
    [Export] public float ZoomStepFactor { get; set; } = 1.15f;

    /// <summary>Zoom steps per second while a zoom key is held.</summary>
    [Export] public float KeyboardZoomStepsPerSecond { get; set; } = 6.0f;

    /// <summary>Keyboard pan speed, in screen heights per second.</summary>
    [Export] public float KeyboardPanScreensPerSecond { get; set; } = 0.6f;

    /// <summary>How quickly the view catches up to its target. Higher is snappier.</summary>
    [Export] public float Smoothing { get; set; } = 12.0f;

    // The camera looks at a focus point: the planet's center plus a sideways offset from view
    // panning. Its direction from the focus point is a latitude/longitude, and its distance is
    // the altitude. Longitudes are kept unwrapped (e.g. 400°) so easing never takes the long
    // way around.
    private float _maxAltitude = 8.0f;
    private double _targetLatitude;
    private double _targetLongitude;
    private float _targetAltitude;
    private Vector3 _targetFocusOffset;
    private double _currentLatitude;
    private double _currentLongitude;
    private float _currentAltitude;
    private Vector3 _currentFocusOffset;

    // In the local view, latitude and longitude are in the planet's own frame (SurfaceFrame);
    // otherwise in fixed space. Which way is up on screen eases between the two.
    private bool _local;
    private Vector3 _currentUp = Vector3.Up;

    // The mouse button that started the current drag, if any.
    private MouseButton _dragButton = MouseButton.None;
    private bool _keyboardPanning;

    /// <summary>How far the camera is from the surface, in planet radii.</summary>
    public float CurrentAltitude => _currentAltitude;

    /// <summary>True while the camera is in the local view (riding with the ground).</summary>
    public bool IsLocalView => _local;

    /// <summary>How panning currently behaves, based on zoom.</summary>
    public PanMode PanMode =>
        _currentAltitude < SurfacePanMaxAltitude ? PanMode.Surface : PanMode.View;

    /// <summary>What the user is doing with the camera right now.</summary>
    public CameraAction CurrentAction => _dragButton switch
    {
        MouseButton.Left => _local ? CameraAction.Panning : CameraAction.Orbiting,
        MouseButton.Right => CameraAction.Panning,
        _ => _keyboardPanning ? CameraAction.Panning : CameraAction.None,
    };

    public override void _Ready()
    {
        ResetView();
        SnapToTarget();
    }

    public override void _Input(InputEvent @event)
    {
        // Clicking the view (not a panel) finishes editing any field, so the keyboard moves
        // the camera again. The click itself carries on as usual.
        if (@event is InputEventMouseButton { Pressed: true }
            && GetViewport().GuiGetHoveredControl() is null)
        {
            GetViewport().GuiReleaseFocus();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { Pressed: true } button
                when button.ButtonIndex is MouseButton.Left or MouseButton.Right:
                _dragButton = button.ButtonIndex;
                break;
            case InputEventMouseMotion motion when _dragButton == MouseButton.Left:
                Orbit(motion.Relative);
                break;
            case InputEventMouseMotion motion when _dragButton == MouseButton.Right:
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
        // Checked every frame, because the button release may be consumed elsewhere.
        if (_dragButton != MouseButton.None && !Input.IsMouseButtonPressed(_dragButton))
        {
            _dragButton = MouseButton.None;
        }

        HandleKeyboard((float)delta);

        if (PanMode == PanMode.Surface)
        {
            // Close up, the view re-centers on the planet so surface panning stays around it.
            _targetFocusOffset = Vector3.Zero;
        }

        SetLocal(AllowLocalView && _targetAltitude < LocalViewMaxAltitude);
        EaseTowardTarget((float)delta);
        UpdateTransform();
    }

    /// <summary>
    /// Places the camera again for the current <see cref="SurfaceFrame"/>, so in the local view
    /// it keeps up with the ground in the same frame the planet moved.
    /// </summary>
    public void FollowSurface()
    {
        if (_local)
        {
            UpdateTransform();
        }
    }

    /// <summary>
    /// Rotates around the focus point as if dragged by <paramref name="screenDelta"/> pixels, at
    /// a steady rate regardless of zoom. In the local view it pans the map instead.
    /// </summary>
    public void Orbit(Vector2 screenDelta)
    {
        if (_local)
        {
            // Looking straight down at a map, dragging moves the map, as in map apps.
            PanAcrossSurface(screenDelta);
            return;
        }

        MoveAngles(screenDelta.Y * OrbitDegreesPerPixel, -screenDelta.X * OrbitDegreesPerPixel);
    }

    /// <summary>
    /// Pans as if the scene were dragged by <paramref name="screenDelta"/> pixels. What moves
    /// depends on <see cref="PanMode"/>. Either way, what's under the mouse follows it.
    /// </summary>
    public void Pan(Vector2 screenDelta)
    {
        if (PanMode == PanMode.Surface)
        {
            PanAcrossSurface(screenDelta);
        }
        else
        {
            SlideView(screenDelta);
        }
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

    /// <summary>
    /// Puts the camera at an altitude straight away, with no easing (e.g. while the view flies
    /// between bodies). Kept within the camera's limits.
    /// </summary>
    public void SetAltitudeImmediately(float altitude)
    {
        _targetAltitude = Mathf.Clamp(altitude, MinAltitude, MaxAltitude);
        _currentAltitude = _targetAltitude;
    }

    /// <summary>
    /// Re-centers the view on the planet, keeping the angle and zoom (e.g. when the view moves
    /// to another body).
    /// </summary>
    public void ClearFocusOffset()
    {
        _targetFocusOffset = Vector3.Zero;
        _currentFocusOffset = Vector3.Zero;
    }

    /// <summary>Returns to the starting view, centered on the planet.</summary>
    public void ResetView()
    {
        SetLocal(false);
        _targetLatitude = StartLatitude;
        _targetLongitude = StartLongitude;
        _targetAltitude = Mathf.Clamp(StartAltitude, MinAltitude, MaxAltitude);
        _targetFocusOffset = Vector3.Zero;
    }

    /// <summary>
    /// Where the camera is heading, for saving with the world (VISION.md SAV-01). In the local
    /// view the latitude and longitude are the planet's own.
    /// </summary>
    public CameraView GetView()
    {
        return new CameraView(
            _targetLatitude,
            SphericalCoordinates.WrapLongitude(_targetLongitude),
            _targetAltitude,
            _targetFocusOffset.X,
            _targetFocusOffset.Y,
            _targetFocusOffset.Z);
    }

    /// <summary>
    /// Jumps straight to a saved view (no easing), or to the starting view if it's null. Values
    /// are clamped to the camera's limits, so a hand-edited file can't break the camera.
    /// </summary>
    public void SetView(CameraView? view)
    {
        if (view is null)
        {
            ResetView();
        }
        else
        {
            _targetLatitude = Math.Clamp(view.LatitudeDegrees, -MaxLatitude, MaxLatitude);
            _targetLongitude = view.LongitudeDegrees;
            _targetAltitude = Mathf.Clamp((float)view.Altitude, MinAltitude, MaxAltitude);
            var offset = new Vector3(
                (float)view.FocusOffsetX, (float)view.FocusOffsetY, (float)view.FocusOffsetZ);
            _targetFocusOffset = offset.LimitLength(PlanetRadius * (1.0f + MaxAltitude));

            // A view saved close up was saved in the planet's own frame (see GetView).
            _local = AllowLocalView && _targetAltitude < LocalViewMaxAltitude;
        }

        SnapToTarget();
    }

    private void PanAcrossSurface(Vector2 screenDelta)
    {
        // Measured at the ground directly below the camera.
        float groundDistance = _currentAltitude * PlanetRadius;
        double degreesPerPixel =
            double.RadiansToDegrees(WorldUnitsPerPixel(groundDistance) / PlanetRadius);
        double longitudeScale = Math.Max(
            Math.Cos(double.DegreesToRadians(_currentLatitude)), MinLongitudeScale);

        MoveAngles(
            screenDelta.Y * degreesPerPixel,
            -screenDelta.X * degreesPerPixel / longitudeScale);
    }

    private void SlideView(Vector2 screenDelta)
    {
        // Dragging right moves the scene right, so the focus point moves left (and likewise
        // for up and down).
        // Measured at the focus point, so the planet's center follows the mouse.
        float focusDistance = (1.0f + _currentAltitude) * PlanetRadius;
        Basis basis = GlobalTransform.Basis;
        Vector3 move = (-basis.X * screenDelta.X + basis.Y * screenDelta.Y)
            * WorldUnitsPerPixel(focusDistance);

        // Keep the planet within reach; Home always brings it back to the center.
        float maxOffset = PlanetRadius * (1.0f + MaxAltitude);
        _targetFocusOffset = (_targetFocusOffset + move).LimitLength(maxOffset);
    }

    private void MoveAngles(double northDegrees, double eastDegrees)
    {
        _targetLatitude = Math.Clamp(_targetLatitude + northDegrees, -MaxLatitude, MaxLatitude);
        _targetLongitude += eastDegrees;
    }

    private void HandleKeyboard(float delta)
    {
        // Ctrl+key combinations are app shortcuts (e.g. Ctrl+S saves), not camera movement. And
        // while a text or number field is being edited, the keys are for it (e.g. Up/Down
        // change a number).
        if (Input.IsKeyPressed(Key.Ctrl)
            || GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit)
        {
            _keyboardPanning = false;
            return;
        }

        Vector2 pan = Input.GetVector(
            InputActions.CameraPanWest,
            InputActions.CameraPanEast,
            InputActions.CameraPanSouth,
            InputActions.CameraPanNorth);
        _keyboardPanning = pan != Vector2.Zero;
        if (_keyboardPanning)
        {
            float screenHeight = GetViewport().GetVisibleRect().Size.Y;
            float pixels = KeyboardPanScreensPerSecond * screenHeight * delta;
            // Moving the view east is the same as dragging the scene west (to the left).
            Pan(new Vector2(-pan.X, pan.Y) * pixels);
        }

        float zoom = Input.GetAxis(InputActions.CameraZoomOut, InputActions.CameraZoomIn);
        if (zoom != 0.0f)
        {
            Zoom(zoom * KeyboardZoomStepsPerSecond * delta);
        }
    }

    // How many world units one screen pixel covers at a given distance from the camera.
    private float WorldUnitsPerPixel(float distance)
    {
        float viewportHeight = GetViewport().GetVisibleRect().Size.Y;
        float visibleHeight = 2.0f * distance * Mathf.Tan(Mathf.DegToRad(Fov) / 2.0f);
        return visibleHeight / viewportHeight;
    }

    private void EaseTowardTarget(float delta)
    {
        // Frame-rate independent easing: the same fraction of the gap closes per second.
        float t = 1.0f - Mathf.Exp(-Smoothing * delta);

        _currentLatitude += (_targetLatitude - _currentLatitude) * t;
        _currentLongitude += (_targetLongitude - _currentLongitude) * t;
        _currentFocusOffset = _currentFocusOffset.Lerp(_targetFocusOffset, t);
        // Ease altitude in log space so zooming in and out feel equally smooth.
        _currentAltitude = Mathf.Exp(
            Mathf.Lerp(Mathf.Log(_currentAltitude), Mathf.Log(_targetAltitude), t));

        // Up turns gently between the fixed frame's and the planet's north (they differ by
        // the planet's tilt and spin).
        Vector3 up = Frame * Vector3.Up;
        _currentUp = _currentUp.Slerp(up, t * 0.5f).Normalized();
    }

    private void SnapToTarget()
    {
        _currentLatitude = _targetLatitude;
        _currentLongitude = _targetLongitude;
        _currentAltitude = _targetAltitude;
        _currentFocusOffset = _targetFocusOffset;
        _currentUp = Frame * Vector3.Up;
        UpdateTransform();
    }

    // The frame latitude and longitude are measured in: the planet's in the local view.
    private Basis Frame => _local ? SurfaceFrame : Basis.Identity;

    // Switches between the local view and the fixed frame, re-expressing where the camera is
    // and where it's heading in the new frame, so nothing moves.
    private void SetLocal(bool local)
    {
        if (local == _local)
        {
            return;
        }

        Basis toNew = local ? SurfaceFrame.Inverse() : SurfaceFrame;
        (_currentLatitude, _currentLongitude) =
            Reexpress(toNew, _currentLatitude, _currentLongitude);
        (_targetLatitude, _targetLongitude) = Reexpress(toNew, _targetLatitude, _targetLongitude);
        _local = local;
    }

    // A direction given as latitude/longitude, turned by `turn`, as latitude/longitude again.
    // The longitude stays unwrapped near the old one, so easing never takes the long way round.
    private static (double Latitude, double Longitude) Reexpress(
        Basis turn, double latitude, double longitude)
    {
        Vector3 direction = SphericalCoordinates
            .ToDirection(new GeoCoordinate(Math.Clamp(latitude, -90, 90), longitude))
            .ToGodot();
        GeoCoordinate turned = SphericalCoordinates.FromDirection((turn * direction).ToNumerics());
        double newLongitude = longitude
            + SphericalCoordinates.LongitudeDelta(longitude, turned.LongitudeDegrees);
        return (Math.Clamp(turned.LatitudeDegrees, -MaxLatitude, MaxLatitude), newLongitude);
    }

    private void UpdateTransform()
    {
        var coordinate = new GeoCoordinate(_currentLatitude, _currentLongitude);
        Vector3 direction = Frame * SphericalCoordinates.ToDirection(coordinate).ToGodot();
        Vector3 focus = _currentFocusOffset;
        Vector3 position = focus + direction * PlanetRadius * (1.0f + _currentAltitude);

        // When the view has been slid away from the planet, orbiting could swing the camera
        // into it. Push the camera back out to the minimum altitude if so.
        float minDistance = PlanetRadius * (1.0f + MinAltitude);
        if (position.LengthSquared() < minDistance * minDistance)
        {
            position = position.Normalized() * minDistance;
        }

        Position = position;
        LookAt(focus, _currentUp);
    }
}
