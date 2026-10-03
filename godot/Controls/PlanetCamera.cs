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
/// <para>
/// A flat world (VISION.md BOD-02) has its own close-up: hovering over a spot on its top face,
/// looking straight down with the disc's center (its north pole) up on screen, and dragging
/// slides across the face like a map. It rides with the tumbling disc. Zooming out past
/// <see cref="FlatLocalExitAltitude"/> returns to orbiting it.
/// </para>
/// </summary>
public partial class PlanetCamera : Camera3D
{
    // Stops short of the poles so the view can't flip over them.
    private const double MaxLatitude = 89.0;

    // Limits how fast longitude changes near the poles, where it gets very compressed.
    private const double MinLongitudeScale = 0.1;

    /// <summary>
    /// Planet radius in world units: how big the body is for framing it (a flat world's is its
    /// disc's radius).
    /// </summary>
    [Export] public float PlanetRadius { get; set; } = 1.0f;

    /// <summary>
    /// The focused body's shape (VISION.md BOD-02), which decides what its close-up is. Set
    /// every frame by whatever places the planet.
    /// </summary>
    public BodyShape Shape { get; set; } = BodyShape.Sphere;

    /// <summary>
    /// In a flat world's close-up, zooming out above this height (in <see cref="PlanetRadius"/>,
    /// the disc's radius) returns to orbiting. Well above where the close-up starts, so the two
    /// don't flip back and forth.
    /// </summary>
    public const float FlatLocalExitAltitude = 1.5f;

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

    // A flat world's close-up: the spot on the top face below the camera (x and z, in the
    // matching globe's radii, as in FlatDisc), and the altitude is the height above the face.
    private bool _flatLocal;
    private Vector2 _targetSpot;
    private Vector2 _currentSpot;

    // Going into or out of a flat world's close-up turns the camera from looking at the disc at
    // an angle to looking straight down (or back): it blends from where it was over a moment.
    private const float SwitchBlendSeconds = 0.4f;
    private Transform3D _blendFrom;
    private float _blend = 1.0f;

    // A spot to glide to in the local view once it's allowed (e.g. after flying to the body).
    private (GeoCoordinate Spot, float Altitude)? _pendingSurfaceTarget;

    // The mouse button that started the current drag, if any.
    private MouseButton _dragButton = MouseButton.None;
    private bool _keyboardPanning;

    /// <summary>How far the camera is from the surface, in planet radii.</summary>
    public float CurrentAltitude => _currentAltitude;

    /// <summary>True while the camera is in the local view (riding with the ground).</summary>
    public bool IsLocalView => _local || _flatLocal;

    /// <summary>How panning currently behaves, based on zoom.</summary>
    public PanMode PanMode =>
        _currentAltitude < SurfacePanMaxAltitude ? PanMode.Surface : PanMode.View;

    /// <summary>What the user is doing with the camera right now.</summary>
    public CameraAction CurrentAction => _dragButton switch
    {
        MouseButton.Left => IsLocalView ? CameraAction.Panning : CameraAction.Orbiting,
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

        UpdateLocalMode();
        if (_pendingSurfaceTarget is var (spot, altitude) && AllowLocalView)
        {
            _pendingSurfaceTarget = null;
            GlideToSurface(spot, altitude);
        }

        EaseTowardTarget((float)delta);
        _blend = Mathf.Min(1.0f, _blend + (float)delta / SwitchBlendSeconds);
        UpdateTransform();
    }

    /// <summary>
    /// Glides down into the local view over a spot on the planet, close enough that
    /// <paramref name="spanDegrees"/> of arc fits on screen top to bottom (VISION.md REN-04:
    /// Zoom to). Waits until the local view is allowed, e.g. while flying to the body.
    /// </summary>
    public void FlyToSurface(GeoCoordinate spot, double spanDegrees)
    {
        // Seen from a low altitude a, the screen's half-height spans about a·tan(fov/2) radians
        // of the ground; leave a little margin around the span.
        // On a flat world a radian of arc is one globe radius of the face, and the altitude is
        // measured in disc radii, π times as big.
        double halfSpan = double.DegreesToRadians(spanDegrees) / 2 * 1.25;
        bool flat = Shape == BodyShape.FlatDisc;
        double altitude = halfSpan / Math.Tan(double.DegreesToRadians(Fov) / 2)
            / (flat ? FlatDisc.Radius : 1);
        _pendingSurfaceTarget = (spot, Mathf.Clamp((float)altitude, MinAltitude,
            (flat ? FlatLocalExitAltitude : LocalViewMaxAltitude) * 0.9f));
    }

