using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Rendering;

/// <summary>
/// A globe with shapes added to or cut out of it (VISION.md BOD-04): the sculpted globe as a
/// mesh lifted on the CPU, with each shape added or cut in turn by Godot's CSG. The globe keeps
/// the planet material (its map, terrain, and relief shading); every face a shape makes is bare
/// rock. Lives under the body's <see cref="PlanetSurface"/>, which hides its own mesh meanwhile.
/// </summary>
/// <remarks>
/// <para>Godot re-carves the whole globe whenever anything changes, on the main thread, so the
/// carved globe is coarser than the plain one (owner's choice, after measuring: 32 squares a
/// face, about 0.17 s on the baseline laptop; slopes are still shaded per height cell). It's
/// only rebuilt when its shapes, heights, size, or relief exaggeration change.</para>
/// <para>The CSG nodes stay hidden: once they've carved (Godot does it the next frame), the
/// result is copied into a plain mesh and drawn, so the last carving stays on show meanwhile
/// and nothing flickers.</para>
/// </remarks>
public partial class ShapedGlobe : Node3D
{
    // Squares along each edge of a face of the carved globe.
    private const ReliefDetail CarvedDetail = (ReliefDetail)32;

    private static readonly StandardMaterial3D _rockMaterial = new()
    {
        AlbedoColor = new Color(0.36f, 0.33f, 0.30f),
        Roughness = 1.0f,
    };

    /// <summary>
    /// Lights the bare rock to match the visual style (VISION.md REN-05): Godot's banded
    /// "toon" light for Painterly and Simple, true light for Realistic. One world is open at a
    /// time, so every shaped globe shares it.
    /// </summary>
    public static void UseStyle(VisualStyle style)
    {
        bool styled = style != VisualStyle.Realistic;
        _rockMaterial.DiffuseMode = styled
            ? BaseMaterial3D.DiffuseModeEnum.Toon
            : BaseMaterial3D.DiffuseModeEnum.Burley;
        _rockMaterial.SpecularMode = styled
            ? BaseMaterial3D.SpecularModeEnum.Disabled
            : BaseMaterial3D.SpecularModeEnum.SchlickGgx;
    }

