using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Interop;
using NothicWorlds.Session;

namespace NothicWorlds.Rendering;

/// <summary>
/// Draws the whole star system (VISION.md REN-01, REN-02, BOD-01): every body at its place for
/// the world clock, spinning and tilted, stars glowing and lighting the others, and faint orbit
/// lines. It reads the world from the session and never changes it.
/// </summary>
/// <remarks>
/// <para>Positions come from Core's <see cref="SystemLayout"/> (readable or true scale) in full
/// precision. The scene is drawn around a <b>floating origin</b>: the focused body sits at the
/// scene's center and everything else is placed relative to it, so even true-scale systems
/// millions of units across stay precise where the camera is.</para>
/// <para>Focusing another body flies there: the origin glides from one body to the other and the
/// camera's scale follows the new body's size.</para>
/// </remarks>
public partial class SystemView : Node3D
{
    private const double FlightSeconds = 1.2;

    // How close the camera can get to the ground, in km (owner's choice: about 10 km on an
    // Earth-sized planet), and never closer than this fraction of a radius (for huge stars).
    private const double ClosestApproachKm = 10.0;
    private const double MinRelativeAltitude = 1e-5;

    // The most the far clipping distance may exceed the near one by.
    private const double MaxDepthRange = 1e6;

    // Close to a body, its own orbit line would run straight through it; it appears once the
    // camera is this many of the body's radii away.
    private const float OwnOrbitLineAltitude = 30.0f;

    // Flying from closer than this many radii keeps the framing (the new body fills the screen
    // as the old one did); from farther out, it keeps the camera's real distance, so flying to
    // a big star from the system view doesn't zoom far out.
    private const float CloseUpAltitude = 10.0f;
    private const int OrbitLineSamples = 256;

    private static readonly Color _orbitColor = new(0.6f, 0.7f, 0.9f, 0.35f);

    private readonly Dictionary<Guid, BodyVisual> _visuals = [];

