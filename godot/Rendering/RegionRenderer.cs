using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Session;

namespace NothicWorlds.Rendering;

/// <summary>
/// Draws the regions on each globe (VISION.md LORE-01; owner's choice: a colored outline with a
/// light fill): one mesh per globe, a child of its surface so it turns and tilts with the body,
/// lifted just above the map. A body's mesh is rebuilt only when its regions change.
/// </summary>
/// <remarks>
/// The fill is cut into small triangles that hug the sphere (see
/// <see cref="SphericalPolygon.FillTriangles"/>); the outline follows the great-circle edges.
/// Both are drawn unshaded, so night doesn't hide them.
/// </remarks>
public partial class RegionRenderer : Node
{
    private const string MeshName = "Regions";
    private const float FillLift = 1.0015f;
    private const float OutlineLift = 1.003f;
    private const double StepDegrees = 1.5;
    private const float FillAlpha = 0.18f;

    // What each body's mesh was built from (its regions, in order, and the highlight), so it's
    // rebuilt only when that changes.
    private readonly Dictionary<Guid, (List<Region> Regions, Guid? Highlight)> _built = [];

    private readonly StandardMaterial3D _material = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        VertexColorUseAsAlbedo = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    private bool _shown = true;
    private Guid? _highlighted;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The system view, whose globes the regions are drawn on.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>Whether regions are drawn (the toolbar's Regions toggle).</summary>
    public bool ShowRegions
    {
        get => _shown;
        set
        {
            _shown = value;
            Refresh();
        }
    }

    /// <summary>The region drawn with a white outline (the one selected in the panel).</summary>
    public Guid? HighlightedRegionId
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
            GD.PushError("RegionRenderer needs a world session and system view.");
            return;
        }

        Session.Changed += Refresh;
        Session.SelectionChanged += Refresh;
        Refresh();
    }

    /// <summary>Brings every globe's region mesh up to date.</summary>
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

            List<Region> regions = _shown
                ? [.. Session.World.Regions.Where(r => r.BodyId == body.Id)]
                : [];
            Guid? highlight = regions.Any(r => r.Id == _highlighted) ? _highlighted : null;
            var mesh = globe.GetNodeOrNull<MeshInstance3D>(MeshName);
            if (mesh is not null && _built.TryGetValue(body.Id, out var built)
                && built.Regions.SequenceEqual(regions) && built.Highlight == highlight)
            {
                continue;
            }

            if (mesh is null)
            {
                mesh = new MeshInstance3D { Name = MeshName, MaterialOverride = _material };
                mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                globe.AddChild(mesh);
            }

            mesh.Mesh = BuildMesh(regions, highlight);
            _built[body.Id] = (regions, highlight);
        }
    }

    private static ArrayMesh? BuildMesh(List<Region> regions, Guid? highlight)
    {
        if (regions.Count == 0)
        {
            return null;
        }

        var fillPoints = new List<Vector3>();
        var fillColors = new List<Color>();
        var linePoints = new List<Vector3>();
        var lineColors = new List<Color>();
        foreach (Region region in regions)
        {
            Color color = new(region.Color.R / 255f, region.Color.G / 255f, region.Color.B / 255f);
            if (SphericalPolygon.FillTriangles(region.Corners, StepDegrees) is List<Vector3D> fill)
            {
                foreach (Vector3D point in fill)
                {
                    fillPoints.Add(ToGodot(point, FillLift));
                    fillColors.Add(color with { A = FillAlpha });
                }
            }

            Color line = region.Id == highlight ? Colors.White : color;
            List<GeoCoordinate> path = SphericalPolygon.EdgePath(region.Corners, StepDegrees);
            for (int i = 1; i < path.Count; i++)
            {
                linePoints.Add(ToGodot(SphericalPolygon.ToUnit(path[i - 1]), OutlineLift));
                linePoints.Add(ToGodot(SphericalPolygon.ToUnit(path[i]), OutlineLift));
                lineColors.Add(line);
                lineColors.Add(line);
            }
        }

        var mesh = new ArrayMesh();
        AddSurface(mesh, Mesh.PrimitiveType.Triangles, fillPoints, fillColors);
        AddSurface(mesh, Mesh.PrimitiveType.Lines, linePoints, lineColors);
        return mesh;
    }

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

    private static Vector3 ToGodot(Vector3D unit, float lift) =>
        new((float)unit.X * lift, (float)unit.Y * lift, (float)unit.Z * lift);
}
