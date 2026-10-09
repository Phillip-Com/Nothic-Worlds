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
/// planet material draws as bare rock. As on the plain globe, the ground is raised to the sea
/// or a lake wherever that's higher (BOD-09), so a shape cut below the water is a dry pit.
/// </summary>
/// <remarks>
/// <para>Godot re-carves the whole globe whenever anything changes, on the main thread, so the
/// carved globe is coarser than the plain one (owner's choice, after measuring: 32 squares a
/// face, about 0.17 s on the baseline laptop; slopes are still shaded per height cell). It's
/// only rebuilt when its shapes, heights, size, or relief exaggeration change.</para>
/// <para>The CSG nodes stay hidden: once they've carved (Godot does it the next frame), the
/// result is copied into a plain mesh and drawn, so the last carving stays on show meanwhile
/// and nothing flickers.</para>
/// <para>Godot only carves nodes in the scene, on the main thread, so the carving itself
/// still pauses the app (most of the time a change takes). Everything around it runs on a
/// worker thread: lifting the globe before, and building the meshes clicks and standing use
/// after. <see cref="CarvingChanged"/> lets the toolbar say "Carving…" meanwhile, and the
/// carving waits a drawn frame so that message is on screen before the pause.</para>
/// </remarks>
public partial class ShapedGlobe : Node3D
{
    // Squares along each edge of a face of the carved globe.
    private const ReliefDetail CarvedDetail = (ReliefDetail)32;

    // Rings out to the rim, and points round each, of a carved flat world's top face: about as
    // many points as the carved globe.
    private const int CarvedRings = 48;
    private const int CarvedSegments = 160;

    // How much bigger the hit meshes are built than the globe (a power of two, so exact). Godot
    // takes a ray as running alongside a triangle when a product of its sides is under 1e-5,
    // which missed every face of a carved sphere's small triangles (about 1e-6 at unit size):
    // rays went through the bowl to the globe's far side. 2^16 keeps a 1 km shape's faces hit
    // on a world of over 100,000 km.
    private const float HitScale = 65536;

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

    // How many shaped globes are carving, for CarvingChanged.
    private static int _carvingCount;

    /// <summary>
    /// Raised with true when a shaped globe starts carving while none was, and with false
    /// when the last one is done, so the toolbar can say so.
    /// </summary>
    public static event Action<bool>? CarvingChanged;

    private CsgCombiner3D? _combiner;
    private MeshInstance3D? _preview;
    private MeshInstance3D? _drawn;
    private TriangleMesh? _triangles;  // The drawn carving, for finding where clicks land
    private TriangleMesh? _groundTriangles;  // ... and with the water taken off, for standing
    private bool _waitingForCarving;
    private bool _carving;
    private Task<Lifted>? _lifting;
    private ulong _liftingFrame;  // The frame lifting began, to carve no sooner than the next
    private Godot.Collections.Array? _globeArrays;  // The cube sphere a globe is lifted from
    private Task<(TriangleMesh All, TriangleMesh Ground)>? _hitMeshes;
    private Material? _globeMaterial;
    private (IReadOnlyList<ShapeEdit> Shapes, HeightGrid Heights, double RadiusKm, float Relief,
        BodyShape Shape, int? WaterLevel, HeightGrid LakeLevels)? _built;

    /// <summary>
    /// The highest an added shape reaches above the body's radius, in radii (0 if none does),
    /// so the camera can keep above it.
    /// </summary>
    public float HighestTop { get; private set; }

    /// <summary>Whether a carving is drawn yet (the first one takes a few frames).</summary>
    public bool HasDrawn => _drawn is not null;

    /// <summary>
    /// Raised when the first carving is drawn, so the body's own mesh can stop drawing.
    /// </summary>
    public event Action? FirstDrawn;

