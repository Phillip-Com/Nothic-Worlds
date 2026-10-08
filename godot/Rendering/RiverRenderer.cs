using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.Rendering;

/// <summary>
/// Draws the rivers on each globe (VISION.md BOD-11; owner's choice: lines, drawn or natural):
/// one mesh per globe, a child of its surface so it turns and tilts with the body. Each river
/// is a ribbon its true width, widening from a fifth of it at its source to all of it at its
/// mouth, with a thin line along it so it still shows where it's narrower than a pixel. A
/// body's mesh is rebuilt only when its rivers' courses change.
/// </summary>
public partial class RiverRenderer : Node
{
    private const string MeshName = "Rivers";
    private const float RibbonLift = 1.0008f;
    private const float LineLift = 1.0012f;

    private static readonly Color _water = new(0.24f, 0.52f, 0.86f);
    private static readonly Color _highlight = new(0.85f, 0.95f, 1f);

    // What each body's mesh was built from (its courses, the highlight, the globe's shape, and
    // its relief), so it's rebuilt only when that changes.
    private readonly Dictionary<Guid, (IReadOnlyList<RiverCourseShown> Courses,
        Guid? Highlight, BodyShape Shape, int Relief)> _built = [];

    private readonly StandardMaterial3D _material = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    private Guid? _highlighted;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The system view, whose globes the rivers are drawn on.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The river (or lake, for its outflow) drawn lighter: the one being edited.</summary>
    public Guid? HighlightedId
    {
        get => _highlighted;
        set
        {
            _highlighted = value;
            Refresh();
        }
    }

    public override void _Ready()
    {
        if (Session is null || System is null)
        {
            GD.PushError("RiverRenderer needs a world session and system view.");
            return;
        }

        Session.WaterChanged += Refresh;
        Session.Changed += Refresh;
        Refresh();
    }

    public override void _Process(double delta)
    {
        // The relief changes with the View menu's exaggeration, not only with edits.
        if (System is not null && _built.Any(pair => System.SurfaceFor(pair.Key) is { } globe
            && globe.ReliefVersion != pair.Value.Relief))
        {
            Refresh();
        }
    }

    /// <summary>Brings every globe's river mesh up to date.</summary>
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

            IReadOnlyList<RiverCourseShown> courses = Session.WaterOn(body.Id)?.Rivers ?? [];
            Guid? highlight = courses.Any(c => (c.RiverId ?? c.LakeId) == _highlighted)
                ? _highlighted
                : null;
            var mesh = globe.GetNodeOrNull<MeshInstance3D>(MeshName);
            if (mesh is not null && _built.TryGetValue(body.Id, out var built)
                && ReferenceEquals(built.Courses, courses) && built.Highlight == highlight
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

            mesh.Mesh = BuildMesh(courses, highlight, globe, body.RadiusKm);
            _built[body.Id] = (courses, highlight, globe.Shape, globe.ReliefVersion);
        }
    }

    private static ArrayMesh? BuildMesh(IReadOnlyList<RiverCourseShown> courses,
        Guid? highlight, PlanetSurface globe, double radiusKm)
    {
        var ribbon = new List<Vector3>();
        var ribbonColors = new List<Color>();
        var lines = new List<Vector3>();
        var lineColors = new List<Color>();
        foreach (RiverCourseShown course in courses)
        {
            List<Vector3D> path = RiverLine.PathOf(course);
            if (path.Count < 2)
            {
                continue;
            }

            Color color = (course.RiverId ?? course.LakeId) == highlight ? _highlight : _water;
            AddRibbon(ribbon, ribbonColors, path, course.WidthKm / radiusKm, color, globe);
            for (int i = 1; i < path.Count; i++)
            {
                lines.Add(Place(globe, path[i - 1], LineLift));
                lines.Add(Place(globe, path[i], LineLift));
                lineColors.Add(color);
                lineColors.Add(color);
            }
        }

        if (lines.Count == 0)
        {
            return null;
        }

        var mesh = new ArrayMesh();
        AddSurface(mesh, Mesh.PrimitiveType.Triangles, ribbon, ribbonColors);
        AddSurface(mesh, Mesh.PrimitiveType.Lines, lines, lineColors);
        return mesh;
    }

    // A strip along the path, widening from a fifth of the full width (in radians) at the
    // source to all of it at the mouth.
    private static void AddRibbon(List<Vector3> points, List<Color> colors,
        List<Vector3D> path, double widthRadians, Color color, PlanetSurface globe)
    {
        Vector3 previousLeft = default, previousRight = default;
        for (int i = 0; i < path.Count; i++)
        {
            Vector3D here = path[i];
            Vector3D along = path[Math.Min(i + 1, path.Count - 1)]
                - path[Math.Max(i - 1, 0)];
            Vector3D side = Cross(here, along);
            double half = widthRadians / 2 * (0.2 + 0.8 * i / (path.Count - 1));
            side = side.Length > 1e-12 ? side * (half / side.Length) : Vector3D.Zero;
            Vector3 left = Place(globe, Unit(here + side), RibbonLift);
            Vector3 right = Place(globe, Unit(here - side), RibbonLift);
            if (i > 0)
            {
                points.AddRange([previousLeft, previousRight, left, left, previousRight, right]);
                for (int corner = 0; corner < 6; corner++)
                {
                    colors.Add(color);
                }
            }

            (previousLeft, previousRight) = (left, right);
        }
    }

    private static Vector3 Place(PlanetSurface globe, Vector3D direction, float lift) =>
        GlobeShape.SurfacePoint(globe.Shape, direction, lift * globe.SurfaceRadiusAt(direction));

    private static Vector3D Unit(Vector3D v) => v * (1 / v.Length);

    private static Vector3D Cross(Vector3D a, Vector3D b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    private static void AddSurface(
        ArrayMesh mesh, Mesh.PrimitiveType primitive, List<Vector3> points, List<Color> colors)
    {
        if (points.Count == 0)
        {
            return;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = points.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        mesh.AddSurfaceFromArrays(primitive, arrays);
    }
}