    private static readonly StandardMaterial3D _previewMaterial = new()
    {
        AlbedoColor = new Color(1.0f, 0.85f, 0.2f, 0.35f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    private CsgCombiner3D? _combiner;
    private MeshInstance3D? _preview;
    private MeshInstance3D? _drawn;
    private TriangleMesh? _triangles;  // The drawn carving, for finding where clicks land
    private bool _waitingForCarving;
    private (IReadOnlyList<ShapeEdit> Shapes, HeightGrid Heights, double RadiusKm, float Relief)?
        _built;

    /// <summary>
    /// The highest an added shape reaches above the body's radius, in radii (0 if none does),
    /// so the camera can keep above it.
    /// </summary>
    public float HighestTop { get; private set; }

    /// <summary>
    /// Builds (or rebuilds, if anything changed) the carved globe.
    /// </summary>
    /// <param name="shapes">The body's shapes, in order.</param>
    /// <param name="heights">Its sculpted heights.</param>
    /// <param name="radiusKm">Its radius, for the shapes' sizes.</param>
    /// <param name="reliefScale">How far a meter lifts the drawn surface, in radii.</param>
    /// <param name="globeMaterial">The planet material the globe's own faces keep.</param>
    public void Show(IReadOnlyList<ShapeEdit> shapes, HeightGrid heights, double radiusKm,
        float reliefScale, Material globeMaterial)
    {
        if (_built is { } built && built.Shapes.SequenceEqual(shapes)
            && ReferenceEquals(built.Heights, heights) && built.RadiusKm == radiusKm
            && built.Relief == reliefScale)
        {
            return;
        }

        _built = ([.. shapes], heights, radiusKm, reliefScale);
        _combiner?.QueueFree();
        _combiner = new CsgCombiner3D { Name = "Carved", Visible = false };
        _combiner.AddChild(new CsgMesh3D
        {
            Name = "Globe",
            Mesh = LiftedGlobe(heights, reliefScale, globeMaterial),
        });

        HighestTop = 0;
        foreach (ShapeEdit shape in shapes)
        {
            _combiner.AddChild(ShapeNode(shape, heights, radiusKm, reliefScale));
        }

        AddChild(_combiner);
        _waitingForCarving = true;
    }

    public override void _Process(double delta)
    {
        if (!_waitingForCarving || _combiner is null
            || _combiner.BakeStaticMesh() is not ArrayMesh carved || carved.GetSurfaceCount() == 0)
        {
            return;
        }

        _waitingForCarving = false;
        _drawn ??= NewDrawnMesh();
        _drawn.Mesh = carved;
        _triangles = carved.GenerateTriangleMesh();
    }

    /// <summary>
    /// Where a ray (in the globe's own space) first meets the carved surface, holes and all, or
    /// null if it misses (or nothing has been carved yet).
    /// </summary>
    public Vector3? RayHit(Vector3 origin, Vector3 direction)
    {
        if (_triangles is null)
        {
            return null;
        }

        Godot.Collections.Dictionary hit = _triangles.IntersectRay(origin, direction);
        return hit.Count > 0 ? (Vector3)hit["position"] : null;
    }

    private MeshInstance3D NewDrawnMesh()
    {
        var drawn = new MeshInstance3D { Name = "Drawn" };
        AddChild(drawn);
        return drawn;
    }


    // The sculpted globe as a mesh, each vertex lifted by its height (as the shader lifts the
    // plain one), wearing the planet material.
    private static ArrayMesh LiftedGlobe(HeightGrid heights, float reliefScale, Material material)
    {
        Godot.Collections.Array arrays = CubeSphereMesh.For(CarvedDetail).SurfaceGetArrays(0);
        Vector3[] points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        for (int index = 0; index < points.Length; index++)
        {
            Vector3 point = points[index];
            double height = heights.SampleAt(new Vector3D(point.X, point.Y, point.Z));
            points[index] = point * (1.0f + reliefScale * (float)height);
        }

        arrays[(int)Mesh.ArrayType.Vertex] = points;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    // A shape as a CSG node of unit size, stretched and placed (see Placement).
    private CsgPrimitive3D ShapeNode(
        ShapeEdit shape, HeightGrid heights, double radiusKm, float reliefScale)
    {
        Transform3D placement = Placement(shape, heights, radiusKm, reliefScale);
        CsgPrimitive3D node = shape.Kind switch
        {
            ShapeKind.Sphere => new CsgSphere3D
            {
                Radius = 0.5f,
                RadialSegments = 48,
                Rings = 24,
                SmoothFaces = true,
                Material = _rockMaterial,
            },
            ShapeKind.Box => new CsgBox3D { Size = Vector3.One, Material = _rockMaterial },
            _ => new CsgCylinder3D
            {
                Radius = 0.5f,
                Height = 1,
                Sides = 32,
                Cone = shape.Kind == ShapeKind.Cone,
                SmoothFaces = true,
                Material = _rockMaterial,
            },
        };
        node.Name = $"{shape.Operation} {shape.Kind}";
        node.Operation = shape.Operation == ShapeOperation.Add
            ? CsgShape3D.OperationEnum.Union
            : CsgShape3D.OperationEnum.Subtraction;
        node.Transform = placement;
        if (shape.Operation == ShapeOperation.Add)
        {
            HighestTop = Math.Max(HighestTop,
                placement.Origin.Length() + placement.Basis.Y.Length() / 2 - 1);
        }

        return node;
    }

    /// <summary>
    /// Shows <paramref name="shape"/> as a see-through preview where it would be carved (while
    /// a handle drags it; owner's choice: carved when let go), or hides the preview (null).
    /// </summary>
    public void ShowPreview(ShapeEdit? shape)
    {
        if (shape is null || _built is not { } built)
        {
            _preview?.QueueFree();
            _preview = null;
            return;
        }

        if (_preview is null)
        {
            _preview = new MeshInstance3D
            {
                Name = "Preview",
                MaterialOverride = _previewMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(_preview);
        }

        _preview.Mesh = shape.Kind switch
        {
            ShapeKind.Sphere => new SphereMesh { Radius = 0.5f, Height = 1 },
            ShapeKind.Box => new BoxMesh { Size = Vector3.One },
            _ => new CylinderMesh
            {
                TopRadius = shape.Kind == ShapeKind.Cone ? 0 : 0.5f,
                BottomRadius = 0.5f,
                Height = 1,
            },
        };
        _preview.Transform = Placement(shape, built.Heights, built.RadiusKm, built.Relief);
    }

    // Where a unit-sized shape goes, stretched to its sizes, in the globe's radii. Its middle
    // rises with the drawn ground under its spot (the relief is exaggerated, the shapes
    // aren't), so a shape sitting on a hill still sits on it.
    private static Transform3D Placement(
        ShapeEdit shape, HeightGrid heights, double radiusKm, float reliefScale)
    {
        ShapeFrame frame = shape.FrameOn(radiusKm);
        double groundMeters = heights.SampleAt(frame.Up);
        double lift = groundMeters * (reliefScale - 1 / (radiusKm * 1000));
        Vector3D center = frame.Center + frame.Up * lift;
        double width = shape.WidthKm / radiusKm;
        double height = shape.Kind == ShapeKind.Sphere ? width : shape.HeightKm / radiusKm;
        double length = shape.Kind == ShapeKind.Box ? shape.LengthKm / radiusKm : width;

        // East, up, and north make a mirror-image frame, which would turn the shape inside out
        // (CSG would then add a cut). Every shape is the same either side of its width, so
        // flipping that one axis fixes it with no visible change.
        return new Transform3D(
            new Basis(ToGodot(frame.Across * -width), ToGodot(frame.Up * height),
                ToGodot(frame.Along * length)),
            ToGodot(center));
    }

    private static Vector3 ToGodot(Vector3D vector) =>
        new((float)vector.X, (float)vector.Y, (float)vector.Z);
}
