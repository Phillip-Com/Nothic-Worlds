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
/// the ground right around it is drawn by <see cref="GroundTiles"/>, both so the ground near
/// the eye keeps its precision. A <see cref="SurfaceSky"/> takes over the
/// background, and a <see cref="FirstPersonHud"/> shows the readouts. Must come last in the
/// scene: it takes the mouse and keys before the globe's camera and tools.
/// </remarks>
public partial class FirstPersonMode : Node
{
    private const double EyeHeightMeters = 1.7;
    private const float LookDegreesPerPixel = 0.2f;

    // The least time between rebuilds of the rivers' channels (and the cloud deck, and a flat
    // world's rim) while the eye has only drifted a little.
    private const double MinGroundRebuildSeconds = 0.1;

    // ... and of the rivers' beds and banks, which are worked out on a worker; they're fixed
    // to the ground, so ones built a moment ago still fit.
    private const double MinBanksRebuildSeconds = 0.25;

    // The ground detail's coarsest noise size, and how many of those the noise repeats over:
    // GROUND_DETAIL_METERS and PATTERN_NOISE_SIZE in planet_surface.gdshaderinc.
    private const double GroundDetailMeters = 4;
    private const double GroundNoiseRepeat = 32;

    // How quickly rough terrain's features thin out on wider squares, farther off (see
    // SmallestFeature), in meters.
    private const double FarRoughMeters = 40;
    private const float FieldOfViewDegrees = 70;

    // How much of the ground built around the eye the globe's own mesh leaves unraised by
    // the water (PlanetSurface.SetNearEye): nearly all, so the water drawn up close meets the
    // globe's own water just inside the edge of what's built (past the horizon on a globe).
    private const double NearEyeShare = 0.95;

    // The camera's near distance, as a share of the eye's clearance, and how many times that
    // its far distance is: the most the engine's depth range allows (see SystemView.FitCamera).
    private const double NearShare = 0.1;
    private const double DepthRange = 1e6;

    // The farthest the ground tiles reach to show distant peaks, in meters (VISION.md REN-06).
    private const double MaxPeakReachMeters = 250_000;

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
    private GroundTiles? _tiles;       // The ground and the water's surface around the eye
    private StandingGroundDetail _standingGroundDetail = StandingGroundDetail.Standard;
    private FirstPersonGround? _deck;  // The clouds below, when flying above them
    private RiverWater? _rivers;       // The rivers' water around the eye
    private RiverBankStrip? _banks;    // ... and their beds and banks, drawn finely
    private RiverChannels? _channels;  // ... and the stretches in reach, as last built
    private Vector3D _channelsAround;  // The spot they were built around
    private Vector3D _channelsMiddle;  // ... on the ground there (in the body's own space)
    private bool _nearEyeDue = true;   // The globe's water hasn't stepped aside for them yet
    private int _banksShownVersion = -1;  // The ground shown when the banks were last built
    private bool _banksStale = true;   // The banks don't match the channels yet
    private PendingBanks? _pendingBanks;  // The banks being worked out on a worker
    private Task<(RiverChannels?, RiverWater.Surface?)>? _pendingChannels;  // ... the channels

    // Names the heights the ground tiles are built from (with the rivers' channels): goes up
    // when the ground or the water changes, so tiles built before are rebuilt.
    private long _tileHeights;
    private double? _tilesWaterRadius;

    // Each river's water along its course (VISION.md BOD-11) and the channels they cut, worked
    // out for the courses and ground they were worked out from (they change rarely; the
    // stretches near the eye with every rebuild).
    private (IReadOnlyList<RiverCourseShown> Courses, int Relief, List<RiverProfile> Rivers,
        RiverCarving Carving)? _profiles;

    // Where there's no water over the ground, the water's surface built with it lies this far
    // under it, so it stays out of sight: this share of the width of the tile's squares (the
    // ground farther out is coarser), and at least this, in meters. Near the shore the water
    // slopes down under the ground over a single square.
    private const double DryCellShare = 0.1, MinDryMeters = 0.5;

    // The least a river's strip reaches past its banks to blend into the coarser ground, in
    // meters (it reaches farther where the ground is sunk farther: see BuildBanks).
    private const double MinSkirtMeters = 2;
    private FlatPatch? _flatGround;    // On a flat world's rim or underside, the ground there
    private FlatPatch? _flatDeck;      // ... and on its top face, the clouds below
    private FlatSpot? _flat;           // Where the eye stands on a flat world (else on a globe)

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
    private double _groundHeightMeters;  // The eye's height when the channels were built
    private double _sinceGroundBuilt;     // Seconds since the channels were last built
    private double _sinceBanksBuilt;      // ... and the banks
    private bool _deckStale = true;       // The cloud deck doesn't match the ground yet
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
        // The fog and night vision switches are remembered on this computer (clouds are too,
        // by WeatherDisplay).
        _sky.ShowFog = AppSettings.StandingFog;
        _sky.NightVision = AppSettings.NightVision;
        _hud.FogSwitch.Toggled += on => _sky.ShowFog = AppSettings.StandingFog = on;
        _hud.CloudsSwitch.Toggled += on =>
        {
            if (Weather is not null)
            {
                Weather.ShowClouds = on;  // The same switch as View ▸ Clouds
            }
        };
        _hud.NightVisionSwitch.Toggled += on => _sky.NightVision = AppSettings.NightVision = on;
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