    /// <summary>
    /// Builds (or rebuilds, if anything changed) the carved globe.
    /// </summary>
    /// <param name="shapes">The body's shapes, in order.</param>
    /// <param name="heights">Its sculpted heights.</param>
    /// <param name="radiusKm">Its radius, for the shapes' sizes.</param>
    /// <param name="reliefScale">How far a meter lifts the drawn surface, in radii.</param>
    /// <param name="globeMaterial">The planet material the globe's own faces keep.</param>
    /// <param name="bodyShape">A globe, or a flat world's disc.</param>
    /// <param name="waterLevel">The sea's level in meters, or null for none.</param>
    /// <param name="lakeLevels">
    /// The lakes' surfaces (see <see cref="PlanetSurface.SetLakeLevels"/>), empty for none.
    /// </param>
    public void Show(IReadOnlyList<ShapeEdit> shapes, HeightGrid heights, double radiusKm,
        float reliefScale, Material globeMaterial, BodyShape bodyShape, int? waterLevel,
        HeightGrid lakeLevels)
    {
        if (_built is { } built && built.Shapes.SequenceEqual(shapes)
            && ReferenceEquals(built.Heights, heights) && built.RadiusKm == radiusKm
            && built.Relief == reliefScale && built.Shape == bodyShape
            && built.WaterLevel == waterLevel && ReferenceEquals(built.LakeLevels, lakeLevels))
        {
            return;
        }

        _built = ([.. shapes], heights, radiusKm, reliefScale, bodyShape, waterLevel,
            lakeLevels);
        _globeMaterial = globeMaterial;
        _waitingForCarving = false;
        _hitMeshes = null;
        var surface = new CarvedSurface(heights, waterLevel, lakeLevels, reliefScale);
        if (bodyShape == BodyShape.FlatDisc)
        {
            _lifting = Task.Run(() => LiftedDisc(surface));
        }
        else
        {
            _globeArrays = CubeSphereMesh.For(CarvedDetail).SurfaceGetArrays(0);
            Vector3[] unit = _globeArrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            _lifting = Task.Run(() => LiftedGlobe(surface, unit));
        }

        _liftingFrame = Engine.GetProcessFrames();
        HighestTop = 0;
        foreach (ShapeEdit shape in shapes.Where(shape => shape.Operation == ShapeOperation.Add))
        {
            Transform3D placement = Placement(shape, heights, radiusKm, reliefScale, bodyShape);

            // Above the radius on a globe; above the face on a flat world.
            float middle = bodyShape == BodyShape.FlatDisc
                ? placement.Origin.Y - (float)FlatDisc.HalfThickness
                : placement.Origin.Length() - 1;
            HighestTop = Math.Max(HighestTop, middle + placement.Basis.Y.Length() / 2);
        }

        SetCarving(true);
    }

    public override void _Process(double delta)
    {
        if (_lifting is { IsCompleted: true } lifting
            && Engine.GetProcessFrames() > _liftingFrame)
        {
            _lifting = null;
            SetCarving(lifting.IsCompletedSuccessfully);  // A failure is reported just below
            StartCarving(Finished(lifting));
        }
        else if (_waitingForCarving && _combiner?.BakeStaticMesh() is ArrayMesh carved)
        {
            _waitingForCarving = false;
            if (carved.GetSurfaceCount() == 0)
            {
                // Everything was cut away (shapes can't be that big, but just in case): the
                // last carving stays on show.
                SetCarving(false);
                return;
            }

            bool first = _drawn is null;
            _drawn ??= NewDrawnMesh();
            _drawn.Mesh = carved;
            if (first)
            {
                FirstDrawn?.Invoke();
            }

            HitSurface[] hitSurfaces = HitSurfaces(carved);
            bool flat = _built?.Shape == BodyShape.FlatDisc;
            _hitMeshes = Task.Run(() => HitMeshes(hitSurfaces, flat));
        }
        else if (_hitMeshes is { IsCompleted: true } hitMeshes)
        {
            _hitMeshes = null;
            SetCarving(false);
            (_triangles, _groundTriangles) = Finished(hitMeshes);
        }
    }

    public override void _EnterTree()
    {
        SetCarving(_lifting is not null || _waitingForCarving || _hitMeshes is not null);
    }

    public override void _ExitTree()
    {
        SetCarving(false);  // A globe taken away mid-carving isn't carving while it's away
    }

    // The result of work done on a worker thread; anything it threw is a bug, so it's thrown
    // again here, on the main thread, where Godot reports it.
    private static T Finished<T>(Task<T> task) => task.GetAwaiter().GetResult();

    // Puts the lifted globe and the shapes into a fresh CSG tree for Godot to carve, which it
    // does at the end of this frame.
    private void StartCarving(Lifted lifted)
    {
        if (_built is not { } built || _globeMaterial is null)
        {
            return;
        }

        _combiner?.QueueFree();
        _combiner = new CsgCombiner3D { Name = "Carved", Visible = false };
        _combiner.AddChild(new CsgMesh3D
        {
            Name = "Globe",
            Mesh = LiftedMesh(lifted, _globeMaterial),
        });

        foreach (ShapeEdit shape in built.Shapes)
        {
            _combiner.AddChild(ShapeNode(shape, built.Heights, built.RadiusKm, built.Relief,
                built.Shape));
        }

        AddChild(_combiner);
        _waitingForCarving = true;
    }