    /// <summary>
    /// Places the camera again for the current <see cref="SurfaceFrame"/>, so in the local view
    /// it keeps up with the ground in the same frame the planet moved.
    /// </summary>
    public void FollowSurface()
    {
        if (IsLocalView)
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
        if (IsLocalView)
        {
            // Looking straight down at a map, dragging moves the map, as in map apps.
            if (_flatLocal)
            {
                PanAcrossFace(screenDelta);
            }
            else
            {
                PanAcrossSurface(screenDelta);
            }

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
        if (_flatLocal)
        {
            PanAcrossFace(screenDelta);
        }
        else if (PanMode == PanMode.Surface)
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
        _flatLocal = false;
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
        if (_flatLocal)
        {
            return FlatLocalAsOrbit();
        }

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

            // A view saved close up was saved in the planet's own frame (see GetView). A flat
            // world's close-up is saved as the orbiting view above it, so it opens orbiting.
            _flatLocal = false;
            _local = AllowLocalView && Shape != BodyShape.FlatDisc
                && _targetAltitude < LocalViewMaxAltitude;
        }

        SnapToTarget();
    }

    // In a flat world's close-up: slides across the face, so what's under the mouse follows it.
    private void PanAcrossFace(Vector2 screenDelta)
    {
        Basis basis = GlobalTransform.Basis;
        Vector3 move = (-basis.X * screenDelta.X + basis.Y * screenDelta.Y)
            * WorldUnitsPerPixel(_currentAltitude * PlanetRadius);
        Vector3 onFace = SurfaceFrame.Inverse() * move / GlobeRadius;
        _targetSpot = (_targetSpot + new Vector2(onFace.X, onFace.Z))
            .LimitLength((float)FlatDisc.Radius);
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
        _currentSpot = _currentSpot.Lerp(_targetSpot, t);
        // Ease altitude in log space so zooming in and out feel equally smooth.
        _currentAltitude = Mathf.Exp(
            Mathf.Lerp(Mathf.Log(_currentAltitude), Mathf.Log(_targetAltitude), t));

        // Up turns gently between the fixed frame's and the planet's north (they differ by
        // the planet's tilt and spin).
        Vector3 up = Frame * LocalUp;
        _currentUp = _currentUp.Slerp(up, t * 0.5f).Normalized();
    }

    private void SnapToTarget()
    {
        _currentLatitude = _targetLatitude;
        _currentLongitude = _targetLongitude;
        _currentAltitude = _targetAltitude;
        _currentFocusOffset = _targetFocusOffset;
        _currentSpot = _targetSpot;
        _currentUp = Frame * LocalUp;
        UpdateTransform();
    }

    private void GlideToSurface(GeoCoordinate spot, float altitude)
    {
        if (Shape == BodyShape.FlatDisc)
        {
            GlideToFace(spot, altitude);
            return;
        }

        _targetAltitude = altitude;
        SetLocal(true);
        _targetFocusOffset = Vector3.Zero;
        _targetLatitude = Math.Clamp(spot.LatitudeDegrees, -MaxLatitude, MaxLatitude);
        _targetLongitude = _currentLongitude
            + SphericalCoordinates.LongitudeDelta(_currentLongitude, spot.LongitudeDegrees);
    }

    // A flat world's close-up starts from wherever the camera is, then heads for the spot.
    private void GlideToFace(GeoCoordinate spot, float altitude)
    {
        if (!_flatLocal && !EnterFlatLocal())
        {
            SnapAboveFace();
        }

        Vector3D point = FlatDisc.TopPointFor(
            SphericalCoordinates.ToDirection(spot).ToVector3D());
        _targetSpot = new Vector2((float)point.X, (float)point.Z);
        _targetAltitude = altitude;
    }

    // From below or beside the disc there's no spot under the camera: start straight above the
    // center instead, at the camera's distance.
    private void SnapAboveFace()
    {
        _flatLocal = true;
        _currentSpot = Vector2.Zero;
        _targetSpot = Vector2.Zero;
        _currentAltitude = Mathf.Clamp(Position.Length() / PlanetRadius, MinAltitude,
            FlatLocalExitAltitude);
        _targetFocusOffset = Vector3.Zero;
        _currentFocusOffset = Vector3.Zero;
    }

    // The matching globe's radius in world units (a flat world's disc is π times as wide).
    private float GlobeRadius =>
        Shape == BodyShape.FlatDisc ? PlanetRadius / (float)FlatDisc.Radius : PlanetRadius;

    // Which way is up on screen in the planet's own frame: north for a globe; toward the
    // center (the north pole) in a flat world's close-up.
    private Vector3 LocalUp
    {
        get
        {
            if (!_flatLocal)
            {
                return Vector3.Up;
            }

            Vector2 toCenter = -_currentSpot;
            return toCenter.LengthSquared() > 1e-8f
                ? new Vector3(toCenter.X, 0, toCenter.Y).Normalized()
                : Vector3.Forward;
        }
    }

