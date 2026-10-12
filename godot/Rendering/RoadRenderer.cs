using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.Rendering;

/// <summary>
/// Draws the roads on each globe (VISION.md BOD-13; owner's choice: lines, drawn or routed,
/// like rivers): one mesh of lines per globe, a child of its surface so it turns and tilts
/// with the body, each road in its style's color (dirt paths dashed, like trails on a map),
/// the selected one lighter. A body's mesh is rebuilt only when its roads' ways change.
/// </summary>
public partial class RoadRenderer : Node
{
    private const string MeshName = "Roads";

    // Just above the rivers' line, so a road crossing a river shows over it.
    private const float LineLift = 1.0013f;

    // How long each dash of dirt paths is, and the gap after it, in radians of arc.
    private const double DashRadians = 0.005;

    private static readonly Color _highlight = new(1f, 0.95f, 0.6f);

    // What each body's mesh was built from (its ways, the highlight, the globe's shape, and
    // its relief), so it's rebuilt only when that changes.
    private readonly Dictionary<Guid, (IReadOnlyList<RoadCourseShown> Ways,
        Guid? Highlight, BodyShape Shape, int Relief)> _built = [];

    private readonly StandardMaterial3D _material = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
    };

    private Guid? _highlighted;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The system view, whose globes the roads are drawn on.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The road drawn lighter: the one being edited.</summary>
    public Guid? HighlightedId
    {
        get => _highlighted;
        set
        {
            _highlighted = value;
            Refresh();
        }
    }

    /// <summary>The color a road of a style is drawn in, on the globe and in lists.</summary>
    public static Color ColorOf(RoadStyle style) => style switch
    {
        RoadStyle.StoneHighway => new Color(0.86f, 0.84f, 0.8f),
        RoadStyle.DirtPaths => new Color(0.9f, 0.62f, 0.36f),
        RoadStyle.CartTrack => new Color(0.85f, 0.72f, 0.48f),
        _ => new Color(0.55f, 0.55f, 0.52f),
    };

    public override void _Ready()
    {
        if (Session is null || System is null)
        {
            GD.PushError("RoadRenderer needs a world session and system view.");
            return;
        }

        Session.RoadsChanged += Refresh;
        Session.Changed += Refresh;
        Refresh();
    }

    public override void _Process(double delta)
    {
        // Standing on a body, these lines, lifted to show from orbit, would float in its sky.
        if (System is not null)
        {
            foreach (Guid id in _built.Keys)
            {
                if (System.SurfaceFor(id)?.GetNodeOrNull<MeshInstance3D>(MeshName) is { } mesh)
                {
                    mesh.Visible = System.StandingOn != id;
                }
            }
        }

        // The relief changes with the View menu's exaggeration, not only with edits.
        if (System is not null && _built.Any(pair => System.SurfaceFor(pair.Key) is { } globe
            && globe.ReliefVersion != pair.Value.Relief))
        {
            Refresh();
        }
    }

    /// <summary>Brings every globe's road mesh up to date.</summary>
    public void Refresh()
    {
        if (Session is null || System is null)
        {
            return;
        }

        foreach (Body body in Session.World.Bodies)
        {
            if (System.SurfaceFor(body.Id) is not PlanetSurface globe)
            {
                _built.Remove(body.Id);
                continue;
            }

            IReadOnlyList<RoadCourseShown> ways = Session.RoadsOn(body.Id);
            Guid? highlight = ways.Any(w => w.RoadId == _highlighted) ? _highlighted : null;
            var mesh = globe.GetNodeOrNull<MeshInstance3D>(MeshName);
            if (mesh is not null && _built.TryGetValue(body.Id, out var built)
                && ReferenceEquals(built.Ways, ways) && built.Highlight == highlight
                && built.Shape == globe.Shape && built.Relief == globe.ReliefVersion)
            {
                continue;
            }

            if (mesh is null)
            {
                mesh = new MeshInstance3D { Name = MeshName, MaterialOverride = _material };
                mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                globe.AddChild(mesh);
            }

            mesh.Mesh = BuildMesh(ways, highlight, globe);
            _built[body.Id] = (ways, highlight, globe.Shape, globe.ReliefVersion);
        }
    }

    private static ArrayMesh? BuildMesh(IReadOnlyList<RoadCourseShown> ways, Guid? highlight,
        PlanetSurface globe)
    {
        var lines = new List<Vector3>();
        var colors = new List<Color>();
        foreach (RoadCourseShown way in ways)
        {
            Color color = way.RoadId == highlight ? _highlight : ColorOf(way.Style);
            bool dashed = way.Style == RoadStyle.DirtPaths;
            double along = 0;
            for (int i = 1; i < way.Path.Count; i++)
            {
                // Long stretches (drawn roads') in pieces shorter than a dash, so they dash.
                Vector3D start = way.Path[i - 1], end = way.Path[i];
                int pieces = dashed
                    ? Math.Max(1, (int)Math.Ceiling((end - start).Length / (DashRadians / 2)))
                    : 1;
                for (int piece = 0; piece < pieces; piece++)
                {
                    Vector3D from = Between(start, end, (double)piece / pieces);
                    Vector3D to = Between(start, end, (double)(piece + 1) / pieces);
                    along += (to - from).Length;
                    if (dashed && (int)(along / DashRadians) % 2 == 1)
                    {
                        continue;
                    }

                    lines.Add(Place(globe, from));
                    lines.Add(Place(globe, to));
                    colors.Add(color);
                    colors.Add(color);
                }
            }
        }

        if (lines.Count == 0)
        {
            return null;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = lines.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
        return mesh;
    }

    // The unit direction a share of the way from one to the other.
    private static Vector3D Between(Vector3D from, Vector3D to, double share)
    {
        Vector3D v = from * (1 - share) + to * share;
        return v * (1 / v.Length);
    }

    private static Vector3 Place(PlanetSurface globe, Vector3D direction) =>
        GlobeShape.SurfacePoint(globe.Shape, direction,
            LineLift * globe.SurfaceRadiusAt(direction));
}
