using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Measurement;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Interop;
using NothicWorlds.Rendering;
using NothicWorlds.Session;
using NothicWorlds.UI;

namespace NothicWorlds.Controls;

/// <summary>
/// Standing on a world in first person (VISION.md REN-06; owner's choices: walk or fly, the sky
/// at true size with a Magnify switch, the live weather overhead, a compass and readouts).
/// Drag to look around; W, A, S, D walk (Shift for ten times faster); the mouse wheel changes
/// the speed; F switches between walking on the ground and flying, Space and C rise and sink
/// while flying; M magnifies small bodies in the sky; Esc goes back to the globe. The clock
/// keeps running, so the sun and moons cross the sky and the weather passes over.
/// </summary>
/// <remarks>
/// While standing, the scene is centered on the eye (<see cref="SystemView.StandingOn"/>) and
/// the ground right around it is drawn by a <see cref="FirstPersonGround"/>, both so the
/// ground near the eye keeps its precision. A <see cref="SurfaceSky"/> takes over the
/// background, and a <see cref="FirstPersonHud"/> shows the readouts. Must come last in the
/// scene: it takes the mouse and keys before the globe's camera and tools.
/// </remarks>
public partial class FirstPersonMode : Node
{
    private const double EyeHeightMeters = 1.7;
    private const float LookDegreesPerPixel = 0.2f;

    // The least time between rebuilds of the ground while it's only drifted a little.
    private const double MinGroundRebuildSeconds = 0.1;

    // The ground detail's coarsest noise size, and how many of those the noise repeats over:
    // GROUND_DETAIL_METERS and PATTERN_NOISE_SIZE in planet_surface.gdshaderinc.
    private const double GroundDetailMeters = 4;
    private const double GroundNoiseRepeat = 32;
    private const float FieldOfViewDegrees = 70;

    // How much of the ground built around the eye the globe's own mesh leaves unraised by
    // the water (PlanetSurface.SetNearEye): nearly all, so the water drawn up close meets the
    // globe's own water just inside the edge of what's built (past the horizon on a globe).
    private const double NearEyeShare = 0.95;

    // How far the water's waves repeat: WAVE_REPEAT_METERS in water_surface.gdshader.
    private const double WaveRepeatMeters = 1000;

    // How close the mouse must come to a body's disc to read it out, in pixels.
    private const float HoverPixels = 8;

    // How often the weather at the spot is looked up again, in seconds of real time.
    private const double WeatherSeconds = 0.5;

    // How far the air's haze reaches: half gone at this distance in clear air, in km.
    private const double ClearHazeKm = 40;

    // Speeds the mouse wheel steps through, in m/s, with their names.
    private static readonly (double MetersPerSecond, string Name)[] _speeds =
    [
        (1.4, "walking pace"), (6, "running pace"), (30, "car speed"), (250, "plane speed"),
        (3_000, "rocket speed"), (30_000, "meteor speed"), (300_000, "very fast"),
    ];