    // Moves between orbiting and the local view as the zoom crosses the thresholds: the
    // globe's local view, or a flat world's close-up (entered only from above its face).
    private void UpdateLocalMode()
    {
        bool flat = Shape == BodyShape.FlatDisc;
        if (_flatLocal && (!flat || !AllowLocalView || _targetAltitude > FlatLocalExitAltitude))
        {
            ExitFlatLocal();
        }

        if (flat)
        {
            SetLocal(false);
            if (!_flatLocal && AllowLocalView && _targetAltitude < LocalViewMaxAltitude)
            {
                EnterFlatLocal();
            }

            return;
        }

        SetLocal(AllowLocalView && _targetAltitude < LocalViewMaxAltitude);
    }

    // Starts a flat world's close-up over the spot below the camera, at its height, carrying
    // on any zoom under way. False if the camera isn't above the face.
    private bool EnterFlatLocal()
    {
        Basis toDisc = SurfaceFrame.Inverse();
        Vector3 local = toDisc * Position;
        float faceHeight = (float)FlatDisc.HalfThickness * GlobeRadius;
        if (local.Y <= faceHeight)
        {
            return false;
        }

        // Keep what's in the middle of the view in the middle: start above the spot the camera
        // is looking at (or the one below it, if it looks past the disc), as far away as now.
        Vector3 ahead = toDisc * -GlobalTransform.Basis.Z;
        Vector3 spot = new(local.X, faceHeight, local.Z);
        if (ahead.Y < 0)
        {
            Vector3 hit = local + ahead * ((faceHeight - local.Y) / ahead.Y);
            if (new Vector2(hit.X, hit.Z).Length() <= FlatDisc.Radius * GlobeRadius)
            {
                spot = hit;
            }
        }

        float zoom = _targetAltitude / _currentAltitude;
        StartBlend();
        _flatLocal = true;
        _currentSpot = new Vector2(spot.X, spot.Z) / GlobeRadius;
        _currentSpot = _currentSpot.LimitLength((float)FlatDisc.Radius);
        _targetSpot = _currentSpot;
        _currentAltitude = Mathf.Max(local.DistanceTo(spot) / PlanetRadius, MinAltitude);
        _targetAltitude = Mathf.Clamp(_currentAltitude * zoom, MinAltitude, MaxAltitude);
        _targetFocusOffset = Vector3.Zero;
        _currentFocusOffset = Vector3.Zero;
        _currentUp = SurfaceFrame * LocalUp;
        return true;
    }

    // Back to orbiting, from wherever the close-up left the camera.
    private void ExitFlatLocal()
    {
        CameraView view = FlatLocalAsOrbit();
        float zoomedTo = _targetAltitude;
        StartBlend();
        _flatLocal = false;
        _currentLatitude = view.LatitudeDegrees;
        _currentLongitude = view.LongitudeDegrees;
        _targetLatitude = view.LatitudeDegrees;
        _targetLongitude = view.LongitudeDegrees;
        _currentAltitude = (float)view.Altitude;
        _targetAltitude = Mathf.Clamp(Mathf.Max(zoomedTo, _currentAltitude), MinAltitude,
            MaxAltitude);
    }

    // The orbiting view where the close-up's camera is now (in fixed space). Above the middle
    // of the disc that can be inside the disc's reach, so it's kept at least halfway out.
    private CameraView FlatLocalAsOrbit()
    {
        Vector3 position = Position.LengthSquared() > 0 ? Position : Vector3.Up * PlanetRadius;
        GeoCoordinate direction = SphericalCoordinates.FromDirection(position.ToNumerics());
        float altitude = Mathf.Max(position.Length() / PlanetRadius - 1, 0.5f);
        return new CameraView(direction.LatitudeDegrees, direction.LongitudeDegrees, altitude,
            0, 0, 0);
    }

    private void StartBlend()
    {
        _blendFrom = Transform;
        _blend = 0.0f;
    }

    // The frame latitude and longitude are measured in: the planet's in the local view.
    private Basis Frame => IsLocalView ? SurfaceFrame : Basis.Identity;

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
        if (_flatLocal)
        {
            UpdateFlatTransform();
        }
        else
        {
            UpdateOrbitTransform();
        }

        if (_blend < 1.0f)
        {
            float eased = _blend * _blend * (3 - 2 * _blend);
            Transform = _blendFrom.InterpolateWith(Transform, eased);
        }
    }

    private void UpdateOrbitTransform()
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

    // Straight above the spot on the face, looking down, with the disc's center up on screen.
    private void UpdateFlatTransform()
    {
        float globe = GlobeRadius;
        var ground = new Vector3(_currentSpot.X * globe, (float)FlatDisc.HalfThickness * globe,
            _currentSpot.Y * globe);
        Vector3 above = ground + Vector3.Up * (_currentAltitude * PlanetRadius);
        Position = SurfaceFrame * above;
        LookAt(SurfaceFrame * ground, _currentUp);
    }
}