    private void SetCarving(bool carving)
    {
        if (carving == _carving)
        {
            return;
        }

        _carving = carving;
        _carvingCount += carving ? 1 : -1;
        if (_carvingCount == (carving ? 1 : 0))
        {
            CarvingChanged?.Invoke(carving);
        }
    }

    /// <summary>
    /// Where a ray (in the globe's own space) first meets the carved surface, holes and all, or
    /// null if it misses (or nothing has been carved yet).
    /// </summary>
    public Vector3? RayHit(Vector3 origin, Vector3 direction)
    {
        return Hit(_triangles, origin, direction);
    }

    /// <summary>
    /// As <see cref="RayHit"/>, but with the water taken off: where a ray meets the carved
    /// ground itself, under any sea or lake, for standing on it.
    /// </summary>
    public Vector3? GroundHit(Vector3 origin, Vector3 direction) =>
        Hit(_groundTriangles, origin, direction);

    private static Vector3? Hit(TriangleMesh? triangles, Vector3 origin, Vector3 direction)
    {
        if (triangles is null)
        {
            return null;
        }

        Godot.Collections.Dictionary hit = triangles.IntersectRay(origin * HitScale, direction);
        return hit.Count > 0 ? (Vector3)hit["position"] / HitScale : null;
    }

    // One surface of the carving as plain arrays, for building the hit meshes off the main
    // thread: its points, its triangles' corners (null if every three points make one), and
    // for the globe's own faces, how far the water raised each point (null for a shape's).
    private readonly record struct HitSurface(Vector3[] Points, int[]? Corners,
        Vector2[]? Raised);

    private HitSurface[] HitSurfaces(ArrayMesh carved)
    {
        var surfaces = new HitSurface[carved.GetSurfaceCount()];
        for (int index = 0; index < surfaces.Length; index++)
        {
            Godot.Collections.Array arrays = carved.SurfaceGetArrays(index);
            Variant corners = arrays[(int)Mesh.ArrayType.Index];
            Variant raised = arrays[(int)Mesh.ArrayType.TexUV];
            surfaces[index] = new HitSurface(arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array(),
                corners.VariantType == Variant.Type.Nil ? null : corners.AsInt32Array(),
                carved.SurfaceGetMaterial(index) == _globeMaterial
                    && raised.VariantType != Variant.Type.Nil
                    ? raised.AsVector2Array()
                    : null);
        }

        return surfaces;
    }

    // The meshes rays are tested against (on a worker thread): the whole carving, and the
    // carving with each of the globe's own points lowered by how far it was raised to the
    // water (kept in its first texture coordinate, which carving carries along), for standing
    // on the ground. The shapes' faces are left as they are. Both are built HitScale times
    // bigger.
    private static (TriangleMesh All, TriangleMesh Ground) HitMeshes(HitSurface[] surfaces,
        bool flat)
    {
        var all = new List<Vector3>();
        var ground = new List<Vector3>();
        bool anyRaised = false;
        foreach (HitSurface surface in surfaces)
        {
            Vector3[] lowered = surface.Points;
            if (surface.Raised is { } raised)
            {
                lowered = new Vector3[surface.Points.Length];
                for (int point = 0; point < lowered.Length; point++)
                {
                    Vector3 up = flat ? Vector3.Up : surface.Points[point].Normalized();
                    lowered[point] = surface.Points[point] - up * raised[point].X;
                    anyRaised |= raised[point].X != 0;
                }
            }

            int count = surface.Corners?.Length ?? surface.Points.Length;
            for (int corner = 0; corner < count; corner++)
            {
                int point = surface.Corners?[corner] ?? corner;
                all.Add(surface.Points[point] * HitScale);
                ground.Add(lowered[point] * HitScale);
            }
        }

        var allMesh = new TriangleMesh();
        allMesh.CreateFromFaces(all.ToArray());
        if (!anyRaised)
        {
            return (allMesh, allMesh);
        }

        var groundMesh = new TriangleMesh();
        groundMesh.CreateFromFaces(ground.ToArray());
        return (allMesh, groundMesh);
    }

    private MeshInstance3D NewDrawnMesh()
    {
        var drawn = new MeshInstance3D { Name = "Drawn" };
        AddChild(drawn);
        return drawn;
    }


    // The lifted globe's or disc's points and how far the water raised each (kept in its
    // first texture coordinate, which carving carries along; see HitMeshes); for a disc, also
    // its normals and triangles' corners (a globe takes those from its cube sphere).
    private sealed record Lifted(Vector3[] Points, Vector2[] Raised, Vector3[]? Normals = null,
        int[]? Corners = null);