    // Every star's asteroid belts (VISION.md BOD-03), by belt.
    private readonly Dictionary<Guid, BeltVisual> _belts = [];
    private readonly SphereMesh _sphere = new()
    {
        Radius = 1.0f,
        Height = 2.0f,
        RadialSegments = 128,
        Rings = 64,
    };
    private readonly StandardMaterial3D _orbitMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        AlbedoColor = _orbitColor,
    };

    // The path of the body being edited: the selection color, fully visible.
    private readonly StandardMaterial3D _highlightMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = new Color(1.0f, 0.85f, 0.2f),
    };

    private OrbitGuideVisual? _guide;
    private SystemScale _scale = SystemScale.Readable;
    private bool _showTerrain = true;
    private VisualStyle _style = VisualStyle.Painterly;
    private Dictionary<Guid, DisplayBody> _layout = [];
    private Guid _focusId;
    private double _flightProgress = 1.0;
    private Vector3D _flightFrom;
    private double _flightFromRadius = 1.0;
    private double _flightFromAltitude;
    private double _flightToAltitude;

    /// <summary>The open world to draw.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The camera, scaled to the focused body and given the system's reach.</summary>
    [Export] public PlanetCamera? Camera { get; set; }

    /// <summary>Lights the planets when the system has no stars (e.g. older worlds).</summary>
    [Export] public DirectionalLight3D? FallbackLight { get; set; }

    /// <summary>The planet surface material; each planet and moon gets its own copy.</summary>
    [Export] public ShaderMaterial? PlanetMaterial { get; set; }

    /// <summary>
    /// The body whose path is highlighted and always shown, even close up (while the System
    /// panel edits it), or null for none.
    /// </summary>
    public Guid? HighlightedOrbit { get; set; }

    /// <summary>Raised after every body has been placed for this frame.</summary>
    public event Action? Placed;

    /// <summary>
    /// How sizes and distances are drawn (owner's choice: readable by default, true scale as a
    /// toggle). The simulation is the same either way.
    /// </summary>
    public SystemScale DisplayScale
    {
        get => _scale;
        set
        {
            if (_scale != value)
            {
                _scale = value;
                RebuildOrbitLines();
            }
        }
    }

    /// <summary>
    /// Where the scene's center is, in display units from the system's center: the focused
    /// body (or a point on the way to it during a flight).
    /// </summary>
    public Vector3D Origin { get; private set; }

    /// <summary>Each body's display position and size, as last placed.</summary>
    public IReadOnlyDictionary<Guid, DisplayBody> Layout => _layout;

    public override void _Ready()
    {
        if (Session is null || Camera is null || PlanetMaterial is null)
        {
            GD.PushError("SystemView needs a world session, a camera, and a planet material.");
            SetProcess(false);
            return;
        }

        _guide = new OrbitGuideVisual { Name = "Orbit guide", Session = Session };
        AddChild(_guide);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActions.ToggleGrid))
        {
            ShowGrid = !ShowGrid;
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>
    /// Whether the latitude/longitude grid shows on the globes (the G key and View ▸ Grid): one
    /// switch for every planet and moon. Comets never show it: they have no map to line up.
    /// </summary>
    public bool ShowGrid
    {
        get => _visuals.Values.FirstOrDefault(visual => visual.Surface is not null
            && visual.Tail is null)?.Surface!.ShowGrid ?? false;
        set
        {
            foreach (BodyVisual visual in _visuals.Values)
            {
                if (visual.Surface is PlanetSurface surface && visual.Tail is null)
                {
                    surface.ShowGrid = value;
                }
            }
        }
    }

    /// <summary>
    /// Whether the stable orbit guide (VISION.md SIM-04) shows around the highlighted body
    /// (View ▸ Orbit Guide; on by default).
    /// </summary>
    public bool ShowOrbitGuide { get; set; } = true;

    /// <summary>
    /// How many times taller than true scale sculpted relief is drawn (VISION.md BOD-04; owner's
    /// choice: heights are true to scale, and the view exaggerates them so they show from
    /// orbit). View ▸ Relief; not saved.
    /// </summary>
    public float ReliefExaggeration { get; set; } = 10;

    /// <summary>
    /// How finely sculpted globes' shapes are drawn (View ▸ Relief Detail; owner's choice: a
    /// quality setting, remembered on this computer by <see cref="AppSettings"/>).
    /// </summary>
    public ReliefDetail ReliefDetail { get; set; } = AppSettings.ReliefDetail;

    /// <summary>
    /// Whether sculpted relief is shaded map-style, lit from the northwest so it always shows,
    /// or by the sunlight (View ▸ Relief Shading; owner's choice; remembered on this computer).
    /// </summary>
    public bool MapStyleShading { get; set; } = AppSettings.MapStyleShading;

    /// <summary>
    /// How the open world is drawn (VISION.md REN-05; owner's choice: stored with the world,
    /// which sets it here).
    /// </summary>
    public VisualStyle Style
    {
        get => _style;
        set
        {
            _style = value;
            ShapedGlobe.UseStyle(value);
        }
    }

    /// <summary>
    /// True while physics mode moves the bodies (VISION.md SIM-03): markers on the designed
    /// orbits are hidden then.
    /// </summary>
    public bool FollowsPhysics => Session?.Physics.IsOn ?? false;

    /// <summary>
    /// Whether painted terrain shows on the globes (View ▸ Terrain), for every globe at once,
    /// including ones added later.
    /// </summary>
    public bool ShowTerrain
    {
        get => _showTerrain;
        set
        {
            _showTerrain = value;
            foreach (BodyVisual visual in _visuals.Values)
            {
                if (visual.Surface is PlanetSurface surface)
                {
                    surface.ShowTerrain = value;
                }
            }
        }
    }

    /// <summary>
    /// Builds the visuals for a world's bodies, replacing any from before, and focuses
    /// <paramref name="focusId"/> straight away (no flight).
    /// </summary>
    public void Show(World world, Guid focusId)
    {
        foreach (BodyVisual visual in _visuals.Values)
        {
            visual.Free();
        }

        _visuals.Clear();
        foreach (BeltVisual belt in _belts.Values)
        {
            belt.Free();
        }

        _belts.Clear();
        Sync(world);
        FocusImmediately(focusId);
    }

    /// <summary>
    /// Brings the visuals up to date after bodies were added, removed, or changed, keeping the
    /// rest (and their loaded maps) as they are.
    /// </summary>
    /// <returns>The bodies whose visuals were created anew, so their surfaces are blank.</returns>
    public HashSet<Guid> Sync(World world)
    {
        var present = new HashSet<Guid>(world.Bodies.Select(body => body.Id));
        foreach (Guid gone in _visuals.Keys.Where(id => !present.Contains(id)).ToList())
        {
            _visuals[gone].Free();
            _visuals.Remove(gone);
        }

        // Stars, world trees, and globes are drawn differently, so a body that changed between
        // them is rebuilt (a planet becoming a moon keeps its visual, and its loaded maps).
        var created = new HashSet<Guid>();
        foreach (Body body in world.Bodies)
        {
            if (!_visuals.TryGetValue(body.Id, out BodyVisual? visual)
                || DrawnAs(visual.Kind) != DrawnAs(body.Kind))
            {
                visual?.Free();
                visual = CreateVisual(body);
                _visuals[body.Id] = visual;
                created.Add(body.Id);
            }

            if (visual.Surface is PlanetSurface surface)
            {
                surface.Shape = body.Shape;
            }
        }

        RebuildOrbitLines();
        return created;
    }

    /// <summary>Centers the view on a body straight away, with no flight.</summary>
    public void FocusImmediately(Guid bodyId)
    {
        _focusId = bodyId;
        _flightProgress = 1.0;
        Camera?.ClearFocusOffset();
        Place();
    }

    /// <summary>The surface of a planet or moon, or null for a star (or an unknown body).</summary>
    public PlanetSurface? SurfaceFor(Guid bodyId)
    {
        return _visuals.GetValueOrDefault(bodyId)?.Surface;
    }

    /// <summary>Flies the view to a body, which then stays at the scene's center.</summary>
    public void FlyTo(Guid bodyId)
    {
        if (bodyId == _focusId)
        {
            return;
        }

        if (!_layout.ContainsKey(bodyId))
        {
            Place();  // A body added a moment ago isn't placed until the next frame.
            if (!_layout.ContainsKey(bodyId))
            {
                return;
            }
        }

        _flightFrom = Origin;
        _flightFromRadius = _layout.TryGetValue(_focusId, out DisplayBody from)
            ? FramingRadius(_focusId, from)
            : 1;
        double toRadius = FramingRadius(bodyId, _layout[bodyId]);
        _flightFromAltitude = Camera?.CurrentAltitude ?? 2.0;
        _flightToAltitude = _flightFromAltitude <= CloseUpAltitude
            ? _flightFromAltitude
            : (1 + _flightFromAltitude) * _flightFromRadius / toRadius - 1;
        _focusId = bodyId;
        _flightProgress = 0.0;
        if (Camera is not null)
        {
            Camera.ClearFocusOffset();
            Camera.AllowLocalView = false;  // Straight away: the bodies are turned differently.
        }
    }

    /// <summary>
    /// Converts a display position to the scene (relative to the floating origin).
    /// </summary>
    public Vector3 ToScene(Vector3D display)
    {
        Vector3D relative = display - Origin;
        return new Vector3((float)relative.X, (float)relative.Y, (float)relative.Z);
    }

    public override void _Process(double delta)
    {
        if (_flightProgress < 1.0)
        {
            _flightProgress = Math.Min(1.0, _flightProgress + delta / FlightSeconds);
        }

        Place();
    }

    // Puts every body where the world clock says, and moves the origin and camera to follow
    // the focused body.
    private void Place()
    {
        if (Session is null || _visuals.Count == 0)
        {
            return;
        }

        World world = Session.World;
        try
        {
            _layout = PhysicsPositions(world) is { } physics
                ? SystemLayout.At(world.Bodies, physics, _scale)
                : SystemLayout.At(world.Bodies, world.TimeDays, _scale);
        }
        catch (ArgumentException error)
        {
            // Can't happen for a world that loaded; skip the frame rather than crash.
            GD.PushError($"Couldn't place the bodies: {error.Message}");
            return;
        }

        if (!_layout.TryGetValue(FollowMerges(_focusId), out DisplayBody focus))
        {
            return;
        }

        double eased = Ease(_flightProgress);
        Origin = _flightFrom + (focus.Position - _flightFrom) * eased;
        double focusRadius = FramingRadius(_focusId, focus);
        double radius = Math.Exp(Lerp(Math.Log(_flightFromRadius), Math.Log(focusRadius), eased));
        if (_flightProgress >= 1.0)
        {
            Origin = focus.Position;
            radius = focusRadius;
        }

        bool anyStar = false;
        foreach (Body body in world.Bodies)
        {
            if (_visuals.TryGetValue(body.Id, out BodyVisual? visual)
                && _layout.TryGetValue(body.Id, out DisplayBody place))
            {
                PlaceBody(visual, body, place, world.TimeDays);
                anyStar |= body.Kind == BodyKind.Star || body.Tree?.GlowStrength > 0;
            }
            else if (_visuals.TryGetValue(body.Id, out BodyVisual? merged))
            {
                Hide(merged);  // Merged into another body in physics mode
            }
        }

        PlaceBelts(world);
        FollowFocusedSurface(world);

        if (FallbackLight is not null)
        {
            FallbackLight.Visible = !anyStar;
        }

        _guide?.Update(ShowOrbitGuide ? HighlightedOrbit : null, _layout, _scale, ToScene);
        FitCamera(radius);
        if (_flightProgress < 1.0 && Camera is not null)
        {
            // Ease the real distance (radius × (1 + altitude)) smoothly from start to end.
            double altitude = Math.Exp(Lerp(Math.Log(1 + _flightFromAltitude),
                Math.Log(1 + _flightToAltitude), eased)) - 1;
            Camera.SetAltitudeImmediately((float)altitude);
        }

        Placed?.Invoke();
    }

    private void PlaceBody(BodyVisual visual, Body body, DisplayBody place, double timeDays)
    {
        visual.Root.Visible = true;
        if (visual.Light is not null)
        {
            visual.Light.Visible = true;
        }

        visual.Root.Transform = new Transform3D(
            Orientation(body, timeDays).Scaled(Vector3.One * (float)place.Radius),
            ToScene(place.Position));
        if (visual.Surface is PlanetSurface surface)
        {
            surface.ReliefScale = (float)(ReliefExaggeration / (body.RadiusKm * 1000));
            surface.ReliefDetail = ReliefDetail;
            surface.SetShapes(body.Surface.Shapes, body.RadiusKm);
            surface.MapShading = MapStyleShading;
            surface.Style = Style;
        }
        if (visual.Light is OmniLight3D light)
        {
            light.Position = ToScene(place.Position);
        }

        // A star's type can change at any time (VISION.md BOD-06); only touched when it does.
        if (visual.StarMaterial is StandardMaterial3D starMaterial && visual.Light is not null)
        {
            Color color = BodyAppearance.StarColor(body.Appearance.StarType).ToGodot();
            if (starMaterial.AlbedoColor != color)
            {
                starMaterial.AlbedoColor = color;
                visual.Light.LightColor = color.Lightened(0.6f);
            }
        }

        PlaceRings(visual, body);
        if (visual.Tree is WorldTreeVisual tree && body.Tree is WorldTreeLook look)
        {
            tree.Show(look);
        }

        if (visual.Tail is CometTailVisual tail && body.Orbit is Orbit cometOrbit
            && _layout.TryGetValue(cometOrbit.ParentId, out DisplayBody star))
        {
            Vector3D away = place.Position - star.Position;
            tail.Place(ToScene(place.Position), new Vector3((float)away.X, (float)away.Y,
                (float)away.Z), OrbitMath.OffsetFromParent(cometOrbit, timeDays).Length, _scale);
        }

        if (visual.OrbitLine is MeshInstance3D line && body.Orbit is Orbit orbit
            && _layout.TryGetValue(orbit.ParentId, out DisplayBody parent))
        {
            line.Position = ToScene(parent.Position);
            bool highlighted = body.Id == HighlightedOrbit;
            line.MaterialOverride = highlighted ? _highlightMaterial : null;
            // In physics mode bodies leave their designed orbits, so the lines would mislead.
            line.Visible = !FollowsPhysics && (highlighted || body.Id != _focusId
                || (Camera?.CurrentAltitude ?? float.MaxValue) > OwnOrbitLineAltitude);
        }
    }

    private static void Hide(BodyVisual visual)
    {
        visual.Root.Visible = false;
        if (visual.Light is not null)
        {
            visual.Light.Visible = false;
        }

        if (visual.OrbitLine is not null)
        {
            visual.OrbitLine.Visible = false;
        }

        if (visual.Tail is not null)
        {
            visual.Tail.Mesh.Visible = false;
        }
    }

    // Physics mode's latest positions, once it has any (VISION.md SIM-03); null to draw the
    // designed orbits. Asks it to work out the clock's time next.
    private IReadOnlyDictionary<Guid, Vector3D>? PhysicsPositions(World world)
    {
        if (Session?.Physics is not { IsOn: true } physics)
        {
            return null;
        }

        physics.Follow(world.TimeDays);
        return physics.Latest?.Positions;
    }

    // The body a body ended up in, after any merges in physics mode (itself if none).
    private Guid FollowMerges(Guid bodyId)
    {
        IReadOnlyList<Collision> collisions = Session?.Physics.Latest?.Collisions ?? [];
        for (int i = 0; i < collisions.Count && !_layout.ContainsKey(bodyId); i++)
        {
            if (collisions[i].AbsorbedId == bodyId)
            {
                bodyId = collisions[i].IntoId;
            }
        }

        return bodyId;
    }

    // Draws every star's belts around it, making a belt's rocks anew when it's added or edited
    // and removing those of belts that are gone.
    private void PlaceBelts(World world)
    {
        var present = new HashSet<Guid>();
        foreach (Body star in world.Bodies.Where(b => b.Belts.Count > 0))
        {
            if (!_layout.TryGetValue(star.Id, out DisplayBody place))
            {
                continue;
            }

            foreach (AsteroidBelt belt in star.Belts)
            {
                present.Add(belt.Id);
                if (!_belts.TryGetValue(belt.Id, out BeltVisual? visual) || visual.Belt != belt)
                {
                    visual?.Free();
                    visual = new BeltVisual(this, belt);
                    _belts[belt.Id] = visual;
                }

                visual.Place(ToScene(place.Position), place.Radius, star, world.TimeDays, _scale);
            }
        }

        foreach (Guid gone in _belts.Keys.Where(id => !present.Contains(id)).ToList())
        {
            _belts[gone].Free();
            _belts.Remove(gone);
        }
    }

    // Tells a globe which way its star is (for its rings' shadow and its weather by night), and
    // shows, updates, or removes its rings (VISION.md BOD-03), lit from the star.
    private void PlaceRings(BodyVisual visual, Body body)
    {
        Vector3? sun = null;
        if (visual.Surface is PlanetSurface globe)
        {
            Vector3 toStar = Session is not null
                && Seasons.StarFor(Session.World.Bodies, body) is Body star
                && _layout.TryGetValue(star.Id, out DisplayBody starPlace)
                && _layout.TryGetValue(body.Id, out DisplayBody here)
                ? ToGodotDirection(starPlace.Position - here.Position)
                : Vector3.Up;
            sun = (visual.Root.GlobalTransform.Basis.Inverse() * toStar).Normalized();
            globe.SetSunDirection(sun.Value);
        }

        if (body.Rings is not PlanetRings rings || visual.Surface is not PlanetSurface surface)
        {
            if (visual.Rings is not null)
            {
                visual.Rings.Free();
                visual.Rings = null;
                visual.Surface?.SetRings(null, Vector3.Up, 0);
            }

            return;
        }

        visual.Rings ??= new RingsVisual(visual.Root);
        if (visual.Rings.Show(rings, body.Id, body.Shape))
        {
            surface.SetRings(rings, visual.Rings.Normal, visual.Rings.Seed);
        }

        visual.Rings.Light(sun!.Value);  // A surface means the sun was worked out above
    }

    private static Vector3 ToGodotDirection(Vector3D vector) =>
        new Vector3((float)vector.X, (float)vector.Y, (float)vector.Z).Normalized();

    // How far out the camera frames a body: a flat world reaches π times its globe's radius.
    private double FramingRadius(Guid bodyId, DisplayBody place) =>
        Session?.World.Bodies.Find(b => b.Id == bodyId) is Body body
            ? place.Radius * ShapeExtent(body)
            : place.Radius;

    private static double ShapeExtent(Body body) =>
        body.Shape == BodyShape.FlatDisc ? FlatDisc.Radius : 1;

    // A flat world tumbles like a spinning coin (owner's choice: physically flat, VISION.md
    // BOD-02): its spin axis lies across the disc (toward longitude 90° east, the disc's +X),
    // and its top face (+Y) starts facing +Z, then turns with the spin.
    private static readonly Basis _flatDiscTurn = new(Vector3.Up, Vector3.Back, Vector3.Right);

    // The body spins about its own axis, and the axis leans by the tilt toward the tilt
    // direction, so the north pole points exactly where Core's BodyOrientation (and so the
    // season calculations) says. A flat world's spin axis lies across its disc instead.
    private static Basis Orientation(Body body, double timeDays)
    {
        Basis globe = GlobeOrientation(body, timeDays);
        return body.Shape == BodyShape.FlatDisc ? globe * _flatDiscTurn : globe;
    }

    private static Basis GlobeOrientation(Body body, double timeDays)
    {
        var spin = new Basis(
            Vector3.Up, (float)double.DegreesToRadians(BodyClock.SpinDegrees(body, timeDays)));
        Vector3D lean = BodyOrientation.LeanDirection(body);
        Vector3 axis = Vector3.Up.Cross(new Vector3((float)lean.X, 0, (float)lean.Z));
        double tilt = double.DegreesToRadians(body.AxialTiltDegrees);
        return tilt == 0 ? spin : new Basis(axis.Normalized(), (float)tilt) * spin;
    }

    // The local view (VISION.md REN-04) rides with the focused body's spin and tilt. While
    // flying to another body it's off, since the two bodies are turned differently.
    private void FollowFocusedSurface(World world)
    {
        if (Camera is null)
        {
            return;
        }

        bool flying = _flightProgress < 1.0;
        Body? focused = world.Bodies.Find(b => b.Id == _focusId);
        Camera.AllowLocalView = !flying;
        Camera.Shape = focused?.Shape ?? BodyShape.Sphere;
        if (Camera.AllowLocalView && focused is Body focusBody)
        {
            Camera.SurfaceFrame = Orientation(focusBody, world.TimeDays);
            Camera.FollowSurface();
        }
    }

    // Scales the camera to the focused body and lets it zoom out far enough to see the whole
    // system, with a far clipping distance to match. Close up it can get within about 10 km of
    // the ground (the local view, VISION.md REN-04).
    private void FitCamera(double focusRadius)
    {
        if (Camera is null)
        {
            return;
        }

        if (Session?.World.Bodies.Find(b => b.Id == _focusId) is Body focused)
        {
            // Above the highest sculpted peak too, so the camera never ends up inside a hill.
            Camera.MinAltitude = (float)Math.Clamp(
                ClosestApproachKm / (focused.RadiusKm * ShapeExtent(focused)),
                MinRelativeAltitude, 0.05)
                + (SurfaceFor(focused.Id)?.HighestRelief ?? 0);
        }

        double extent = 0;
        foreach (DisplayBody place in _layout.Values)
        {
            extent = Math.Max(extent, (place.Position - Origin).Length + place.Radius);
        }

        Camera.PlanetRadius = (float)focusRadius;
        Camera.MaxAltitude = (float)Math.Max(8.0, 2.5 * extent / focusRadius);
        double near = Math.Max(focusRadius * 1e-6, NearestSurfaceDistance() * 0.02);
        Camera.Near = (float)near;

        // Right down at the ground the near distance is tiny, and the engine can't build a
        // view that also reaches across the whole system (it reports frustum errors). Nothing
        // that far shows when looking straight down anyway.
        double far = Math.Max(100.0 * focusRadius, 6.0 * extent);
        Camera.Far = (float)Math.Min(far, near * MaxDepthRange);
    }

    // How far the camera is from the closest body's surface. The near clipping distance follows
    // it: tiny up close, so the camera can get right down to the ground, and larger when zoomed
    // out. A near distance fixed at a tiny value made the depth buffer run out of precision far
    // away (at true scale the camera can be ~59,000 units out), so distant orbit lines and
    // bodies failed the depth test and weren't drawn.
    private double NearestSurfaceDistance()
    {
        Vector3 at = Camera!.GlobalPosition;
        var camera = new Vector3D(at.X, at.Y, at.Z) + Origin;
        double nearest = double.MaxValue;
        foreach ((Guid id, DisplayBody place) in _layout)
        {
            double distance = _visuals.GetValueOrDefault(id) is
            { Surface.Shape: BodyShape.FlatDisc } flat
                ? DistanceToDisc(flat.Root, at) * place.Radius
                : (place.Position - camera).Length - place.Radius;
            nearest = Math.Min(nearest, distance);
        }

        return nearest;
    }

    // How far a point is from a flat world's disc, in its globe's radii (its own space, where the
    // disc is FlatDisc's size).
    private static double DistanceToDisc(Node3D root, Vector3 point)
    {
        Vector3 local = root.GlobalTransform.AffineInverse() * point;
        double across = Math.Max(0, new Vector2(local.X, local.Z).Length() - FlatDisc.Radius);
        double up = Math.Max(0, Math.Abs(local.Y) - FlatDisc.HalfThickness);
        return Math.Sqrt(across * across + up * up);
    }

    // How a kind of body is drawn: as a star, a world tree, or a globe.
    private static BodyKind DrawnAs(BodyKind kind) =>
        kind is BodyKind.Star or BodyKind.WorldTree ? kind : BodyKind.Planet;

    // A light like a star's: it reaches everything, with no fall-off. It's kept apart from the
    // scaled body, so the body's size doesn't change its reach.
    private OmniLight3D CreateLight(Body body, Color color)
    {
        var light = new OmniLight3D
        {
            Name = $"Light {body.Name}",
            OmniRange = 1e7f,
            OmniAttenuation = 0.0f,
            LightColor = color,
        };
        AddChild(light);
        return light;
    }

    private BodyVisual CreateVisual(Body body)
    {
        var root = new Node3D { Name = $"Body {body.Name}" };
        AddChild(root);
        PlanetSurface? surface = null;
        OmniLight3D? light = null;
        StandardMaterial3D? starMaterial = null;
        WorldTreeVisual? tree = null;
        if (body.Kind == BodyKind.Star)
        {
            Color starColor = BodyAppearance.StarColor(body.Appearance.StarType).ToGodot();
            starMaterial = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = starColor,
            };
            root.AddChild(new MeshInstance3D { Mesh = _sphere, MaterialOverride = starMaterial });
            light = CreateLight(body, starColor.Lightened(0.6f));
        }
        else if (body.Kind == BodyKind.WorldTree)
        {
            light = CreateLight(body, Colors.White);
            tree = new WorldTreeVisual(root, light);
        }
        else
        {
            surface = new PlanetSurface
            {
                Mesh = _sphere,
                MaterialOverride = (ShaderMaterial)PlanetMaterial!.Duplicate(),
            };
            surface.ShowTerrain = _showTerrain;
            if (body.Kind == BodyKind.Comet)
            {
                surface.ShowGrid = false;
            }
            root.AddChild(surface);
        }

        return new BodyVisual(body.Id, root, surface)
        {
            Kind = body.Kind,
            Light = light,
            StarMaterial = starMaterial,
            Tree = tree,
            Tail = body.Kind == BodyKind.Comet ? new CometTailVisual(this) : null,
        };
    }

    private void RebuildOrbitLines()
    {
        if (Session is null)
        {
            return;
        }

        World world = Session.World;
        var byId = world.Bodies.ToDictionary(body => body.Id);
        foreach (BodyVisual visual in _visuals.Values)
        {
            visual.OrbitLine?.QueueFree();
            visual.OrbitLine = null;
            if (byId.GetValueOrDefault(visual.BodyId) is { Orbit: Orbit orbit } body
                && byId.GetValueOrDefault(orbit.ParentId) is Body parent)
            {
                visual.OrbitLine = CreateOrbitLine(body, parent);
            }
        }
    }

    private MeshInstance3D CreateOrbitLine(Body body, Body parent)
    {
        IReadOnlyList<Vector3D> path =
            SystemLayout.OrbitPath(body, parent, _scale, OrbitLineSamples);
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip, _orbitMaterial);
        foreach (Vector3D point in path.Append(path[0]))
        {
            mesh.SurfaceAddVertex(new Vector3((float)point.X, (float)point.Y, (float)point.Z));
        }

        mesh.SurfaceEnd();
        var line = new MeshInstance3D
        {
            Name = $"Orbit {body.Name}",
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(line);
        return line;
    }

    // Smooth start and finish (smoothstep).
    private static double Ease(double t) => t * t * (3 - 2 * t);

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    // A body's nodes in the scene.
    private sealed class BodyVisual(Guid bodyId, Node3D root, PlanetSurface? surface)
    {
        public Guid BodyId { get; } = bodyId;

        public Node3D Root { get; } = root;

        public PlanetSurface? Surface { get; } = surface;

        public MeshInstance3D? OrbitLine { get; set; }

        public OmniLight3D? Light { get; init; }

        public StandardMaterial3D? StarMaterial { get; init; }

        public CometTailVisual? Tail { get; init; }

        public RingsVisual? Rings { get; set; }

        public BodyKind Kind { get; init; }

        public WorldTreeVisual? Tree { get; init; }

        public void Free()
        {
            Rings?.Free();
            Root.QueueFree();
            OrbitLine?.QueueFree();
            Light?.QueueFree();
            Tail?.Mesh.QueueFree();
        }
    }
}