    private static readonly string[] _compass =
        ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
            "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"];

    // The minimap (VISION.md REN-08): a camera above the spot drawing into a small picture,
    // and how far across it reaches, in meters, stepping through these.
    private static readonly double[] _mapSpans =
        [500, 1_000, 2_000, 5_000, 10_000, 20_000, 50_000, 100_000, 200_000, 500_000,
            1_000_000, 2_000_000, 5_000_000, 10_000_000, 20_000_000, 50_000_000];
    private const int StartMapSpan = 6;  // 50 km
    private const int MapPictureSize = 256;
    private SubViewport? _overhead;
    private Camera3D? _overheadCamera;
    private int _mapSpan = StartMapSpan;

    private readonly SurfaceSky _sky = new();
    private readonly UnderwaterView _underwater = new();
    private readonly ShaderMaterial _waterMaterial =
        new() { Shader = GD.Load<Shader>("res://Rendering/water_surface.gdshader") };
    private Camera3D? _camera;
    private FirstPersonGround? _ground;
    private FirstPersonGround? _deck;  // The clouds below, when flying above them
    private FirstPersonGround? _water; // The water's surface, on a body with water
    private double? _waterBuiltRadius; // The water level it was built at, in radii
    private RiverWater? _rivers;       // The rivers' water around the eye
    private RiverChannels? _channels;  // ... and the channels they cut, as last built

    // Each river's water along its course (VISION.md BOD-11), worked out for the courses and
    // ground they were worked out from (they change rarely; the channels with every rebuild).
    private (IReadOnlyList<RiverCourseShown> Courses, int Relief, List<RiverProfile> Rivers)?
        _profiles;

    // Where there's no water over the ground, the water's surface built around the eye lies
    // this far under it, so it stays out of sight: this share of the distance from the eye
    // (the ground drawn farther out is coarser), and at least this, in meters. Near the shore
    // the water slopes down under the ground over a single step of the mesh.
    private const double DryShare = 0.002, MinDryMeters = 0.5;
    private FlatPatch? _flatGround;    // On a flat world, the ground around the eye
    private FlatPatch? _flatDeck;      // ... and the clouds below
    private FlatPatch? _flatWater;     // ... and the water's surface
    private double? _flatWaterBuilt;   // The water level it was built at, in radii
    private FlatSpot? _flat;           // Where the eye stands on a flat world (else on a globe)
    private double _flatBuiltHeightMeters;

    // On a flat world the ground patch sits this far off the disc's own face (in globe radii,
    // about 6 m on an Earth-sized world), so the two don't flicker against each other where
    // they'd lie in the same plane; the eye stands on the patch. Too small to see at the
    // patch's edge, many km away.
    private const double FlatGroundLift = 1e-6;
    private FlatFace _flatBuiltFace;
    private FirstPersonHud? _hud;
    private Guid? _bodyId;
    private Vector3D _spot;  // Unit direction on the body, its own frame
    private double _heading;  // Radians clockwise from north
    private double _pitch;    // Radians above level
    private double _heightMeters = EyeHeightMeters;  // Above the ground

    // Flying over a globe: the height kept, above the body's radius rather than the ground
    // (owner's choice), so a cliff passing underneath doesn't drop the eye; the ground only
    // pushes it up where it rises higher. And the ground's height under the eye.
    private double _altitudeMeters;
    private double _groundMeters;
    private bool _descending;
    private bool _flying;
    private bool _magnify;
    private bool _dragging;
    private Vector2 _mouse;
    private int _speed;
    private int _groundVersion = -1;
    private double _groundHeightMeters;  // The eye's height when the ground was built
    private double _sinceGroundBuilt;     // Seconds since the ground was last built
    private bool _deckStale = true;       // The cloud deck doesn't match the ground yet
    private bool _waterStale = true;      // The water's surface doesn't match the ground yet
    private float _savedRelief = 1;       // View ▸ Relief's exaggeration, back on leaving
    private WeatherSample? _weather;
    private double _weatherAge = double.PositiveInfinity;

    // What standing changed, to put back.
    private ProcessModeEnum _savedCameraMode;
    private readonly List<(CanvasItem Item, bool Visible)> _hiddenItems = [];
    private readonly List<(CanvasLayer Layer, bool Visible)> _hiddenLayers = [];

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The system view, which centers the scene on the eye while standing.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The globe's camera, current again when standing ends.</summary>
    [Export] public PlanetCamera? GlobeCamera { get; set; }

    /// <summary>The scene's environment, whose background becomes the sky.</summary>
    [Export] public WorldEnvironment? Environment { get; set; }

    /// <summary>The nebulas painted on the sky, for the night sky.</summary>
    [Export] public NebulaBackdrop? Nebulas { get; set; }

    /// <summary>Picks the spot to stand on with a click on the globe.</summary>
    [Export] public PinPlacer? Placer { get; set; }

    /// <summary>The live weather, for what's overhead and falling.</summary>
    [Export] public WeatherDisplay? Weather { get; set; }

    /// <summary>The message line, for why standing isn't possible.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>The time bar, which stays while standing (VISION.md REN-08).</summary>
    [Export] public TimeControls? TimeBar { get; set; }

    /// <summary>The Calendar tab, which opens over the view while standing (REN-08).</summary>
    [Export] public CalendarPanel? Calendar { get; set; }

    /// <summary>
    /// What to hide while standing (the globe's markers, labels, panels, and menus).
    /// </summary>
    [Export] public Godot.Collections.Array<Node> HideWhileStanding { get; set; } = [];

    /// <summary>True while standing on a world.</summary>
    public bool IsStanding => _bodyId is not null;

    public override void _Ready()
    {
        // Before the system view, so the eye it centers the scene on is this frame's.
        ProcessPriority = -10;
        _hud = new FirstPersonHud();
        AddChild(_hud);
        AddChild(_underwater);
        _hud.Minimap.Zoomed += step =>
            _mapSpan = Math.Clamp(_mapSpan + step, 0, _mapSpans.Length - 1);
        _hud.Minimap.Clicked += TravelOnMap;
        _hud.CalendarButton.Pressed += ToggleCalendar;
        if (Session is not null)
        {
            Session.WorldClosed += _ => Leave();
            Session.WaterChanged += () => _groundVersion = -1;  // Rivers and lakes moved
            Session.SelectionChanged += () =>
            {
                if (_bodyId is Guid id && Session.SelectedBodyId != id)
                {
                    Leave();
                }
            };
        }
    }

    /// <summary>
    /// Asks for a click on the selected planet or moon (owner's choice), then stands there.
    /// Esc cancels.
    /// </summary>
    public void ChooseWhereToStand()
    {
        if (Session is null)
        {
            return;
        }

        Body body = Session.SelectedBody;
        if (!body.HasSurface)
        {
            Toolbar?.ShowError("Only planets and moons can be stood on.");
            return;
        }

        if (Placer is null)
        {
            StandOnSelected();
            return;
        }

        Guid id = body.Id;
        Placer.Start(id, spot => StandAt(id, spot),
            prompt: $"Click where on {body.Name} to stand (Esc cancels).");
    }

    /// <summary>
    /// Stands on the selected body at the spot in the middle of the screen (or, if that misses
    /// the globe, the spot facing the camera).
    /// </summary>
    public void StandOnSelected()
    {
        if (Session is null || System is null || GlobeCamera is null)
        {
            return;
        }

        Body body = Session.SelectedBody;
        if (!body.HasSurface || System.SurfaceFor(body.Id) is not PlanetSurface globe)
        {
            Toolbar?.ShowError("Only planets and moons can be stood on.");
            return;
        }

        Vector2 middle = GlobeCamera.GetViewport().GetVisibleRect().Size / 2;
        Vector3? point = GlobePicker.PointAt(GlobeCamera, globe, middle);
        if (body.Shape == BodyShape.FlatDisc)
        {
            // On a flat world: the spot on its top face (or its middle, if that's missed).
            Vector3 onTop = point ?? Vector3.Zero;
            Enter(body.Id, new Vector3D(0, 1, 0),
                FlatWalk.OnTop(new Vector3D(onTop.X, onTop.Y, onTop.Z)));
            return;
        }

        Vector3 toward = point
            ?? (globe.GlobalTransform.AffineInverse() * GlobeCamera.GlobalPosition);
        Enter(body.Id, Unit(new Vector3D(toward.X, toward.Y, toward.Z)));
    }

    // Stands on a body at a spot clicked on its globe (on a flat world, the spot on its top
    // face that shows that place on the map).
    private void StandAt(Guid bodyId, GeoCoordinate spot)
    {
        if (Session?.World.Bodies.Find(b => b.Id == bodyId) is not Body body)
        {
            return;
        }

        Vector3D direction = SphericalPolygon.ToUnit(spot);
        if (body.Shape == BodyShape.FlatDisc)
        {
            Enter(bodyId, new Vector3D(0, 1, 0), FlatWalk.OnTop(FlatDisc.TopPointFor(direction)));
            return;
        }

        Enter(bodyId, direction);
    }

    /// <summary>Goes back to the globe, if standing.</summary>
    public void Leave()
    {
        if (_bodyId is null)
        {
            return;
        }

        if (System?.SurfaceFor(_bodyId.Value) is PlanetSurface globe)
        {
            globe.SetNearEye(null, 0);
        }

        _bodyId = null;
        if (System is not null)
        {
            System.StandingOn = null;
            System.ReliefExaggeration = _savedRelief;
        }

        _ground?.QueueFree();
        _ground = null;
        _deck?.QueueFree();
        _deck = null;
        _water?.QueueFree();
        _water = null;
        _waterBuiltRadius = null;
        _rivers?.QueueFree();
        _rivers = null;
        _channels = null;
        _profiles = null;
        _underwater.Visible = false;
        _flatGround?.QueueFree();
        _flatGround = null;
        _flatDeck?.QueueFree();
        _flatDeck = null;
        _flatWater?.QueueFree();
        _flatWater = null;
        _flatWaterBuilt = null;
        _camera?.QueueFree();
        _camera = null;
        _overhead?.QueueFree();
        _overhead = null;
        _overheadCamera = null;
        if (TimeBar is not null)
        {
            TimeBar.KeepShown = false;
        }

        if (Calendar is not null)
        {
            Calendar.KeepShown = false;
        }
        if (GlobeCamera is not null)
        {
            GlobeCamera.ProcessMode = _savedCameraMode;
            GlobeCamera.MakeCurrent();
        }

        _sky.Hide();
        foreach ((CanvasItem item, bool visible) in _hiddenItems)
        {
            item.Visible = visible;
        }

        foreach ((CanvasLayer layer, bool visible) in _hiddenLayers)
        {
            layer.Visible = visible;
        }

        _hiddenItems.Clear();
        _hiddenLayers.Clear();
        _hud!.Visible = false;
    }

    private void Enter(Guid bodyId, Vector3D spot, FlatSpot? flat = null)
    {
        Leave();
        _bodyId = bodyId;
        _spot = spot;
        _flat = flat;
        _heading = 0;
        _pitch = 0;
        _flying = false;
        _heightMeters = EyeHeightMeters;
        _groundVersion = -1;
        _weather = null;
        _weatherAge = double.PositiveInfinity;

        // Standing, the ground is always at its true height (owner's choice): View ▸ Relief's
        // exaggeration comes back on leaving.
        if (System is not null)
        {
            _savedRelief = System.ReliefExaggeration;
            System.ReliefExaggeration = 1;
        }

        _waterMaterial.SetShaderParameter("flat_disc", flat is not null);
        _camera = new Camera3D { Fov = FieldOfViewDegrees, Name = "FirstPersonCamera" };
        AddChild(_camera);
        _camera.MakeCurrent();
        BuildOverhead();
        if (TimeBar is not null)
        {
            TimeBar.KeepShown = true;
        }

        if (Calendar is not null)
        {
            Calendar.KeepShown = true;
        }
        if (GlobeCamera is not null)
        {
            // Paused, or W/A/S/D would pan it off the globe while walking.
            _savedCameraMode = GlobeCamera.ProcessMode;
            GlobeCamera.ProcessMode = ProcessModeEnum.Disabled;
        }

        _ground = new FirstPersonGround();
        AddChild(_ground);
        _deck = new FirstPersonGround
        {
            Name = "CloudDeck",
            MaterialOverride = _sky.DeckMaterial,
            Visible = false,
        };
        AddChild(_deck);
        _water = new FirstPersonGround
        {
            Name = "WaterSurface",
            MaterialOverride = _waterMaterial,
            Visible = false,
        };
        AddChild(_water);
        _rivers = new RiverWater { Visible = flat is null };
        AddChild(_rivers);
        if (flat is not null)
        {
            _flatGround = new FlatPatch { Name = "FlatGround" };
            AddChild(_flatGround);
            _flatDeck = new FlatPatch
            {
                Name = "FlatCloudDeck",
                MaterialOverride = _sky.DeckMaterial,
                Visible = false,
            };
            AddChild(_flatDeck);
            _flatWater = new FlatPatch
            {
                Name = "FlatWaterSurface",
                MaterialOverride = _waterMaterial,
                Visible = false,
            };
            AddChild(_flatWater);
        }

        if (Environment is not null)
        {
            _sky.Show(Environment);
        }

        foreach (Node node in HideWhileStanding)
        {
            if (node is CanvasItem item)
            {
                _hiddenItems.Add((item, item.Visible));
                item.Visible = false;
            }
            else if (node is CanvasLayer layer)
            {
                _hiddenLayers.Add((layer, layer.Visible));
                layer.Visible = false;
            }
        }

        _hud!.Visible = true;
        ShowHelp();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_bodyId is null)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _dragging = button.Pressed;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                _speed = Math.Min(_speed + 1, _speeds.Length - 1);
                ShowHelp();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                _speed = Math.Max(_speed - 1, 0);
                ShowHelp();
                break;
            case InputEventMouseMotion motion:
                _mouse = motion.Position;
                if (_dragging)
                {
                    Look(motion.Relative);
                }

                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }:
                Leave();
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.F }:
                _flying = !_flying;
                _heightMeters = _flying
                    ? Math.Max(_heightMeters, EyeHeightMeters)
                    : EyeHeightMeters;
                _altitudeMeters = _groundMeters + _heightMeters;
                ShowHelp();
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.T }:
                ToggleCalendar();
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.M }:
                _magnify = !_magnify;
                ShowHelp();
                break;
            case InputEventMouse or InputEventKey:
                break;  // Taken, so the globe's camera and tools stay still
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (_bodyId is not Guid id || Session is null || System is null || _camera is null)
        {
            return;
        }

        Body? body = Session.World.Bodies.Find(b => b.Id == id);
        if (body is null || System.SurfaceFor(id) is not PlanetSurface globe
            || !System.Layout.TryGetValue(id, out DisplayBody place))
        {
            Leave();
            return;
        }

        Move(body, delta);
        _sinceGroundBuilt += delta;
        double radiusMeters = body.RadiusKm * 1000;
        if (_flat is null)
        {
            KeepGroundBuilt(globe, body);
        }

        // The ground's lift under the eye, in radii: on a globe, above its radius; on a flat
        // world's top face, above the face (its rim and underside are bare rock).
        double groundLift = _flat is FlatSpot here
            ? here.Face == FlatFace.Top
                ? FlatCarvedAt(globe, FlatWalk.Point(here), body)
                : 0
            : GroundRadius(globe, body) - 1;
        double groundRadius = 1 + groundLift;
        _groundMeters = groundLift * radiusMeters;
        if (_flying)
        {
            // Descending stops at the ground; otherwise the ground only lifts the eye.
            if (_descending)
            {
                _altitudeMeters = Math.Max(_altitudeMeters, _groundMeters + EyeHeightMeters);
            }

            _heightMeters = Math.Max(EyeHeightMeters, _altitudeMeters - _groundMeters);
        }

        Vector3D eye = _flat is FlatSpot standing
            ? FlatPoint(globe, standing, _heightMeters / radiusMeters, body)
            : _spot * (groundRadius + _heightMeters / radiusMeters);

        // How far the eye is above the base the water level is measured from, where there can
        // be water (not on a flat world's rim or underside).
        double? eyeLift = _flat is { Face: not FlatFace.Top }
            ? null
            : groundLift + _heightMeters / radiusMeters;
        System.StandingOn = id;
        System.StandingEye = eye;

        double time = Session.World.TimeDays;
        (Vector3D East, Vector3D North, Vector3D Up) frame = Frame(body, time);
        double clearance = Clearance(globe, eyeLift, radiusMeters);
        PlaceCamera(frame, clearance / radiusMeters * place.Radius);
        PlaceOverhead(frame, body, place.Radius);
        if (_flat is FlatSpot flat)
        {
            PlaceFlatGround(globe, body, time, eye, place.Radius, flat);
        }
        else
        {
            PlaceGround(body, time, eye, place.Radius);
        }

        UpdateWeather(id, time, delta);
        SkyView? seen = _flat is FlatSpot spot
            ? SkyView.FromFlat(Session.World.Bodies, body, spot,
                (_groundMeters + _heightMeters) / 1000, time)
            : SkyView.From(Session.World.Bodies, body, SphericalPolygon.FromUnit(_spot),
                (groundRadius - 1) * body.RadiusKm + _heightMeters / 1000, time);
        if (seen is not SkyView sky)
        {
            return;
        }

        _sky.ShowBodies(Session.World.Bodies, body, sky, frame, _magnify, Nebulas?.SkyTexture);
        _sky.ShowStars(Nebulas?.Stars, Nebulas?.ShowConstellations ?? true);
        Basis toBody = BodyBasis(body, time).Transposed();
        double sunAltitude = sky.Star?.AltitudeDegrees ?? -90;
        double unitsPerKm = place.Radius / body.RadiusKm;
        bool clouds = _flat is FlatSpot on
            ? _sky.ShowFlatClouds(globe, body, toBody, on, eye, sunAltitude, unitsPerKm)
            : _sky.ShowClouds(globe, body, toBody, eye, groundRadius, sunAltitude, unitsPerKm);
        bool deck = clouds && !BelowClouds() && !globe.IsCarved;
        _deck!.Visible = deck && _flat is null;
        if (_flatDeck is not null)
        {
            _flatDeck.Visible = deck && _flat is { Face: FlatFace.Top };
        }
        _sky.ShowHaze(body.HasAtmosphere, place.Radius / body.RadiusKm, HazeKm(),
            sky.Star?.AltitudeDegrees ?? -90, _weather?.CloudCover ?? 0);
        ShowWater(globe, eyeLift, radiusMeters, place.Radius, sunAltitude, unitsPerKm);
        ShowReadouts(body, time, sky, frame);
    }

    // Turns the view by a drag of the mouse.
    private void Look(Vector2 drag)
    {
        _heading += double.DegreesToRadians(drag.X * LookDegreesPerPixel);
        _pitch = Math.Clamp(_pitch - double.DegreesToRadians(drag.Y * LookDegreesPerPixel),
            -Math.PI / 2 + 0.01, Math.PI / 2 - 0.01);
    }

    // Walks or flies by the keys held, over the body's surface along great circles.
    private void Move(Body body, double delta)
    {
        double forward = Held(Key.W) - Held(Key.S), sideways = Held(Key.D) - Held(Key.A);
        double rise = _flying ? Held(Key.Space) - Held(Key.C) : 0;
        double speed = _speeds[_speed].MetersPerSecond
            * (Input.IsKeyPressed(Key.Shift) ? 10 : 1) * delta;
        _descending = rise < 0;
        if (rise != 0)
        {
            // Rising goes faster the higher you are, so flying up off a world doesn't take hours.
            double step = rise * Math.Max(speed, _heightMeters * delta);
            if (_flat is null)
            {
                _altitudeMeters += step;
            }
            else
            {
                _heightMeters = Math.Max(EyeHeightMeters, _heightMeters + step);
            }
        }

        if (forward == 0 && sideways == 0)
        {
            return;
        }

        double bearing = _heading + Math.Atan2(sideways, forward);
        double length = Math.Sqrt(forward * forward + sideways * sideways);
        if (_flat is FlatSpot flat)
        {
            // Over the face, the rim, and the underside alike (FlatWalk keeps north toward
            // the top's center, so the heading carries over the edge).
            _flat = FlatWalk.Walk(flat, bearing, speed * length / (body.RadiusKm * 1000));
            return;
        }

        double angle = speed * length / (body.RadiusKm * 1000 + _heightMeters);
        _spot = GlobeWalk.Walk(_spot, bearing, angle);
    }

    // How far out the drawn ground is under the eye, in the body's radii: the sculpted ground,
    // or on a carved globe, the carving straight below.
    private double GroundRadius(PlanetSurface globe, Body body)
    {
        if (globe.IsCarved)
        {
            var above = new Vector3((float)_spot.X, (float)_spot.Y, (float)_spot.Z);
            if (globe.CarvedHit(above * 1.5f, -above) is Vector3 hit)
            {
                return hit.Length();
            }
        }

        // Never under the drawn ground, which can stand above the ground's height between its
        // points (see FirstPersonGround.HighestAround).
        double ground = CarvedRadius(globe, body, _spot);
        return _ground?.HighestAround(_spot) is double drawn ? Math.Max(ground, drawn) : ground;
    }

    // How far the eye is from the nearest surface it could look at closely, in meters: the
    // ground below, or the water's surface above or below it (never under eye height). The
    // near clipping distance follows it, so over deep water the surface just below isn't cut
    // away by a near distance set by the bottom, kilometers down.
    private double Clearance(PlanetSurface globe, double? eyeLift, double radiusMeters)
    {
        if (eyeLift is not double lift || globe.IsCarved
            || WaterRadiusHere(globe, radiusMeters / 1000) is not double water)
        {
            return _heightMeters;
        }

        double fromWater = Math.Abs(lift - (water - 1)) * radiusMeters;
        return Math.Min(_heightMeters, Math.Max(fromWater, EyeHeightMeters));
    }

    // The camera at the scene's middle (the eye), looking along the heading and pitch.
    private void PlaceCamera((Vector3D East, Vector3D North, Vector3D Up) frame,
        double eyeHeight)
    {
        Vector3D look = Look(frame);
        Vector3D right = frame.East * Math.Cos(_heading) - frame.North * Math.Sin(_heading);
        Vector3D cameraUp = Cross(right, look);
        _camera!.GlobalTransform = new Transform3D(
            new Basis(ToGodot(right), ToGodot(cameraUp), ToGodot(look * -1)), Vector3.Zero);

        // The near distance follows the eye's clearance; far stays within the depth range the
        // engine can build (see SystemView.FitCamera).
        // A tenth of it (about 17 cm standing), so ground or a cliff right in front isn't
        // cut away; the view still reaches 170 km, past the horizon from the ground.
        float near = (float)Math.Max(eyeHeight * 0.1, 1e-9);
        _camera.Near = near;
        _camera.Far = near * 1e6f;
    }

    // The way the view looks, in the scene's frame.
    private Vector3D Look((Vector3D East, Vector3D North, Vector3D Up) frame)
    {
        Vector3D level = frame.North * Math.Cos(_heading) + frame.East * Math.Sin(_heading);
        return level * Math.Cos(_pitch) + frame.Up * Math.Sin(_pitch);
    }

    // The rings of ground around the eye, and the cloud deck's: built again when the eye has
    // moved a good part of its height away from their middle, risen or sunk by half, or the
    // ground changed (they reach past the horizon, which moves out as the eye rises); placed
    // relative to the eye. Moving fast, that would be every frame, which took the frame rate
    // down to a dozen a second: then they're built at most every MinGroundRebuildSeconds,
    // unless the eye has gone a good way out across them.
    private void PlaceGround(Body body, double time, Vector3D eye, double displayRadius)
    {
        PlaceRings(_ground!, body, time, eye, displayRadius);
        PlaceRings(_deck!, body, time, eye, displayRadius);
        PlaceRings(_water!, body, time, eye, displayRadius);
        PlaceRivers(body, time, eye, displayRadius);
    }

    // Puts the rivers' water in the scene, relative to the eye.
    private void PlaceRivers(Body body, double time, Vector3D eye, double displayRadius)
    {
        if (_rivers is not { Mesh: not null } rivers)
        {
            return;
        }

        rivers.GlobalTransform = new Transform3D(
            BodyBasis(body, time).Scaled(Vector3.One * (float)displayRadius),
            ToGodot(ToSystem(body, time, rivers.Middle - eye) * displayRadius));
        rivers.SetScale(body.RadiusKm * 1000 / displayRadius);
    }

    // The rivers around `eye` (a direction on the globe, or the one a flat world's spot
    // stands for), out to `outer` radians, and the channels they cut (VISION.md BOD-11), or
    // null with none.
    private RiverChannels? ChannelsAround(PlanetSurface globe, Body body, Vector3D eye,
        double outer)
    {
        IReadOnlyList<RiverCourseShown> courses = Session?.WaterOn(body.Id)?.Rivers ?? [];
        if (courses.Count == 0)
        {
            _profiles = null;
            return null;
        }

        double radiusMeters = body.RadiusKm * 1000;
        if (_profiles is not var (known, relief, _) || !ReferenceEquals(known, courses)
            || relief != globe.ReliefVersion)
        {
            List<RiverProfile> rivers = [.. courses
                .Select(course => RiverProfile.For(course, body.RadiusKm,
                    direction => (globe.GroundRadiusAt(direction, body.RadiusKm) - 1)
                        * radiusMeters))
                .OfType<RiverProfile>()];
            _profiles = (courses, globe.ReliefVersion, rivers);
        }

        return RiverChannels.Near(_profiles.Value.Rivers, eye, body.RadiusKm,
            outer * radiusMeters,
            direction => (globe.GroundRadiusAt(direction, body.RadiusKm) - 1) * radiusMeters);
    }

    // How far out the drawn ground is at a direction, in radii, with the rivers' channels
    // cut into it.
    private double CarvedRadius(PlanetSurface globe, Body body, Vector3D direction)
    {
        double ground = globe.GroundRadiusAt(direction, body.RadiusKm);
        if (_channels is null)
        {
            return ground;
        }

        double radiusMeters = body.RadiusKm * 1000;
        return 1 + _channels.Carve(direction, (ground - 1) * radiusMeters) / radiusMeters;
    }

    // The height the water's surface is built at, in radii above the base: the water's,
    // where it stands above the ground (before rivers' channels are cut: those hold the
    // river's water, not the sea's), else out of sight just under the drawn (carved) ground,
    // `away` radii from the eye.
    private static double WaterOrUnder(double? water, double ground, double carved,
        double away, Body body)
    {
        if (water is double level && level > ground)
        {
            return level;
        }

        double radiusMeters = body.RadiusKm * 1000;
        return carved - Math.Max(MinDryMeters, DryShare * away * radiusMeters) / radiusMeters;
    }

    // How far out the water's surface is over the eye, in radii: the sea's, or a lake's
    // (VISION.md BOD-11), whichever is higher; null with neither, or where the ground (before
    // rivers' channels are cut) stands above it.
    private double? WaterRadiusHere(PlanetSurface globe, double radiusKm)
    {
        if (_flat is FlatSpot flat)
        {
            return FlatWalk.MapDirection(flat) is Vector3D seen
                && globe.WaterRadiusAt(seen) is double flatWater
                && flatWater - 1 > FlatGroundAt(globe, FlatWalk.Point(flat), radiusKm)
                    ? flatWater
                    : null;
        }

        return globe.WaterRadiusAt(_spot) is double water
            && water > globe.GroundRadiusAt(_spot, radiusKm)
                ? water
                : null;
    }

    // Under the water (VISION.md BOD-09), with the eye <paramref name="eyeRadius"/> radii out:
    // the murk, tint, and ripples of light. Above it, none.
    private void ShowWater(PlanetSurface globe, double? eyeLift, double radiusMeters,
        double displayRadius, double sunAltitude, double unitsPerKm)
    {
        _waterMaterial.SetShaderParameter("meters_per_unit",
            (float)(radiusMeters / displayRadius));
        _waterMaterial.SetShaderParameter("daylight", (float)SurfaceSky.Daylight(sunAltitude));
        globe.CopyWaterColorsTo(_waterMaterial);
        if (eyeLift is double lift && !globe.IsCarved
            && WaterRadiusHere(globe, radiusMeters / 1000) is double water && lift < water - 1)
        {
            double depthMeters = (water - 1 - lift) * radiusMeters;
            Color? color = WaterColorHere();
            _sky.ShowUnderwater(unitsPerKm, depthMeters, sunAltitude, color);
            _underwater.SetWaterColor(color);
            _underwater.Visible = true;
        }
        else
        {
            _underwater.Visible = false;
        }
    }

    // The color of the water the eye is in: its terrain's, where a water terrain is painted
    // underfoot, else null (the standard blue-green).
    private Color? WaterColorHere()
    {
        if (Session?.World.Bodies.Find(b => b.Id == _bodyId) is not Body body)
        {
            return null;
        }

        Vector3D? place = _flat is FlatSpot flat ? FlatWalk.MapDirection(flat) : _spot;
        if (place is not Vector3D direction)
        {
            return null;
        }

        byte code = body.Surface.Terrain.CodeAt(direction);
        TerrainType? underfoot = Session.World.TerrainTypes.Find(type => type.Code == code);
        return underfoot?.Climate == ClimateKind.Water ? underfoot.Color.ToGodot() : null;
    }

    // Builds the ground (and cloud deck) again when needed (see PlaceGround). Done before the
    // eye is placed on it, so the eye is always measured against the ground that's drawn:
    // built after, the eye could be a frame below the new ground and see through it.
    private void KeepGroundBuilt(PlanetSurface globe, Body body)
    {
        FirstPersonGround ground = _ground!;
        ground.Visible = !globe.IsCarved;  // A carved globe draws its own carving
        _water!.Visible = !globe.IsCarved && (globe.WaterRadius is not null || globe.HasLakes);
        _rivers!.Visible = !globe.IsCarved;
        if (globe.IsCarved)
        {
            return;
        }

        double height = _heightMeters / (body.RadiusKm * 1000);
        double horizon = Math.Acos(1 / (1 + height));
        double outer = Math.Clamp(horizon * 4, 0.003, 0.6);
        double moved = Math.Acos(Math.Clamp(ground.Center.Dot(_spot), -1, 1));
        double risen = _heightMeters / _groundHeightMeters;
        bool drifted = moved > Math.Max(height * 0.25, 1e-7)
            && (_sinceGroundBuilt >= MinGroundRebuildSeconds || moved > outer * 0.3);
        if (ground.Mesh is null || globe.ReliefVersion != _groundVersion
            || drifted || risen > 1.5 || risen < 1 / 1.5)
        {
            _sinceGroundBuilt = 0;
            _channels = ChannelsAround(globe, body, _spot, outer);
            ground.Build(direction => CarvedRadius(globe, body, direction), _spot,
                Math.Max(height * 0.5, 2e-7), outer);
            double radiusMeters = body.RadiusKm * 1000;
            _rivers!.Build(_channels, ground.Center * ground.CenterRadius,
                (direction, meters) => direction * (1 + meters / radiusMeters), radiusMeters);
            ground.MaterialOverride = globe.MaterialOverride;
            SetGroundDetail(ground.MaterialOverride, ground.Center * ground.CenterRadius,
                body.RadiusKm);
            globe.SetNearEye(ground.Center, outer * NearEyeShare);
            _groundVersion = globe.ReliefVersion;
            _groundHeightMeters = _heightMeters;
            _deckStale = true;
            _waterStale = true;
        }

        // The water's surface: the same rings at the sea's level, or a lake's (VISION.md
        // BOD-11), and out of sight under the ground where there's neither.
        if (_water.Visible && (_waterStale || globe.WaterRadius != _waterBuiltRadius))
        {
            _waterStale = false;
            _waterBuiltRadius = globe.WaterRadius;
            Vector3D center = ground.Center;
            _water.Build(direction => WaterOrUnder(globe.WaterRadiusAt(direction) - 1,
                    globe.GroundRadiusAt(direction, body.RadiusKm) - 1,
                    CarvedRadius(globe, body, direction) - 1,
                    Math.Acos(Math.Clamp(direction.Dot(center), -1, 1)), body) + 1,
                center, Math.Max(height * 0.5, 2e-7), outer);
            SetWaveOrigin(ground.Center * _water.CenterRadius, body.RadiusKm);
        }

        // The cloud deck: the same rings at the cloud layer, over the ground here. It's only
        // seen from above the clouds, so it's built only then.
        if (_deckStale && !BelowClouds())
        {
            _deckStale = false;
            double layer = ground.CenterRadius + SurfaceSky.CloudHeightKm / body.RadiusKm;
            _deck!.Build(_ => layer, ground.Center, 2e-7, outer);
        }
    }

    // Tells the ground's material where the point its patch is built around falls in the fine
    // ground detail's noise (planet_surface.gdshaderinc): worked out here in double precision
    // and wrapped to the noise's repeat, so the detail stays put on the ground as it's rebuilt.
    private static void SetGroundDetail(Material? material, Vector3D anchor, double radiusKm)
    {
        if (material is not ShaderMaterial shader)
        {
            return;
        }

        double radiusMeters = radiusKm * 1000;
        float Wrapped(double radii)
        {
            double units = radii * radiusMeters / GroundDetailMeters;
            return (float)(units - Math.Floor(units / GroundNoiseRepeat) * GroundNoiseRepeat);
        }

        shader.SetShaderParameter("ground_detail_origin",
            new Vector3(Wrapped(anchor.X), Wrapped(anchor.Y), Wrapped(anchor.Z)));
        shader.SetShaderParameter("ground_radius_meters", (float)radiusMeters);
    }

    // Tells the water's material where the point its rings are built around falls in the
    // waves' repeat, worked out in double precision, so the waves stay put as it's rebuilt.
    private void SetWaveOrigin(Vector3D anchor, double radiusKm)
    {
        double radiusMeters = radiusKm * 1000;
        float Wrapped(double radii)
        {
            double meters = radii * radiusMeters;
            return (float)(meters - Math.Floor(meters / WaveRepeatMeters) * WaveRepeatMeters);
        }

        _waterMaterial.SetShaderParameter("wave_origin",
            new Vector3(Wrapped(anchor.X), Wrapped(anchor.Y), Wrapped(anchor.Z)));
        _waterMaterial.SetShaderParameter("radius_meters", (float)radiusMeters);
    }

    // On a flat world, the patch of ground around the eye (the map on top, bare rock on the
    // rim and underside) and the cloud deck over the top: built again when the eye has moved a
    // good part of its height away from their middle, onto another face, or risen or sunk by
    // half; placed relative to the eye.
    private void PlaceFlatGround(PlanetSurface globe, Body body, double time, Vector3D eye,
        double displayRadius, FlatSpot flat)
    {
        _ground!.Visible = false;
        FlatPatch ground = _flatGround!;
        ground.Visible = !globe.IsCarved;  // A carved disc draws its own carving
        double height = _heightMeters / (body.RadiusKm * 1000);
        Vector3D underfoot = FlatPoint(globe, flat, 0, body);
        double risen = _heightMeters / _flatBuiltHeightMeters;
        double outer = Math.Clamp(height * 400, 0.003, 2 * FlatDisc.Radius);
        if (ground.Mesh is null || flat.Face != _flatBuiltFace
            || globe.ReliefVersion != _groundVersion
            || (ground.Middle - underfoot).Length > Math.Max(height * 0.25, 1e-7)
            || risen > 1.5 || risen < 1 / 1.5)
        {
            // No horizon on a flat face: the patch reaches well past what the eye sees sharply,
            // and the disc's own mesh beyond it.
            // (Hidden on a carved disc, so not lifted there: that would cast a ray per point.)
            _channels = FlatWalk.MapDirection(flat) is Vector3D seen && !globe.IsCarved
                ? ChannelsAround(globe, body, seen, outer)
                : null;
            ground.Build(flat, outer, FlatGroundLift, ground: globe.IsCarved ? null
                : point => FlatCarvedAt(globe, point, body), rimLift: globe.RimLift);
            double radiusMeters = body.RadiusKm * 1000;
            _rivers!.Build(_channels, ground.Middle, (direction, meters) =>
                FlatDisc.TopPointFor(direction) + new Vector3D(0, FlatGroundLift
                    + Math.Max(meters / radiusMeters, PlanetSurface.FlatDeepestLift), 0),
                radiusMeters);
            globe.SetNearEye(flat.Face == FlatFace.Top ? ground.Middle : null,
                outer * NearEyeShare);
            _groundVersion = globe.ReliefVersion;
            _flatWaterBuilt = null;
            ground.MaterialOverride = flat.Face == FlatFace.Top
                ? globe.MaterialOverride
                : PlanetSurface.RockMaterial;
            SetGroundDetail(ground.MaterialOverride, ground.Middle, body.RadiusKm);
            if (flat.Face == FlatFace.Top)
            {
                // The deck carries each point's map direction, as the globe's deck does.
                _flatDeck!.Build(flat, outer, SurfaceSky.CloudHeightKm / body.RadiusKm,
                    point => FlatDisc.DirectionFor(point));
            }

            _flatBuiltHeightMeters = _heightMeters;
            _flatBuiltFace = flat.Face;
        }

        // The water's surface over the top face, at the sea's level or a lake's (VISION.md
        // BOD-11), and out of sight under the ground where there's neither.
        FlatPatch water = _flatWater!;
        water.Visible = flat.Face == FlatFace.Top && !globe.IsCarved
            && (globe.WaterRadius is not null || globe.HasLakes);
        _rivers!.Visible = flat.Face == FlatFace.Top && !globe.IsCarved;
        double sea = globe.WaterRadius ?? 0;
        if (water.Visible && sea != _flatWaterBuilt)
        {
            _flatWaterBuilt = sea;
            Vector3D middle = ground.Middle;
            water.Build(flat, outer, FlatGroundLift, ground: point => Math.Max(WaterOrUnder(
                    globe.WaterRadiusAt(FlatDisc.DirectionFor(point)) - 1,
                    FlatGroundAt(globe, point, body.RadiusKm), FlatCarvedAt(globe, point, body),
                    new Vector3D(point.X - middle.X, 0, point.Z - middle.Z).Length, body),
                PlanetSurface.FlatDeepestLift));
            SetWaveOrigin(water.Middle, body.RadiusKm);
        }

        PlacePatch(ground, body, time, eye, displayRadius);
        PlacePatch(_flatDeck!, body, time, eye, displayRadius);
        PlacePatch(water, body, time, eye, displayRadius);
        PlaceRivers(body, time, eye, displayRadius);
    }

    // Where a spot on a flat world is, `above` globe radii off its drawn ground: on the top
    // face, lifted by the ground there (VISION.md BOD-10), rivers' channels cut in; on the
    // rim, stretched to meet it.
    private Vector3D FlatPoint(PlanetSurface globe, FlatSpot spot, double above, Body body)
    {
        Vector3D point = FlatPatch.OnRim(FlatWalk.Point(spot, FlatGroundLift + above), spot.Face,
            globe.RimLift);
        return spot.Face == FlatFace.Top
            ? point + new Vector3D(0, FlatCarvedAt(globe, point, body), 0)
            : point;
    }

    // How far the drawn ground is lifted at a point of a flat world's top face, in radii: on
    // a disc carved by shapes (VISION.md BOD-10), the carving straight below (the face's own
    // height where a hole goes right through).
    private static double FlatGroundAt(PlanetSurface globe, Vector3D topPoint, double radiusKm)
    {
        if (globe.IsCarved)
        {
            var above = new Vector3((float)topPoint.X, 1, (float)topPoint.Z);
            return globe.CarvedHit(above, Vector3.Down) is Vector3 hit
                ? hit.Y - FlatDisc.HalfThickness
                : 0;
        }

        return globe.GroundRadiusAt(FlatDisc.DirectionFor(topPoint), radiusKm) - 1;
    }

    // How far the drawn ground is lifted at a point of a flat world's top face, in radii,
    // with the rivers' channels cut into it.
    private double FlatCarvedAt(PlanetSurface globe, Vector3D topPoint, Body body)
    {
        double lift = FlatGroundAt(globe, topPoint, body.RadiusKm);
        if (_channels is null)
        {
            return lift;
        }

        double radiusMeters = body.RadiusKm * 1000;
        double carved = _channels.Carve(FlatDisc.DirectionFor(topPoint), lift * radiusMeters);
        return Math.Max(carved / radiusMeters, PlanetSurface.FlatDeepestLift);
    }

    // Puts a flat world's patch in the scene, relative to the eye.
    private static void PlacePatch(FlatPatch patch, Body body, double time, Vector3D eye,
        double displayRadius)
    {
        if (patch.Mesh is null)
        {
            return;
        }

        patch.GlobalTransform = new Transform3D(
            BodyBasis(body, time).Scaled(Vector3.One * (float)displayRadius),
            ToGodot(ToSystem(body, time, patch.Middle - eye) * displayRadius));
    }

    // Puts rings built around a spot in the scene, relative to the eye.
    private static void PlaceRings(FirstPersonGround rings, Body body, double time, Vector3D eye,
        double displayRadius)
    {
        if (rings.Mesh is null)
        {
            return;
        }

        Vector3D offset = rings.Center * rings.CenterRadius - eye;
        rings.GlobalTransform = new Transform3D(
            BodyBasis(body, time).Scaled(Vector3.One * (float)displayRadius),
            ToGodot(ToSystem(body, time, offset) * displayRadius));
    }

    // Looks up the live weather at the spot now and then (it changes slowly).
    private void UpdateWeather(Guid id, double time, double delta)
    {
        _weatherAge += delta;
        if (_weatherAge < WeatherSeconds)
        {
            return;
        }

        _weatherAge = 0;
        // On a flat world, only the top face has weather (the rim and underside are rock).
        Vector3D? at = _flat is FlatSpot flat ? FlatWalk.MapDirection(flat) : _spot;
        _weather = at is Vector3D direction
            ? Weather?.WeatherOf(id)?.At(time).SampleAt(direction)
            : null;
    }

    // The haze's reach: shorter in rain, and shorter still in snow.
    private double HazeKm()
    {
        if (_weather is not WeatherSample weather || !BelowClouds())
        {
            return ClearHazeKm;
        }

        double thickness = weather.Precipitation switch
        {
            PrecipitationKind.Rain => 2,
            PrecipitationKind.Snow => 6,
            _ => 0,
        };
        return Math.Max(1.5, ClearHazeKm / (1 + thickness * weather.PrecipitationMmPerHour));
    }

    private bool BelowClouds() => _heightMeters < SurfaceSky.CloudHeightKm * 1000;

    // The compass, the readout for the body under the mouse, the panel, and what's falling.
    private void ShowReadouts(Body body, double time, SkyView sky,
        (Vector3D East, Vector3D North, Vector3D Up) frame)
    {
        FirstPersonHud hud = _hud!;
        hud.SetHeading(double.RadiansToDegrees(_heading));
        hud.SetHover(_dragging ? null : HoverText(sky, frame), _mouse);
        hud.SetInfo(InfoText(body, time, sky));

        double light = sky.Star is SkyBody star
            ? Math.Clamp((star.AltitudeDegrees + 6) / 12, 0.08, 1)
            : 0.08;
        // Rain and snow fall through the air, not under the water.
        if (_weather is WeatherSample weather && body.HasAtmosphere && BelowClouds()
            && !_underwater.Visible)
        {
            double amount = Math.Clamp(weather.PrecipitationMmPerHour / 4, 0, 1);
            double fall = weather.Precipitation == PrecipitationKind.Snow ? 1.5 : 8;
            double across = weather.WindEastMs * Math.Cos(_heading)
                - weather.WindNorthMs * Math.Sin(_heading);
            hud.SetFalling(weather.Precipitation == PrecipitationKind.Rain ? amount : 0,
                weather.Precipitation == PrecipitationKind.Snow ? amount : 0,
                Math.Clamp(across / fall, -1.5, 1.5), light);
        }
        else
        {
            hud.SetFalling(0, 0, 0, light);
        }
    }

    // The body under the mouse: its name, where it stands, how far, and how lit.
    private string? HoverText(SkyView sky, (Vector3D East, Vector3D North, Vector3D Up) frame)
    {
        Vector3 ray = _camera!.ProjectRayNormal(_mouse);
        var toward = new Vector3D(ray.X, ray.Y, ray.Z);
        double up = toward.Dot(frame.Up);
        float pixelDegrees = FieldOfViewDegrees / GetViewport().GetVisibleRect().Size.Y;
        if (sky.BodyAt(toward.Dot(frame.East), toward.Dot(frame.North), up,
                _magnify ? SurfaceSky.MagnifiedRadiusDegrees : 0, HoverPixels * pixelDegrees)
            is not SkyBody body || body.AltitudeDegrees < -1)
        {
            return null;
        }

        string lit = body.Kind == BodyKind.Star ? "" : $" · {body.LitFraction * 100:0}% lit";
        return $"{body.Name}\n{body.AltitudeDegrees:0}° up · {body.AzimuthDegrees:0}° " +
            $"{CompassPoint(body.AzimuthDegrees)} · {Distance(body.DistanceKm)}{lit}";
    }

    // The date, the sun's time and height, the weather here, and the height when flying.
    private string InfoText(Body body, double time, SkyView sky)
    {
        var lines = new List<string> { BodyClock.Describe(body, time) };
        if (sky.Star is SkyBody star)
        {
            // No sun time on a flat world: the sun doesn't cross a meridian over a disc.
            string sunTime = sky.SolarTimeHours is double hours
                ? $"Sun time {(int)(hours * 60) % 1440 / 60}:{(int)(hours * 60) % 60:00} · "
                : "";
            lines.Add($"{sunTime}{star.Name} {Math.Abs(star.AltitudeDegrees):0}° " +
                (star.AltitudeDegrees >= 0 ? "up" : "below the horizon"));
        }

        if (_weather is WeatherSample weather && body.HasAtmosphere)
        {
            lines.Add($"{LiveWeatherText.Describe(weather)} · " +
                UnitText.Format(Quantity.Temperature, weather.TemperatureC));
        }

        if (_flying)
        {
            lines.Add($"Height {UnitText.Format(Quantity.Length, _heightMeters)}");
        }

        return string.Join('\n', lines);
    }

    private void ShowHelp()
    {
        Body? body = Session?.World.Bodies.Find(b => b.Id == _bodyId);
        (double metersPerSecond, string name) = _speeds[_speed];
        _hud?.SetHelp($"Standing on {body?.Name} · {(_flying ? "Flying" : "Walking")} · " +
            $"{Speed(metersPerSecond)} ({name})" +
            $"{(_magnify ? " · Magnified" : "")}\n" +
            "Drag to look · W A S D move (Shift: faster) · Wheel: speed · F: " +
            $"{(_flying ? "walk" : "fly")}{(_flying ? " · Space / C: up / down" : "")} · " +
            "M: magnify · T: calendar · Esc: back");
    }

    private static string Speed(double metersPerSecond) => metersPerSecond < 1000
        ? UnitText.Format(Quantity.Speed, metersPerSecond * 3.6)
        : $"{UnitText.Shown(Quantity.Distance, metersPerSecond / 1000):N0} " +
            $"{UnitText.Symbol(Quantity.Distance)}/s";

    private static string CompassPoint(double degrees) =>
        _compass[(int)Math.Round(degrees / 22.5) % _compass.Length];

    // Far distances in AU, middling ones in millions of km or miles, near ones in km or miles.
    private static string Distance(double km) => UnitText.Distance(km);

    private static double Held(Key key) => Input.IsKeyPressed(key) ? 1 : 0;

    // The minimap's camera (VISION.md REN-08): its own small picture of the scene, lit evenly
    // (its own environment: no haze, and the night side still readable) so it reads as a map.
    private void BuildOverhead()
    {
        _overhead = new SubViewport
        {
            Name = "Minimap",
            Size = new Vector2I(MapPictureSize, MapPictureSize),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Disabled,
        };
        _overheadCamera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.02f, 0.02f, 0.04f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Colors.White,
                AmbientLightEnergy = 0.7f,
            },
        };
        _overhead.AddChild(_overheadCamera);
        AddChild(_overhead);
        _overheadCamera.MakeCurrent();
        _hud!.Minimap.SetPicture(_overhead.GetTexture());
    }

    // Looks straight down on the spot from above, north up, as wide as the chosen span (but
    // never wider than the world: then it's the half of the globe facing the eye).
    private void PlaceOverhead((Vector3D East, Vector3D North, Vector3D Up) frame, Body body,
        double displayRadius)
    {
        if (_overheadCamera is null)
        {
            return;
        }

        double radiusMeters = body.RadiusKm * 1000;
        double spanMeters = Math.Min(_mapSpans[_mapSpan], 2.2 * radiusMeters);
        double unitsPerMeter = displayRadius / radiusMeters;
        double span = spanMeters * unitsPerMeter;

        // Above the eye by the span (well over anything near), and seeing down past the bottom.
        double height = Math.Max(span, (_heightMeters + 20_000) * unitsPerMeter);
        double depth = Math.Min(height + span * 2 + 30_000 * unitsPerMeter,
            height + 3 * displayRadius);
        _overheadCamera.Size = (float)span;
        _overheadCamera.Near = (float)(height * 0.01);
        _overheadCamera.Far = (float)depth;
        Vector3 up = ToGodot(frame.Up), north = ToGodot(frame.North);
        _overheadCamera.GlobalTransform = new Transform3D(Basis.LookingAt(-up, north),
            up * (float)height);
        _hud!.Minimap.Show(double.RadiansToDegrees(_heading),
            $"{UnitText.Format(Quantity.Distance, spanMeters / 1000)} across");
    }

    // Travels to a spot clicked on the minimap (VISION.md REN-08), from -1 to 1 across it (x
    // east, y north). The map is drawn straight down, so a point that far out on it lies an
    // arc of asin(distance / radius) from the spot.
    private void TravelOnMap(Vector2 across)
    {
        if (Session?.World.Bodies.Find(b => b.Id == _bodyId) is not Body body)
        {
            return;
        }

        double radiusMeters = body.RadiusKm * 1000;
        double spanMeters = Math.Min(_mapSpans[_mapSpan], 2.2 * radiusMeters);
        double meters = across.Length() * spanMeters / 2;
        double bearing = Math.Atan2(across.X, across.Y);
        if (_flat is FlatSpot flat)
        {
            _flat = FlatWalk.Walk(flat, bearing, meters / radiusMeters);
            _flatBuiltHeightMeters = 0;  // Build the ground there straight away
            return;
        }

        _spot = GlobeWalk.FromOverhead(_spot, bearing, meters / radiusMeters);
        _groundVersion = -1;  // Build the ground there straight away
    }

    // Opens or closes the calendar over the view (VISION.md REN-08).
    private void ToggleCalendar()
    {
        // Through the toolbar's Calendar button, so it's still right back in the system view.
        if (Calendar is { IsPanelOpen: true })
        {
            Toolbar?.CloseCalendar();
        }
        else
        {
            Toolbar?.ShowCalendar();
        }
    }

    // East, north, and up at the spot, in the system's (and scene's) frame.
    private (Vector3D East, Vector3D North, Vector3D Up) Frame(Body body, double time)
    {
        if (_flat is FlatSpot flat)
        {
            (Vector3D flatEast, Vector3D flatNorth, Vector3D flatUp) = FlatWalk.Frame(flat);
            return (ToSystem(body, time, flatEast), ToSystem(body, time, flatNorth),
                ToSystem(body, time, flatUp));
        }

        (Vector3D east, Vector3D north) = GlobeWalk.Tangents(_spot);
        return (ToSystem(body, time, east), ToSystem(body, time, north),
            ToSystem(body, time, _spot));
    }

    private static Vector3D ToSystem(Body body, double time, Vector3D local) =>
        BodyOrientation.ShapeToSystem(body, time, local);

    // The body's turn (its own frame to the scene's) as a basis.
    private static Basis BodyBasis(Body body, double time) => new(
        ToGodot(ToSystem(body, time, new Vector3D(1, 0, 0))),
        ToGodot(ToSystem(body, time, new Vector3D(0, 1, 0))),
        ToGodot(ToSystem(body, time, new Vector3D(0, 0, 1))));

    private static Vector3D Cross(Vector3D a, Vector3D b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X);

    private static Vector3D Unit(Vector3D v) => v * (1 / v.Length);

    private static Vector3 ToGodot(Vector3D v) => new((float)v.X, (float)v.Y, (float)v.Z);
}