    // The lifted globe or disc as a mesh wearing the planet material (on the main thread).
    private ArrayMesh LiftedMesh(Lifted lifted, Material material)
    {
        Godot.Collections.Array arrays;
        if (lifted.Normals is null)
        {
            arrays = _globeArrays!;
        }
        else
        {
            arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Normal] = lifted.Normals;
            arrays[(int)Mesh.ArrayType.Index] = lifted.Corners!;
        }

        arrays[(int)Mesh.ArrayType.Vertex] = lifted.Points;
        arrays[(int)Mesh.ArrayType.TexUV] = lifted.Raised;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, material);
        return mesh;
    }

    // The sculpted globe (on a worker thread): each of the cube sphere's points lifted by its
    // height or to the water over it, as the shader lifts the plain one.
    private static Lifted LiftedGlobe(CarvedSurface surface, Vector3[] unit)
    {
        var points = new Vector3[unit.Length];
        var raised = new Vector2[unit.Length];
        for (int index = 0; index < unit.Length; index++)
        {
            Vector3 point = unit[index];
            (float ground, float water) = surface.LiftAt(new Vector3D(point.X, point.Y, point.Z));
            float lift = Math.Max(ground, water);
            points[index] = point * (1.0f + lift);
            raised[index] = new Vector2(lift - ground, 0);
        }

        return new Lifted(points, raised);
    }

    // A flat world as a closed disc (VISION.md BOD-10): its top face lifted by its heights,
    // as the shader lifts the plain one (never through the underside, and the rim ring by the
    // ground at the south pole, which it all stands for), its rim, and its underside, sharing
    // their edges so CSG can carve it. One surface with the planet material, which draws the
    // rim and underside as bare rock. Built on a worker thread.
    private static Lifted LiftedDisc(CarvedSurface surface)
    {
        float half = (float)FlatDisc.HalfThickness;
        var points = new List<Vector3>();
        var normals = new List<Vector3>();
        var raised = new List<Vector2>();
        int Add(Vector3 point, Vector3 normal, float raisedBy = 0)
        {
            points.Add(point);
            normals.Add(normal);
            raised.Add(new Vector2(raisedBy, 0));
            return points.Count - 1;
        }

        // A point of the top face, lifted to the ground or to the water over it.
        int AddTop(Vector3 top)
        {
            (float ground, float water) =
                surface.LiftAt(FlatDisc.DirectionFor(new Vector3D(top.X, 0, top.Z)));
            ground = Math.Max(ground, PlanetSurface.FlatDeepestLift);
            float lift = Math.Max(ground, water);
            return Add(top + Vector3.Up * (half + lift), Vector3.Up, lift - ground);
        }

        int topMiddle = AddTop(Vector3.Zero);
        var rings = new int[CarvedRings, CarvedSegments];
        for (int ring = 0; ring < CarvedRings; ring++)
        {
            float across = (float)(FlatDisc.Radius * (ring + 1) / CarvedRings);
            for (int segment = 0; segment < CarvedSegments; segment++)
            {
                float angle = Mathf.Tau * segment / CarvedSegments;
                var top = new Vector3(Mathf.Sin(angle) * across, 0, Mathf.Cos(angle) * across);
                rings[ring, segment] = AddTop(top);
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

        return new Lifted(points.ToArray(), raised.ToArray(), normals.ToArray(),
            indices.ToArray());
    }

    // A shape as a CSG node of unit size, stretched and placed (see Placement).
    private static CsgPrimitive3D ShapeNode(ShapeEdit shape, HeightGrid heights, double radiusKm,
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

    // The ground and the water over it, which the carved globe's own faces are lifted to.
    private readonly record struct CarvedSurface(HeightGrid Heights, int? WaterLevel,
        HeightGrid LakeLevels, float ReliefScale)
    {
        // How far the ground and the water's surface (the sea's or a lake's, whichever is
        // higher, as in the shader) lift the point at a direction, in radii. The water's lift
        // is negative infinity where there's none.
        public (float Ground, float Water) LiftAt(Vector3D direction)
        {
            float ground = ReliefScale * (float)Heights.SampleAt(direction);
            double water = WaterLevel ?? double.NegativeInfinity;
            if (!LakeLevels.IsEmpty && LakeLevels.HeightAt(direction) is short lake
                && lake > HeightGrid.MinHeightMeters)
            {
                water = Math.Max(water, lake);
            }

            return (ground, double.IsNegativeInfinity(water)
                ? float.NegativeInfinity
                : ReliefScale * (float)water);
        }
    }
}
