using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Interop;

namespace NothicWorlds.Rendering;

/// <summary>
/// A globe with shapes added to or cut out of it (VISION.md BOD-04): the sculpted globe as a
/// mesh lifted on the CPU, with each shape added or cut in turn by Godot's CSG. The globe keeps
/// the planet material (its map, terrain, and relief shading); every face a shape makes is bare
/// rock. Lives under the body's <see cref="PlanetSurface"/>, which hides its own mesh meanwhile.
/// A flat world (BOD-10) is carved the same way, as a closed disc whose rim and underside the
/// planet material draws as bare rock.
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

    // Rings out to the rim, and points round each, of a carved flat world's top face: about as
    // many points as the carved globe.
    private const int CarvedRings = 48;
    private const int CarvedSegments = 160;

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
    private (IReadOnlyList<ShapeEdit> Shapes, HeightGrid Heights, double RadiusKm, float Relief,
        BodyShape Shape)? _built;

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
    /// <param name="bodyShape">A globe, or a flat world's disc.</param>
    public void Show(IReadOnlyList<ShapeEdit> shapes, HeightGrid heights, double radiusKm,
        float reliefScale, Material globeMaterial, BodyShape bodyShape)
    {
        if (_built is { } built && built.Shapes.SequenceEqual(shapes)
            && ReferenceEquals(built.Heights, heights) && built.RadiusKm == radiusKm
            && built.Relief == reliefScale && built.Shape == bodyShape)
        {
            return;
        }

        _built = ([.. shapes], heights, radiusKm, reliefScale, bodyShape);
        _combiner?.QueueFree();
        _combiner = new CsgCombiner3D { Name = "Carved", Visible = false };
        _combiner.AddChild(new CsgMesh3D
        {
            Name = "Globe",
            Mesh = bodyShape == BodyShape.FlatDisc
                ? LiftedDisc(heights, reliefScale, globeMaterial)
                : LiftedGlobe(heights, reliefScale, globeMaterial),
        });

        HighestTop = 0;
        foreach (ShapeEdit shape in shapes)
        {
            _combiner.AddChild(ShapeNode(shape, heights, radiusKm, reliefScale, bodyShape));
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

    // A flat world as a closed disc (VISION.md BOD-10): its top face lifted by its heights,
    // as the shader lifts the plain one (never through the underside, and the rim ring by the
    // ground at the south pole, which it all stands for), its rim, and its underside, sharing
    // their edges so CSG can carve it. One surface with the planet material, which draws the
    // rim and underside as bare rock.
    private static ArrayMesh LiftedDisc(HeightGrid heights, float reliefScale, Material material)
    {
        float half = (float)FlatDisc.HalfThickness;
        var points = new List<Vector3>();
        var normals = new List<Vector3>();
        float Lift(Vector3 top) => heights.IsEmpty ? 0 : Math.Max(reliefScale * (float)
            heights.SampleAt(FlatDisc.DirectionFor(new Vector3D(top.X, 0, top.Z))),
            PlanetSurface.FlatDeepestLift);
        int Add(Vector3 point, Vector3 normal)
        {
            points.Add(point);
            normals.Add(normal);
            return points.Count - 1;
        }

        int topMiddle = Add(new Vector3(0, half + Lift(Vector3.Zero), 0), Vector3.Up);
        var rings = new int[CarvedRings, CarvedSegments];
        for (int ring = 0; ring < CarvedRings; ring++)
        {
            float across = (float)(FlatDisc.Radius * (ring + 1) / CarvedRings);
            for (int segment = 0; segment < CarvedSegments; segment++)
            {
                float angle = Mathf.Tau * segment / CarvedSegments;
                var top = new Vector3(Mathf.Sin(angle) * across, 0, Mathf.Cos(angle) * across);
                rings[ring, segment] = Add(top + Vector3.Up * (half + Lift(top)), Vector3.Up);
            }
        }

        var bottom = new int[CarvedSegments];
        for (int segment = 0; segment < CarvedSegments; segment++)
        {
            Vector3 rim = points[rings[CarvedRings - 1, segment]];
            bottom[segment] = Add(new Vector3(rim.X, -half, rim.Z), Vector3.Down);
        }

        int bottomMiddle = Add(new Vector3(0, -half, 0), Vector3.Down);

        // Godot's front faces wind clockwise as seen; segments run counterclockwise seen from
        // above (from +Z toward +X).
        var indices = new List<int>();
        for (int segment = 0; segment < CarvedSegments; segment++)
        {
            int next = (segment + 1) % CarvedSegments;
            indices.AddRange([topMiddle, rings[0, next], rings[0, segment]]);
            for (int ring = 0; ring < CarvedRings - 1; ring++)
            {
                int a = rings[ring, segment], b = rings[ring, next];
                int c = rings[ring + 1, segment], d = rings[ring + 1, next];
                indices.AddRange([a, b, c, b, d, c]);
            }

            int topA = rings[CarvedRings - 1, segment], topB = rings[CarvedRings - 1, next];
            indices.AddRange([topA, topB, bottom[segment], topB, bottom[next], bottom[segment]]);
            indices.AddRange([bottomMiddle, bottom[segment], bottom[next]]);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = points.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    // A shape as a CSG node of unit size, stretched and placed (see Placement).
    private CsgPrimitive3D ShapeNode(ShapeEdit shape, HeightGrid heights, double radiusKm,
        float reliefScale, BodyShape bodyShape)
    {
        Transform3D placement = Placement(shape, heights, radiusKm, reliefScale, bodyShape);
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
            // Above the radius on a globe; above the face on a flat world.
            float top = bodyShape == BodyShape.FlatDisc
                ? placement.Origin.Y - (float)FlatDisc.HalfThickness
                : placement.Origin.Length() - 1;
            HighestTop = Math.Max(HighestTop, top + placement.Basis.Y.Length() / 2);
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
        _preview.Transform = Placement(shape, built.Heights, built.RadiusKm, built.Relief,
            built.Shape);
    }

    // Where a unit-sized shape goes, stretched to its sizes, in the globe's radii. Its middle
    // rises with the drawn ground under its spot (the relief is exaggerated, the shapes
    // aren't), so a shape sitting on a hill still sits on it.
    private static Transform3D Placement(ShapeEdit shape, HeightGrid heights, double radiusKm,
        float reliefScale, BodyShape bodyShape)
    {
        ShapeFrame frame = shape.FrameOn(radiusKm, bodyShape);
        double groundMeters = heights.SampleAt(
            SphericalCoordinates.ToDirection(shape.Spot).ToVector3D());
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
