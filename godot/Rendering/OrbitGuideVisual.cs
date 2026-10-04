using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.Rendering;

/// <summary>
/// The stable orbit guide in the system view (VISION.md SIM-04): see-through rings, flat in the
/// orbit plane, around a body and around what it circles, green where an orbit would stay
/// steady and red where it wouldn't (from <see cref="OrbitGuide"/>). The rings are rebuilt only
/// when the world, the body, or the scale changes; each frame just moves them with the bodies.
/// </summary>
public partial class OrbitGuideVisual : Node3D
{
    private const int Segments = 192;

    private static readonly Color _steadyColor = new(0.3f, 0.9f, 0.4f, 0.16f);
    private static readonly Color _unsteadyColor = new(1.0f, 0.3f, 0.25f, 0.22f);

    private readonly StandardMaterial3D _material = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        VertexColorUseAsAlbedo = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
    };

    // One mesh of rings per body they're around, by that body.
    private readonly Dictionary<Guid, MeshInstance3D> _rings = [];
    private Guid? _bodyId;
    private SystemScale _scale;
    private bool _stale = true;

    /// <summary>The open world. Set it before adding the guide to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

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
            };
            AddChild(rings);
            _rings[center.Id] = rings;
        }
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
