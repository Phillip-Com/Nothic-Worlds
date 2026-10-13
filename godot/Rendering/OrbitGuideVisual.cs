using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.Rendering;

/// <summary>
/// The stable orbit guide in the system view (VISION.md SIM-04): see-through rings around a body
/// (in its equator) and around what it circles (in the body's own orbit), green where an orbit
/// would stay steady and red where it wouldn't (from <see cref="OrbitGuide"/>), and the body's
/// orbit line colored the same way, red where an elongated orbit swings into trouble. Rebuilt
/// only when the world, the body, or the scale changes; each frame just moves it with the
/// bodies.
/// </summary>
public partial class OrbitGuideVisual : Node3D
{
    private const int Segments = 192;
    private const int PathSamples = 256;

    private static readonly Color _steadyColor = new(SteadyColor, 0.16f);
    private static readonly Color _unsteadyColor = new(UnsteadyColor, 0.22f);

    /// <summary>The color of the rings where an orbit would stay steady (fully opaque).</summary>
    public static readonly Color SteadyColor = new(0.3f, 0.9f, 0.4f);

    /// <summary>The color of the rings where an orbit wouldn't stay steady.</summary>
    public static readonly Color UnsteadyColor = new(1.0f, 0.3f, 0.25f);

    private readonly StandardMaterial3D _material = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        VertexColorUseAsAlbedo = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
    };

    // The body's own orbit, colored by the rings it passes through: solid, like an orbit line.
    private readonly StandardMaterial3D _pathMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
    };

    // One mesh of rings per body they're around, by that body.
    private readonly Dictionary<Guid, MeshInstance3D> _rings = [];
    private MeshInstance3D? _path;
    private Guid _pathParentId;
    private Guid? _bodyId;
    private SystemScale _scale;
    private bool _stale = true;

    /// <summary>The open world. Set it before adding the guide to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    /// <summary>
    /// Whether the body's orbit line is drawn colored (off in physics mode, where bodies leave
    /// their designed orbits).
    /// </summary>
    public bool ShowPath { get; set; } = true;

    /// <summary>
    /// Whether the guide is drawing the body's orbit line, so the plain one can stay hidden.
    /// </summary>
    public bool ShowsPath => _path is not null && ShowPath;

    public override void _EnterTree()
    {
        Session.Changed += MarkStale;
        _stale = true;
    }

    public override void _ExitTree()
    {
        Session.Changed -= MarkStale;
    }

    /// <summary>
    /// Shows the guide for <paramref name="bodyId"/> (none for null), drawn as the system view
    /// places the bodies this frame.
    /// </summary>
    /// <param name="bodyId">The body to guide, or null to hide the guide.</param>
    /// <param name="layout">Every body's display position and size this frame.</param>
    /// <param name="scale">How the view draws sizes and distances.</param>
    /// <param name="toScene">Converts a display position to the scene.</param>
    public void Update(Guid? bodyId, IReadOnlyDictionary<Guid, DisplayBody> layout,
        SystemScale scale, Func<Vector3D, Vector3> toScene)
    {
        if (_stale || bodyId != _bodyId || scale != _scale)
        {
            Rebuild(bodyId, layout, scale);
        }

        foreach ((Guid centerId, MeshInstance3D rings) in _rings)
        {
            if (layout.TryGetValue(centerId, out DisplayBody center))
            {
                rings.Position = toScene(center.Position);
            }
        }

        if (_path is not null)
        {
            _path.Visible = ShowPath && layout.ContainsKey(_pathParentId);
            if (layout.TryGetValue(_pathParentId, out DisplayBody parent))
            {
                _path.Position = toScene(parent.Position);
            }
        }
    }

    private void MarkStale() => _stale = true;

    private void Rebuild(Guid? bodyId, IReadOnlyDictionary<Guid, DisplayBody> layout,
        SystemScale scale)
    {
        _stale = false;
        _bodyId = bodyId;
        _scale = scale;
        foreach (MeshInstance3D rings in _rings.Values)
        {
            rings.QueueFree();
        }

        _rings.Clear();
        _path?.QueueFree();
        _path = null;
        List<Body> bodies = Session.World.Bodies;
        if (bodies.Find(b => b.Id == bodyId) is not Body body
            || !layout.TryGetValue(body.Id, out DisplayBody selected))
        {
            return;
        }

        foreach (IGrouping<Guid, GuideBand> around in OrbitGuide.Bands(bodies, body)
            .GroupBy(band => band.CenterId))
        {
            Body center = bodies.Find(b => b.Id == around.Key)!;
            // Around the body, rings line up with its moons' centers; around its parent, with
            // where the body's own center would be.
            double childRadius = center.Id == body.Id ? 0 : selected.Radius;
            var rings = new MeshInstance3D
            {
                Name = $"Guide around {center.Name}",
                Mesh = RingMesh(around, center, layout[center.Id].Radius, childRadius, scale),
                MaterialOverride = _material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Basis = PlaneBasis(OrbitGuide.PlaneNormal(body, center.Id)),
            };
            AddChild(rings);
            _rings[center.Id] = rings;
        }

        AddPath(bodies, body, scale);
    }

    // Turns the rings' flat plane (Y up) to the plane with this axis. A ring looks the same
    // either way up, so an axis pointing down is flipped rather than turned all the way over.
    private static Basis PlaneBasis(Vector3D normal)
    {
        var axis = new Vector3((float)normal.X, (float)normal.Y, (float)normal.Z);
        axis = (axis.Y < 0 ? -axis : axis).Normalized();
        return axis.IsEqualApprox(Vector3.Up)
            ? Basis.Identity
            : new Basis(new Quaternion(Vector3.Up, axis));
    }

    // The body's own orbit, green where it's steady and red where it swings into an unsteady
    // ring, at the same points the system view draws its orbit line through.
    private void AddPath(List<Body> bodies, Body body, SystemScale scale)
    {
        if (body.Orbit is not Orbit orbit
            || bodies.Find(b => b.Id == orbit.ParentId) is not Body parent)
        {
            return;
        }

        IReadOnlyList<double> times = OrbitMath.EvenlySpacedTimes(orbit, PathSamples);
        if (times.Count == 0
            || OrbitGuide.SteadyAlongOrbit(bodies, body, times) is not IReadOnlyList<bool> steady)
        {
            return;
        }

        // Back to the first point, to close the loop.
        var vertices = new Vector3[times.Count + 1];
        var colors = new Color[times.Count + 1];
        for (int i = 0; i <= times.Count; i++)
        {
            int at = i % times.Count;
            Vector3D point = SystemLayout.OrbitPoint(body, parent, times[at], scale);
            vertices[i] = new Vector3((float)point.X, (float)point.Y, (float)point.Z);
            colors[i] = steady[at] ? SteadyColor : UnsteadyColor;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.LineStrip, arrays);
        _path = new MeshInstance3D
        {
            Name = $"Orbit check for {body.Name}",
            Mesh = mesh,
            MaterialOverride = _pathMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_path);
        _pathParentId = parent.Id;
    }

    private ArrayMesh RingMesh(IEnumerable<GuideBand> bands, Body center, double centerRadius,
        double childRadius, SystemScale scale)
    {
        // A ring starting at the body's surface starts at its drawn edge: the readable view
        // draws distances from its center farther out than its drawn size, leaving a gap.
        double extent = center.Shape == BodyShape.FlatDisc ? FlatDisc.Radius : 1;
        double drawnEdge = centerRadius * extent;
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        foreach (GuideBand band in bands)
        {
            double inner = band.InnerKm <= center.RadiusKm * extent
                ? drawnEdge
                : SystemLayout.DisplayDistance(band.InnerKm, centerRadius, childRadius, scale);
            double outer = SystemLayout.DisplayDistance(band.OuterKm, centerRadius, childRadius,
                scale);
            if (outer > inner)
            {
                AddRing(vertices, colors, (float)inner, (float)outer,
                    band.Steady ? _steadyColor : _unsteadyColor);
            }
        }

        var mesh = new ArrayMesh();
        if (vertices.Count > 0)
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        }

        return mesh;
    }

    // A flat ring in the orbit plane (Y up), as triangles.
    private static void AddRing(
        List<Vector3> vertices, List<Color> colors, float inner, float outer, Color color)
    {
        for (int i = 0; i < Segments; i++)
        {
            float from = Mathf.Tau * i / Segments;
            float to = Mathf.Tau * (i + 1) / Segments;
            Vector3 innerFrom = new(inner * Mathf.Cos(from), 0, inner * Mathf.Sin(from));
            Vector3 outerFrom = new(outer * Mathf.Cos(from), 0, outer * Mathf.Sin(from));
            Vector3 innerTo = new(inner * Mathf.Cos(to), 0, inner * Mathf.Sin(to));
            Vector3 outerTo = new(outer * Mathf.Cos(to), 0, outer * Mathf.Sin(to));
            vertices.AddRange([innerFrom, outerFrom, outerTo, innerFrom, outerTo, innerTo]);
            for (int corner = 0; corner < 6; corner++)
            {
                colors.Add(color);
            }
        }
    }
}