    /// <summary>
    /// How finely the ground is drawn around the eye (File ▸ Settings, Standing ground
    /// detail): changed while standing, the ground is built again.
    /// </summary>
    public StandingGroundDetail StandingGroundDetail
    {
        get => _standingGroundDetail;
        set
        {
            if (value == _standingGroundDetail)
            {
                return;
            }

            _standingGroundDetail = value;
            if (_tiles is not null)
            {
                MakeTiles();
            }
        }
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

        _tiles?.QueueFree();
        _tiles = null;
        _deck?.QueueFree();
        _deck = null;
        _rivers?.QueueFree();
        _rivers = null;
        _banks?.QueueFree();
        _banks = null;
        _channels = null;
        _profiles = null;
        _pendingBanks = null;
        _pendingChannels = null;
        _underwater.Visible = false;
        _flatGround?.QueueFree();
        _flatGround = null;
        _flatDeck?.QueueFree();
        _flatDeck = null;
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

        _deck = new FirstPersonGround
        {
            Name = "CloudDeck",
            MaterialOverride = _sky.DeckMaterial,
            Visible = false,
        };
        AddChild(_deck);
        _rivers = new RiverWater { Visible = flat is null };
        AddChild(_rivers);
        _banks = new RiverBankStrip { Visible = flat is null };
        AddChild(_banks);
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
        }

        MakeTiles();

        if (Environment is not null)
        {
            _sky.Show(Environment);
        }

        // Everything's visibility is noted before any is hidden: hiding the toolbar hides what
        // follows it (the terrain brush), which would otherwise be noted as hidden and stay
        // hidden, ignoring clicks, after leaving.
        foreach (Node node in HideWhileStanding)
        {
            if (node is CanvasItem item)
            {
                _hiddenItems.Add((item, item.Visible));
            }
            else if (node is CanvasLayer layer)
            {
                _hiddenLayers.Add((layer, layer.Visible));
            }
        }

        _hiddenItems.ForEach(hidden => hidden.Item.Visible = false);
        _hiddenLayers.ForEach(hidden => hidden.Layer.Visible = false);

        _hud!.Visible = true;
        _hud.FogSwitch.SetPressedNoSignal(_sky.ShowFog);
        _hud.CloudsSwitch.SetPressedNoSignal(Weather?.ShowClouds ?? true);
        _hud.NightVisionSwitch.SetPressedNoSignal(_sky.NightVision);
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
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.G }:
                _hud!.FogSwitch.ButtonPressed = !_hud.FogSwitch.ButtonPressed;
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.K }:
                _hud!.CloudsSwitch.ButtonPressed = !_hud.CloudsSwitch.ButtonPressed;
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.N }:
                _hud!.NightVisionSwitch.ButtonPressed = !_hud.NightVisionSwitch.ButtonPressed;
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
        _sinceBanksBuilt += delta;
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
        _sky.ShowNightVision(sunAltitude);
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
            if (globe.CarvedGroundHit(above * 1.5f, -above) is Vector3 hit)
            {
                return hit.Length();
            }
        }

        // Never under the drawn ground, which can stand above the ground's height between its
        // points (see GroundTiles.HighestAround).
        double ground = CarvedRadius(globe, body, _spot);
        return _tiles?.HighestAround(_spot) is double drawn ? Math.Max(ground, drawn) : ground;
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
        float near = (float)Math.Max(eyeHeight * NearShare, 1e-9);
        _camera.Near = near;
        _camera.Far = near * (float)DepthRange;
    }

    // Makes the ground tiles for the body stood on, in place of any there were: over a globe,
    // or a flat world's top face.
    private void MakeTiles()
    {
        if (Session?.World.Bodies.Find(b => b.Id == _bodyId) is not Body body)
        {
            return;
        }

        _tiles?.QueueFree();
        double radiusKm = body.RadiusKm;
        ITileSurface surface = _flat is null ? new GlobeTileSurface() : new FlatTopTileSurface();
        _tiles = new GroundTiles(surface, radiusKm * 1000, _standingGroundDetail)
        {
            WaterMaterial = _waterMaterial,
            Visible = false,
            PrepareGround = (tile, anchor) => SetGroundDetail(tile, anchor, radiusKm),
            PrepareWater = (tile, anchor) => SetWaveOrigin(tile, anchor, radiusKm),
        };
        AddChild(_tiles);
        _waterMaterial.SetShaderParameter("radius_meters", (float)(radiusKm * 1000));
        _banksStale = true;
        _nearEyeDue = true;
    }

    // The way the view looks, in the scene's frame.
    private Vector3D Look((Vector3D East, Vector3D North, Vector3D Up) frame)
    {
        Vector3D level = frame.North * Math.Cos(_heading) + frame.East * Math.Sin(_heading);
        return level * Math.Cos(_pitch) + frame.Up * Math.Sin(_pitch);
    }

    // Puts the ground tiles, the cloud deck, and the rivers in the scene, relative to the eye.
    private void PlaceGround(Body body, double time, Vector3D eye, double displayRadius)
    {
        _tiles!.Place(point => At(body, time, eye, displayRadius, point));
        PlaceRings(_deck!, body, time, eye, displayRadius);
        PlaceRivers(body, time, eye, displayRadius);
    }

    // Puts the rivers' water, beds, and banks in the scene, relative to the eye.
    private void PlaceRivers(Body body, double time, Vector3D eye, double displayRadius)
    {
        if (_rivers is { Mesh: not null } rivers)
        {
            rivers.GlobalTransform = At(body, time, eye, displayRadius, rivers.Middle);
            rivers.SetScale(body.RadiusKm * 1000 / displayRadius);
        }

        if (_banks is { Mesh: not null } banks)
        {
            banks.GlobalTransform = At(body, time, eye, displayRadius, banks.Middle);
        }
    }

    // Where a mesh kept relative to `middle` (a point in the body's own space, in radii) goes
    // in the scene, relative to the eye.
    private static Transform3D At(Body body, double time, Vector3D eye, double displayRadius,
        Vector3D middle) => new(
            BodyBasis(body, time).Scaled(Vector3.One * (float)displayRadius),
            ToGodot(ToSystem(body, time, middle - eye) * displayRadius));

    // Starts working out the beds and banks of the rivers around the eye, to be drawn finely
    // over the coarser ground (VISION.md BOD-11), on a worker: `coarseRadius` gives how far
    // out that ground is shown at a direction, in radii (or null off it), and must be safe off
    // the main thread; `pointAt` and `placeAt` as RiverBankStrip.Build. TakeInBanks draws them.
    private void StartBanks(PlanetSurface globe, Body body, Vector3D eye, Vector3D middle,
        Func<Vector3D, double?> coarseRadius, Func<Vector3D, double, Vector3D> pointAt,
        Func<Vector3D, double, Vector3D> placeAt)
    {
        if (_channels is not { } channels || _profiles is not { Carving: var carving })
        {
            ShowBanks(globe, body, null, middle);
            return;
        }

        double radiusKm = body.RadiusKm, radiusMeters = radiusKm * 1000;
        double GroundMeters(Vector3D direction) =>
            globe.GroundLiftAt(direction, radiusKm) * radiusMeters;

        // Each skirt reaches past where the coarser ground is sunk under it, on the widest
        // squares that may be drawn as far from the eye (over the ground, and up) as it is.
        GroundTiles tiles = _tiles!;
        double height = _heightMeters / radiusMeters;
        double SkirtMeters(Vector3D direction)
        {
            double over = Math.Acos(Math.Clamp(direction.Dot(eye), -1, 1));
            double away = Math.Sqrt(over * over + height * height);
            return MinSkirtMeters + 2 * RiverCarving.SinkReachMeters(
                tiles.WidestCellAt(away) * radiusMeters);
        }

        _pendingBanks = new PendingBanks(Task.Run(() => RiverBankStrip.Prepare(
            RiverBanks.Strips(channels, carving, eye, radiusKm, GroundMeters,
                direction => coarseRadius(direction) is double coarse
                    ? (coarse - 1) * radiusMeters
                    : carving.Carve(direction, GroundMeters(direction)),
                SkirtMeters),
            middle, pointAt, placeAt)), middle);
    }

    // Draws the beds and banks of the rivers once they've been worked out (StartBanks).
    private void TakeInBanks(PlanetSurface globe, Body body)
    {
        if (_pendingBanks is not { Strips.IsCompleted: true } pending)
        {
            return;
        }

        _pendingBanks = null;
        if (pending.Strips.Exception is { } error)
        {
            GD.PushError($"Couldn't work out the rivers' banks: {error.GetBaseException()}");
            return;
        }

        ShowBanks(globe, body, pending.Strips.Result, pending.Middle);
    }

    // Draws the beds and banks of the rivers worked out (none: nothing), around `middle`.
    private void ShowBanks(PlanetSurface globe, Body body, RiverBankStrip.Surface? strips,
        Vector3D middle)
    {
        _banks!.MaterialOverride = globe.MaterialOverride;
        _banks.Show(strips, middle);
        SetGroundDetail(_banks, middle, body.RadiusKm);
    }

    // Starts finding the stretches of the rivers around `eye` (a direction on the globe, or
    // the one a flat world's spot stands for; null for none), out to `outer` radians, and
    // working out their water's surface around `middle`, each point where `pointAt` puts it
    // (VISION.md BOD-11), on a worker; TakeInChannels draws them. The rivers' water and
    // channels along their whole courses are worked out first, here, if the courses or the
    // ground have changed, so the ground tiles are always built with the right channels.
    private void StartChannels(PlanetSurface globe, Body body, Vector3D? eye, double outer,
        Vector3D middle, Func<Vector3D, double, Vector3D> pointAt)
    {
        List<RiverProfile>? rivers = RiversOn(globe, body);
        double radiusKm = body.RadiusKm, radiusMeters = radiusKm * 1000;
        _pendingChannels = Task.Run(() =>
        {
            RiverChannels? channels = rivers is not null && eye is Vector3D around
                ? RiverChannels.Near(rivers, around, radiusKm, outer * radiusMeters)
                : null;
            return (channels, RiverWater.Prepare(channels, middle, pointAt, radiusMeters));
        });
    }

    // Draws the rivers' water once the stretches around the eye have been found
    // (StartChannels); their beds and banks follow.
    private void TakeInChannels()
    {
        if (_pendingChannels is not { IsCompleted: true } pending)
        {
            return;
        }

        _pendingChannels = null;
        if (pending.Exception is { } error)
        {
            GD.PushError($"Couldn't find the rivers around you: {error.GetBaseException()}");
            return;
        }

        (_channels, RiverWater.Surface? water) = pending.Result;
        _rivers!.Show(water, _channelsMiddle);
        _banksStale = true;
    }

    // Each river's water along its course on a body, and the channels they cut (VISION.md
    // BOD-11), worked out again if the courses or the ground have changed; null with none.
    private List<RiverProfile>? RiversOn(PlanetSurface globe, Body body)
    {
        IReadOnlyList<RiverCourseShown> courses = Session?.WaterOn(body.Id)?.Rivers ?? [];
        if (courses.Count == 0)
        {
            _profiles = null;
            FadeRoughness(globe, null, body.RadiusKm);
            return null;
        }

        double radiusMeters = body.RadiusKm * 1000;
        if (_profiles is not var (known, relief, _, _) || !ReferenceEquals(known, courses)
            || relief != globe.ReliefVersion)
        {
            var profiles = new RiverProfile?[courses.Count];
            Parallel.For(0, courses.Count, i => profiles[i] = RiverProfile.For(courses[i],
                body.RadiusKm,
                direction => globe.SmoothGroundLiftAt(direction, body.RadiusKm) * radiusMeters));
            List<RiverProfile> rivers = [.. profiles.OfType<RiverProfile>()];
            var carving = RiverCarving.For(rivers, body.RadiusKm);
            _profiles = (courses, globe.ReliefVersion, rivers, carving);
            FadeRoughness(globe, carving, body.RadiusKm);
        }

        return _profiles.Value.Rivers;
    }

    // Fades rough terrain's fine relief out toward the rivers carved (none: kept everywhere;
    // VISION.md BOD-12), so they run in smooth valleys. Rough ground near them changes with
    // them, so then every tile's built again.
    private void FadeRoughness(PlanetSurface globe, RiverCarving? carving, double radiusKm)
    {
        Func<Vector3D, double>? riverMeters = null;
        if (carving is { IsEmpty: false })
        {
            double reach = TerrainRoughness.RiverReachMeters(radiusKm);
            riverMeters = direction => carving.BeyondBanksMeters(direction, reach);
        }

        if (globe.SetRiverDistance(riverMeters) && globe.IsRough)
        {
            _tileHeights++;
        }
    }

    // How high the coarser ground around the eye is built on a tile (see Recipes), in meters
    // at a base point: the ground's height (in radii from the middle, at a direction) with the
    // rivers' channels cut in, and sunk out of sight under the fine strip that draws their
    // beds and banks; null where no river comes near the tile. `toDirection` gives the
    // direction a base point stands for. Safe off the main thread (tiles are built on workers).
    private static Func<Vector3D, double>? CarvedTile(RiverCarving carving, GroundTiles tiles,
        PlanetSurface globe, GroundTile tile, double radiusKm,
        Func<Vector3D, Vector3D> toDirection, double smallestMeters)
    {
        // The ball around the tile's directions (a little more, as the edges between its
        // corners and middles bulge), and the width of its squares there, in meters.
        var bases = new List<Vector3D>(9);
        foreach (double across in new[] { 0, 0.5, 1 })
        {
            foreach (double down in new[] { 0, 0.5, 1 })
            {
                bases.Add(tiles.BasePoint(tile, across, down));
            }
        }

        TileExtent flat = TileExtent.Around(bases, 0, 0);
        TileExtent round = TileExtent.Around([.. bases.Select(toDirection)], 0, 0);
        double radiusMeters = radiusKm * 1000;
        double cellMeters = tiles.CellWidth(tile) * radiusMeters
            * round.Radius / Math.Max(flat.Radius, 1e-15);
        return carving.UnderStripNear(round.Center, round.Radius * 1.1, cellMeters)
            is { } under
            ? point =>
            {
                Vector3D direction = toDirection(point);
                return under(direction,
                    globe.GroundLiftAt(direction, radiusKm, smallestMeters) * radiusMeters);
            }
        : null;
    }

    // The smallest features of rough terrain (VISION.md BOD-12) worth building into a tile,
    // and into its parent (twice as coarse), in meters (see SmallestFeature). `toDirection`
    // gives the direction a base point stands for; `farThinning`, whether they thin out far off.
    private static (double Own, double Parent) SmallestFeatures(GroundTiles tiles,
        GroundTile tile, double radiusKm, Func<Vector3D, Vector3D> toDirection,
        bool farThinning)
    {
        Vector3D corner = tiles.BasePoint(tile, 0, 0), across = tiles.BasePoint(tile, 1, 0);
        double flat = (across - corner).Length;
        double round = (toDirection(across) - toDirection(corner)).Length;
        double cellMeters = tiles.CellWidth(tile) * radiusKm * 1000 * round
            / Math.Max(flat, 1e-15);
        return (SmallestFeature(cellMeters, farThinning),
            SmallestFeature(2 * cellMeters, farThinning));
    }

    // The smallest rough features built into ground with squares this wide: at least two
    // squares across, as finer ones would only shimmer (that's always more than a few pixels
    // on screen). With `farThinning`, far fewer on the widest squares, far off, so tiles whose
    // outer edge is in sight (squares over a kilometer wide) meet the globe's own ground beyond
    // it, which has none, without a ledge: about 30 m features 200 m away, 4 km ones 5 km away,
    // and none past 15 km or so. Tiles reaching the farthest peak end out of sight past the
    // horizon, so they keep every feature to the edge.
    private static double SmallestFeature(double cellMeters, bool farThinning) =>
        Math.Max(TerrainRoughness.FinestMeters, Math.Max(2 * cellMeters,
            farThinning ? cellMeters * cellMeters / FarRoughMeters : 0));

    // How far out the drawn ground is at a direction, in radii, with the rivers' channels
    // cut into it.
    private double CarvedRadius(PlanetSurface globe, Body body, Vector3D direction)
    {
        double ground = globe.GroundRadiusAt(direction, body.RadiusKm);
        if (_profiles is not { Carving: var carving })
        {
            return ground;
        }

        double radiusMeters = body.RadiusKm * 1000;
        return 1 + carving.Carve(direction, (ground - 1) * radiusMeters) / radiusMeters;
    }

    // The height the water's surface is built at, in radii above the base: the water's,
    // where it stands above the `ground` (before rivers' channels are cut: those hold the
    // river's water, not the sea's; given the smallest rough features the tile shows, and
    // worked out only where there's water), else out of sight just under the drawn ground
    // (`under`: carved, and sunk under rivers' banks) on a tile with squares `cell` radii
    // across. The drawn ground is never above the ground, so where the water is below it,
    // the ground isn't needed.
    private static double WaterOrUnder(double? water, Func<double, double> ground, double under,
        double cell, double radiusMeters, bool farThinning)
    {
        if (water is double level && level > under
            && level > ground(SmallestFeature(cell * radiusMeters, farThinning)))
        {
            return level;
        }

        return under - Math.Max(MinDryMeters, DryCellShare * cell * radiusMeters)
            / radiusMeters;
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

    // Keeps the ground around the eye built (GroundTiles), with the rivers' channels cut in
    // (they don't depend on where the eye is, so the tiles are kept as they are), and the
    // cloud deck over it. Done before the eye is placed on it, so the eye is always measured
    // against the ground that's drawn. The rivers' water, beds, and banks are drawn in steps
    // that shorten toward the eye, so they're found again on a worker when the eye has moved a
    // good part of its height away, risen or sunk by half, or the ground changed (see
    // ChannelsDue).
    private void KeepGroundBuilt(PlanetSurface globe, Body body)
    {
        GroundTiles tiles = _tiles!;
        bool shown = !globe.IsCarved;  // A carved globe draws its own carving
        tiles.Visible = shown;
        _rivers!.Visible = shown;
        _banks!.Visible = shown;
        if (!shown)
        {
            return;
        }

        double radiusMeters = body.RadiusKm * 1000;
        double ground = globe.GroundRadiusAt(_spot, body.RadiusKm);

        // Flying, the tiles are chosen for the height flown at, not the height kept above the
        // drawn ground: that is measured on the tiles chosen, so it would choose them in turn,
        // and rising, the view would flick between coarse tiles and fine ones every frame.
        double heightMeters = _flying
            ? Math.Max(EyeHeightMeters, _altitudeMeters - (ground - 1) * radiusMeters)
            : _heightMeters;
        double height = heightMeters / radiusMeters;

        // The ground reaches well past the horizon, which moves out as the eye rises.
        double outer = Math.Clamp(Math.Acos(1 / (1 + height)) * 4, 0.003, 0.6);
        double reach = TileReach(globe, outer, ground - 1 + height, heightMeters, radiusMeters);
        Vector3D middle = _spot * ground;
        Vector3D Lifted(Vector3D direction, double meters) =>
            direction * (1 + meters / radiusMeters);
        TakeInChannels();
        double moved = Math.Acos(Math.Clamp(_channelsAround.Dot(_spot), -1, 1));
        if (ChannelsDue(globe, moved > Math.Max(height * 0.25, 1e-7), moved > outer * 0.3))
        {
            _channelsAround = _spot;
            _channelsMiddle = middle;
            StartChannels(globe, body, _spot, outer, middle, Lifted);
        }

        tiles.GroundMaterial = globe.MaterialOverride;
        SetGroundRadius(globe.MaterialOverride, radiusMeters);

        // At Low detail the tiles' edge can be in sight, so rough features thin out toward it.
        bool farThinning = _standingGroundDetail == StandingGroundDetail.Low;
        Func<GroundTile, TileExtent, GroundTileRecipe> recipes = Recipes(globe,
            (_, smallest) => direction =>
                globe.GroundRadiusAt(direction, body.RadiusKm, smallest),
            (carving, tile, smallest) => CarvedTile(carving, tiles, globe, tile, body.RadiusKm,
                direction => direction, smallest) is { } carved
                ? direction => 1 + carved(direction) / radiusMeters
                : null,
            tile => SmallestFeatures(tiles, tile, body.RadiusKm, direction => direction,
                farThinning),
            (direction, under, cell) => WaterOrUnder(globe.WaterRadiusAt(direction) - 1,
                smallest => globe.GroundLiftAt(direction, body.RadiusKm, smallest), under - 1,
                cell, radiusMeters, farThinning) + 1);
        // The ground out past `outer` to the farthest peak is built after the ground within it
        // (owner's choice, 2026-10-09).
        tiles.Update(_spot * (ground + height), _spot, reach, outer, RecipesVersion(globe),
            recipes);
        if (_nearEyeDue && tiles.HasGround)
        {
            // The globe's own water steps aside for the water drawn on the tiles.
            _nearEyeDue = false;
            globe.SetNearEye(_channelsAround, reach * NearEyeShare);
        }

        TakeInBanks(globe, body);
        if (BanksDue(tiles))
        {
            StartBanks(globe, body, _spot, middle, tiles.ShownHeights(), Lifted,
                (direction, _) => direction);
        }

        // The cloud deck: rings at the cloud layer, over the ground here. It's only seen from
        // above the clouds, so it's built only then.
        if (_deckStale && !BelowClouds())
        {
            _deckStale = false;
            double layer = ground + SurfaceSky.CloudHeightKm / body.RadiusKm;
            _deck!.Build(_ => layer, _spot, 2e-7, outer);
        }
    }

    // How far the ground tiles reach over a globe, in radians: at Low detail, `outer` (a
    // little past the horizon); otherwise out to the farthest peak that can show over the
    // horizon from an eye `eyeLift` radii above the base (`heightMeters` above the ground):
    // the eye's horizon and the highest point's, together. Never past the camera's far
    // distance (where nothing's drawn) or MaxPeakReachMeters; flat ground stays at `outer`.
    private double TileReach(PlanetSurface globe, double outer, double eyeLift,
        double heightMeters, double radiusMeters)
    {
        if (_standingGroundDetail == StandingGroundDetail.Low)
        {
            return outer;
        }

        double peak = Math.Acos(1 / (1 + Math.Max(eyeLift, 0)))
            + Math.Acos(1 / (1 + Math.Max(globe.HighestRelief, 0)));
        double farthest = Math.Min(MaxPeakReachMeters,
            heightMeters * NearShare * DepthRange) / radiusMeters;
        return Math.Max(outer, Math.Min(peak, farthest));
    }

    // Whether the rivers' channels (and with them the cloud deck, and a flat world's rim) are
    // due to be built again: when the ground has changed, the eye has risen or sunk by half,
    // or it has `drifted` a good part of its height away from where they were built. Moving
    // fast, that would be every frame, so then it's at most every MinGroundRebuildSeconds,
    // unless it has gone `far`. Notes the ground and height they're built for.
    private bool ChannelsDue(PlanetSurface globe, bool drifted, bool far)
    {
        double risen = _heightMeters / _groundHeightMeters;
        if (globe.ReliefVersion == _groundVersion && risen <= 1.5 && risen >= 1 / 1.5
            && !(drifted && _pendingChannels is null
                && (_sinceGroundBuilt >= MinGroundRebuildSeconds || far)))
        {
            return false;
        }

        if (globe.ReliefVersion != _groundVersion)
        {
            _tileHeights++;  // The ground (or the water on it) changed: every tile's rebuilt
        }

        _groundVersion = globe.ReliefVersion;
        _groundHeightMeters = _heightMeters;
        _sinceGroundBuilt = 0;
        _deckStale = true;
        _nearEyeDue = true;
        return true;
    }

    // Whether the rivers' beds and banks are due to be built again: after the channels, or
    // once the ground shown under them has changed (their edges meet it), at most every
    // MinBanksRebuildSeconds, and not while they or the channels are being worked out.
    private bool BanksDue(GroundTiles tiles)
    {
        if (_sinceBanksBuilt < MinBanksRebuildSeconds || _pendingBanks is not null
            || _pendingChannels is not null)
        {
            return false;
        }

        bool rivers = _channels is { Stretches.Count: > 0 };
        if (!_banksStale && (!rivers || tiles.ShownVersion == _banksShownVersion))
        {
            return false;
        }

        _banksStale = false;
        _banksShownVersion = tiles.ShownVersion;
        _sinceBanksBuilt = 0;
        return true;
    }

    // Names the ground tiles' recipes (see Recipes): it changes when they do. Notes the sea's
    // level they're built for: when that changes, every tile's built again.
    private long RecipesVersion(PlanetSurface globe)
    {
        if (globe.WaterRadius != _tilesWaterRadius)
        {
            _tilesWaterRadius = globe.WaterRadius;
            _tileHeights++;
        }

        return _tileHeights << 32;
    }

    // How the ground tiles are built (GroundTileRecipe): at the `plain` ground's height; near
    // the rivers, at the height `carved` gives a tile with their channels cut in (null for a
    // tile no river comes near); with the water's surface `water` gives over them, on a body
    // with water. The channels don't depend on where the eye is, so these tiles are kept like
    // any other, and built again only when the ground or the rivers change.
    private Func<GroundTile, TileExtent, GroundTileRecipe> Recipes(PlanetSurface globe,
        Func<GroundTile, double, Func<Vector3D, double>> plain,
        Func<RiverCarving, GroundTile, double, Func<Vector3D, double>?> carved,
        Func<GroundTile, (double Own, double Parent)> smallest,
        Func<Vector3D, double, double, double> water)
    {
        Func<Vector3D, double, double, double>? waterOver =
            globe.WaterRadius is not null || globe.HasLakes ? water : null;
        long stamp = _tileHeights << 32;
        RiverCarving? carving = _profiles is { Carving: { IsEmpty: false } some } ? some : null;
        bool rough = globe.IsRough;

        // Rough terrain shows finer detail on finer tiles (BOD-12), so each tile morphs toward
        // its parent's ground as the parent builds it. Which rivers come near a tile is found
        // when it's built, on a worker.
        return (tile, _) =>
        {
            (double own, double parent) = rough ? smallest(tile) : (0, 0);
            Func<Vector3D, double> HeightWith(double finest)
            {
                if (carving is null)
                {
                    return plain(tile, finest);
                }

                Func<Vector3D, double>? height = null;
                return point =>
                    (height ??= carved(carving, tile, finest) ?? plain(tile, finest))(point);
            }

            return new GroundTileRecipe(stamp, HeightWith(own), waterOver,
                rough && parent != own ? HeightWith(parent) : null);
        };
    }

    // Tells the ground's material the globe's radius in meters, for the fine ground detail
    // (planet_surface.gdshaderinc).
    private static void SetGroundRadius(Material? material, double radiusMeters)
    {
        if (material is ShaderMaterial shader)
        {
            shader.SetShaderParameter("ground_radius_meters", (float)radiusMeters);
        }
    }

    // Tells a mesh of the ground where the point it's built around falls in the fine ground
    // detail's noise (planet_surface.gdshaderinc): worked out here in double precision and
    // wrapped to the noise's repeat, so the detail runs on unbroken from one tile to the next.
    private static void SetGroundDetail(GeometryInstance3D ground, Vector3D anchor,
        double radiusKm)
    {
        double radiusMeters = radiusKm * 1000;
        float Wrapped(double radii)
        {
            double units = radii * radiusMeters / GroundDetailMeters;
            return (float)(units - Math.Floor(units / GroundNoiseRepeat) * GroundNoiseRepeat);
        }

        ground.SetInstanceShaderParameter("ground_detail_origin",
            new Vector3(Wrapped(anchor.X), Wrapped(anchor.Y), Wrapped(anchor.Z)));
    }

    // Tells a tile of the water's surface where the point it's built around falls in the
    // waves' repeat, worked out in double precision, so the waves run on unbroken.
    private static void SetWaveOrigin(GeometryInstance3D water, Vector3D anchor,
        double radiusKm)
    {
        double radiusMeters = radiusKm * 1000;
        float Wrapped(double radii)
        {
            double meters = radii * radiusMeters;
            return (float)(meters - Math.Floor(meters / WaveRepeatMeters) * WaveRepeatMeters);
        }

        water.SetInstanceShaderParameter("wave_origin",
            new Vector3(Wrapped(anchor.X), Wrapped(anchor.Y), Wrapped(anchor.Z)));
    }

    // On a flat world: on the top face, the ground tiles (as on a globe: see
    // KeepGroundBuilt), the rivers, and the cloud deck over them; on the rim and underside, a
    // patch of bare rock around the eye. The rock, deck, and channels are built again as
    // ChannelsDue says, or when the eye has gone onto another face. All placed relative to
    // the eye.
    private void PlaceFlatGround(PlanetSurface globe, Body body, double time, Vector3D eye,
        double displayRadius, FlatSpot flat)
    {
        GroundTiles tiles = _tiles!;
        FlatPatch rock = _flatGround!;
        bool top = flat.Face == FlatFace.Top;
        tiles.Visible = top && !globe.IsCarved;  // A carved disc draws its own carving
        rock.Visible = !top && !globe.IsCarved;
        _rivers!.Visible = tiles.Visible;
        _banks!.Visible = tiles.Visible;
        double radiusMeters = body.RadiusKm * 1000;
        double height = _heightMeters / radiusMeters;
        Vector3D underfoot = FlatPoint(globe, flat, 0, body);

        // No horizon on a flat face: the ground reaches well past what the eye sees sharply,
        // and the disc's own mesh beyond it.
        double outer = Math.Clamp(height * 400, 0.003, 2 * FlatDisc.Radius);
        Vector3D OnTop(Vector3D direction, double meters) =>
            FlatDisc.TopPointFor(direction) + new Vector3D(0, FlatGroundLift
                + Math.Max(meters / radiusMeters, PlanetSurface.FlatDeepestLift), 0);
        TakeInChannels();
        double moved = (underfoot - _channelsMiddle).Length;
        bool otherFace = flat.Face != _flatBuiltFace;
        if (ChannelsDue(globe, moved > Math.Max(height * 0.25, 1e-7), otherFace) || otherFace)
        {
            _flatBuiltFace = flat.Face;
            _channelsMiddle = underfoot;
            StartChannels(globe, body,
                top && !globe.IsCarved ? FlatWalk.MapDirection(flat) : null, outer, underfoot,
                OnTop);
            if (top)
            {
                // The deck carries each point's map direction, as the globe's deck does.
                _flatDeck!.Build(flat, outer, SurfaceSky.CloudHeightKm / body.RadiusKm,
                    point => FlatDisc.DirectionFor(point));
            }
            else
            {
                rock.Build(flat, outer, FlatGroundLift, rimLift: globe.RimLift);
                rock.MaterialOverride = PlanetSurface.RockMaterial;
                globe.SetNearEye(null, 0);
            }
        }

        if (tiles.Visible)
        {
            tiles.GroundMaterial = globe.MaterialOverride;
            SetGroundRadius(globe.MaterialOverride, radiusMeters);
            double radiusKm = body.RadiusKm;
            Func<GroundTile, TileExtent, GroundTileRecipe> recipes = Recipes(globe,
                (_, smallest) => point =>
                    FlatGroundLift + FlatGroundAt(globe, point, radiusKm, smallest),
                (carving, tile, smallest) => CarvedTile(carving, tiles, globe, tile, radiusKm,
                    FlatDisc.DirectionFor, smallest) is { } carved
                    ? point => FlatGroundLift + Math.Max(carved(point) / radiusMeters,
                        PlanetSurface.FlatDeepestLift)
                    : null,
                tile => SmallestFeatures(tiles, tile, radiusKm, FlatDisc.DirectionFor, true),
                (point, under, cell) => FlatGroundLift + Math.Max(WaterOrUnder(
                        globe.WaterRadiusAt(FlatDisc.DirectionFor(point)) - 1,
                        smallest => FlatGroundAt(globe, point, radiusKm, smallest),
                        under - FlatGroundLift, cell, radiusMeters, true),
                    PlanetSurface.FlatDeepestLift));
            tiles.Update(eye, FlatWalk.Point(flat), outer, outer, RecipesVersion(globe), recipes);
            if (_nearEyeDue && tiles.HasGround)
            {
                _nearEyeDue = false;
                globe.SetNearEye(_channelsMiddle, outer * NearEyeShare);
            }

            TakeInBanks(globe, body);
            if (BanksDue(tiles))
            {
                Func<Vector3D, double?> shown = tiles.ShownHeights();
                StartBanks(globe, body, FlatWalk.MapDirection(flat) ?? Vector3D.Zero,
                    underfoot,
                    direction => shown(FlatDisc.TopPointFor(direction)) is double drawn
                        ? 1 + drawn - FlatGroundLift
                        : null,
                    OnTop, OnTop);
            }
        }

        tiles.Place(point => At(body, time, eye, displayRadius, point));
        PlacePatch(rock, body, time, eye, displayRadius);
        PlacePatch(_flatDeck!, body, time, eye, displayRadius);
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
    private static double FlatGroundAt(PlanetSurface globe, Vector3D topPoint, double radiusKm,
        double smallestMeters = TerrainRoughness.FinestMeters)
    {
        if (globe.IsCarved)
        {
            var above = new Vector3((float)topPoint.X, 1, (float)topPoint.Z);
            return globe.CarvedGroundHit(above, Vector3.Down) is Vector3 hit
                ? hit.Y - FlatDisc.HalfThickness
                : 0;
        }

        return globe.GroundLiftAt(FlatDisc.DirectionFor(topPoint), radiusKm, smallestMeters);
    }

    // How far the drawn ground is lifted at a point of a flat world's top face, in radii,
    // with the rivers' channels cut into it.
    private double FlatCarvedAt(PlanetSurface globe, Vector3D topPoint, Body body)
    {
        double lift = FlatGroundAt(globe, topPoint, body.RadiusKm);
        if (_profiles is not { Carving: var carving } || globe.IsCarved)
        {
            return lift;
        }

        double radiusMeters = body.RadiusKm * 1000;
        double carved = carving.Carve(FlatDisc.DirectionFor(topPoint), lift * radiusMeters);
        return Math.Max(carved / radiusMeters, PlanetSurface.FlatDeepestLift);
    }

    // Puts a flat world's patch in the scene, relative to the eye.
    private static void PlacePatch(FlatPatch patch, Body body, double time, Vector3D eye,
        double displayRadius)
    {
        if (patch.Mesh is not null)
        {
            patch.GlobalTransform = At(body, time, eye, displayRadius, patch.Middle);
        }
    }

    // Puts rings built around a spot in the scene, relative to the eye.
    private static void PlaceRings(FirstPersonGround rings, Body body, double time, Vector3D eye,
        double displayRadius)
    {
        if (rings.Mesh is not null)
        {
            rings.GlobalTransform = At(body, time, eye, displayRadius,
                rings.Center * rings.CenterRadius);
        }
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
            "M: magnify · T: calendar · G / K / N: fog, clouds, night vision · Esc: back");
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
            _sinceGroundBuilt = MinGroundRebuildSeconds;  // Its channels straight away
            return;
        }

        _spot = GlobeWalk.FromOverhead(_spot, bearing, meters / radiusMeters);
        _sinceGroundBuilt = MinGroundRebuildSeconds;  // Its channels straight away
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

    // The rivers' beds and banks being worked out (StartBanks), and where they're drawn around.
    private sealed record PendingBanks(Task<RiverBankStrip.Surface?> Strips, Vector3D Middle);
}
