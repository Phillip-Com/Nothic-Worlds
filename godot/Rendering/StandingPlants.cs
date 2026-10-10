using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Rendering;

/// <summary>
/// The plants around the eye while standing (VISION.md REN-06): trees, bushes, small plants,
/// stones, and short grass, as each terrain type's <see cref="PlantCover"/> grows them
/// (<see cref="PlantMix"/>), on spots seeded by the body and the ground's own squares
/// (<see cref="PlantScatter"/>, <see cref="PlantPatches"/>), so they stay put and come back
/// where they were. Placed on a worker against the ground drawn (<see cref="PlantGround"/>)
/// and drawn in batches: one per model's shape (trees a little way off in a simpler shape),
/// the grass, and far trees as flat pictures (<see cref="PlantImpostors"/>) thinning out
/// toward the farthest. The near plants are placed again every few meters the eye moves, the
/// far ones only every few tens: placing them all takes the baseline laptop half a second.
/// Positions are kept, as the ground tiles', relative to a point in the body's own space, in
/// its radii.
/// </summary>
public partial class StandingPlants : Node3D
{
    // The squares each layer is scattered over, at most this wide (meters): small enough that
    // the squares near the edge of a short reach aren't mostly wasted.
    private const double TreeSquareMeters = 128;
    private const double UndergrowthSquareMeters = 32;
    private const double GrassSquareMeters = 8;

    // Each layer's own spots (PlantScatter's layer).
    private const int TreeLayer = 0;
    private const int UndergrowthLayer = 1;
    private const int GrassLayer = 2;

    // The near plants are placed again once the eye has moved this far (meters), the far ones
    // this far; when the ground drawn changes instead, after this long (seconds). The edges
    // are faded, so the lag doesn't show.
    private const double NearMoveMeters = 4;
    private const double FarMoveMeters = 40;
    private const double NearGroundSeconds = 1;
    private const double FarGroundSeconds = 4;

    // How far the ground is sampled either side of a plant to find its slope (meters), and the
    // steepest each may stand on (the cosine of the slope: trees about 37°, the rest 40°, as
    // the ground turns to rock; stones anywhere up to 50°). Far trees aren't checked: their
    // slope can't be seen.
    private const double SlopeStepMeters = 2;
    private const double TreeLevel = 0.8;
    private const double PlantLevel = 0.77;
    private const double StoneLevel = 0.64;

    // How far below the snow line trees stop (meters): the tree line; other plants go up to
    // it.
    private const double TreeLineMeters = 300;

    // Each plant sinks this share of its height into the ground (at most half a meter), so it
    // doesn't float where the ground slopes under it.
    private const double SinkShare = 0.05;

    // Floats a plant takes in a batch: its transform (12), then its custom data (4).
    private const int Stride = 16;

    private readonly PlantModels _models;
    private readonly PlantImpostors _impostors;
    private readonly ITileSurface _surface;
    private readonly double _radiusMeters;
    private readonly int _seed;
    private readonly Reach _reach;
    private readonly int _treeLevel;
    private readonly int _undergrowthLevel;
    private readonly int _grassLevel;
    private readonly Node3D _near = new() { Name = "Near" };
    private readonly Node3D _far = new() { Name = "Far" };
    private readonly Dictionary<(PlantModel Model, int Shape, bool Simple), Batch> _meshes =
        [];
    private readonly Batch _billboards;
    private readonly Batch _grass;
    private readonly ShaderMaterial _meshMaterial;
    private readonly ShaderMaterial _undergrowthMaterial;
    private readonly ShaderMaterial _grassMaterial;
    private readonly ShaderMaterial _billboardMaterial;
    private readonly CancellationTokenSource _stop = new();

    // Each square's plants, before the ground's heights: worked out once, then kept while
    // what grows where stays the same. Only the worker uses it, one placing at a time.
    private readonly Dictionary<(int Layer, GroundTile Tile), Spot[]> _squares = [];
    private long _squaresCover = long.MinValue;

    private readonly Group _nearGroup = new();
    private readonly Group _farGroup = new();
    private Task<Placed>? _placing;
    private bool _failed;
    private bool _sheetShown;  // The trees' pictures are drawn, so far trees can show

    /// <summary>
    /// Makes the plants for a surface (as <see cref="GroundTiles"/>) of a body
    /// <paramref name="radiusMeters"/> in radius, whose own <paramref name="seed"/> places
    /// them, as many as <paramref name="detail"/> says (not Off), drawn with
    /// <paramref name="models"/>.
    /// </summary>
    public StandingPlants(PlantModels models, ITileSurface surface, double radiusMeters,
        int seed, StandingPlantDetail detail)
    {
        Name = "StandingPlants";
        _models = models;
        _surface = surface;
        _radiusMeters = radiusMeters;
        _seed = seed;
        _reach = Reach.For(detail);
        _treeLevel = PlantPatches.Level(surface, TreeSquareMeters, radiusMeters);
        _undergrowthLevel = PlantPatches.Level(surface, UndergrowthSquareMeters, radiusMeters);
        _grassLevel = PlantPatches.Level(surface, GrassSquareMeters, radiusMeters);
        AddChild(_near);
        AddChild(_far);

        // Trees hand over from their shapes to their pictures over the last tenth of the
        // shapes' reach; up close, a trunk or a bush the eye is in is left out rather than
        // seen from inside.
        double handOver = _reach.TreeMeshes * 0.1;
        _meshMaterial = Material("plant_mesh", 0.012f);
        _meshMaterial.SetShaderParameter("fade_out_start", (float)(_reach.TreeMeshes - handOver));
        _meshMaterial.SetShaderParameter("fade_out_end", (float)_reach.TreeMeshes);
        _meshMaterial.SetShaderParameter("clear_within", new Vector2(0.4f, 0.9f));
        _undergrowthMaterial = Material("plant_mesh", 0.012f);
        _undergrowthMaterial.SetShaderParameter("fade_out_start",
            (float)(_reach.Undergrowth * 0.8));
        _undergrowthMaterial.SetShaderParameter("fade_out_end", (float)_reach.Undergrowth);
        _undergrowthMaterial.SetShaderParameter("clear_within", new Vector2(0.6f, 1.2f));
        _grassMaterial = Material("plant_grass", 0.12f);
        _grassMaterial.SetShaderParameter("fade_out_start", (float)(_reach.Grass * 0.7));
        _grassMaterial.SetShaderParameter("fade_out_end", (float)_reach.Grass);
        _billboardMaterial = Material("plant_billboard", 0);
        _billboardMaterial.SetShaderParameter("fade_in_start",
            (float)(_reach.TreeMeshes - handOver));
        _billboardMaterial.SetShaderParameter("fade_in_end", (float)_reach.TreeMeshes);
        _billboardMaterial.SetShaderParameter("fade_out_start", (float)(_reach.Trees * 0.9));
        _billboardMaterial.SetShaderParameter("fade_out_end", (float)_reach.Trees);

        foreach ((PlantModel model, PlantShape[] shapes) in models.Shapes)
        {
            ShaderMaterial material =
                PlantModels.IsTree(model) ? _meshMaterial : _undergrowthMaterial;
            for (int shape = 0; shape < shapes.Length; shape++)
            {
                _meshes[(model, shape, false)] = AddBatch(_near, $"{model}{shape}",
                    shapes[shape].Mesh, material);
                if (shapes[shape].Simple is ArrayMesh simple)
                {
                    _meshes[(model, shape, true)] = AddBatch(_near, $"{model}{shape}Simple",
                        simple, material);
                }
            }
        }

        _grass = AddBatch(_near, "Grass", models.Tuft, _grassMaterial);
        _impostors = new PlantImpostors(models);
        AddChild(_impostors);
        _billboardMaterial.SetShaderParameter("rows", _impostors.Rows);
        _billboardMaterial.SetShaderParameter("columns", PlantImpostors.Columns);
        _billboards = AddBatch(_far, "Billboards", MakeQuad(), _billboardMaterial);
    }

    /// <summary>Required by Godot; the plants are made with the other constructor.</summary>
    public StandingPlants()
        : this(PlantModels.Load(), new GlobeTileSurface(), 6371000, 0,
            StandingPlantDetail.Standard)
    {
    }

    /// <summary>
    /// Keeps the plants placed around an eye at <paramref name="eye"/> (in the body's own
    /// space), <paramref name="eyeHeightMeters"/> above the ground, over a base point
    /// <paramref name="eyeBase"/>: takes in plants placed since last time, and places the
    /// near or the far ones again on a worker if the eye has moved far enough or the ground
    /// has changed. Cheap when nothing has.
    /// </summary>
    public void Update(Vector3D eye, Vector3D eyeBase, double eyeHeightMeters,
        PlantGround ground)
    {
        if (!_sheetShown && _impostors.Sheet is ImageTexture sheet)
        {
            // The far trees, left out until now, are placed.
            _billboardMaterial.SetShaderParameter("sheet", sheet);
            _sheetShown = true;
            _farGroup.PlacedFor = null;
        }

        TakeInPlaced();
        if (_placing is not null || _failed)
        {
            return;
        }

        // The near plants first: they're seen most.
        bool far;
        if (_nearGroup.Due(eye, ground.Version, _radiusMeters, NearMoveMeters,
            NearGroundSeconds))
        {
            far = false;
        }
        else if (_sheetShown && _farGroup.Due(eye, ground.Version, _radiusMeters,
            FarMoveMeters, FarGroundSeconds))
        {
            far = true;
        }
        else
        {
            return;
        }

        Group group = far ? _farGroup : _nearGroup;
        group.Start(eye, ground.Version);
        Placed placed = group.Spare;
        CancellationToken stop = _stop.Token;
        _placing = Task.Run(
            () => Place(far, placed, eye, eyeBase, eyeHeightMeters, ground, stop), stop);
    }

    /// <summary>
    /// Puts the plants in the scene: <paramref name="at"/> gives the transform for a point in
    /// the body's own space, and the scene has <paramref name="metersPerUnit"/>.
    /// </summary>
    public void Place(Func<Vector3D, Transform3D> at, double metersPerUnit)
    {
        _near.GlobalTransform = at(_nearGroup.Origin);
        _far.GlobalTransform = at(_farGroup.Origin);
        foreach (ShaderMaterial material in (ReadOnlySpan<ShaderMaterial>)
            [_meshMaterial, _undergrowthMaterial, _grassMaterial, _billboardMaterial])
        {
            material.SetShaderParameter("meters_per_unit", (float)metersPerUnit);
        }
    }

    public override void _ExitTree() => _stop.Cancel();

    private static ShaderMaterial Material(string shader, float sway)
    {
        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>($"res://Rendering/{shader}.gdshader"),
        };
        material.SetShaderParameter("sway", sway);
        return material;
    }

    private static Batch AddBatch(Node3D parent, string name, Mesh mesh, Material material)
    {
        var multi = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = mesh,
        };
        var node = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = multi,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        parent.AddChild(node);
        return new Batch(node, multi);
    }

    // A quad -0.5 to 0.5 across and 0 to 1 up, for the trees' flat pictures. Its bounds are
    // padded: the shader turns and grows it (to the picture's cell), and the batch is culled
    // by them.
    private static ArrayMesh MakeQuad()
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new Vector3[]
        {
            new(-0.5f, 0, 0), new(0.5f, 0, 0), new(0.5f, 1, 0), new(-0.5f, 1, 0),
        };
        arrays[(int)Mesh.ArrayType.Index] = new[] { 0, 1, 2, 0, 2, 3 };
        var quad = new ArrayMesh
        {
            CustomAabb = new Aabb(new Vector3(-2, -1, -2), Vector3.One * 4),
        };
        quad.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return quad;
    }

    private void TakeInPlaced()
    {
        if (_placing is not { IsCompleted: true } placing)
        {
            return;
        }

        _placing = null;
        if (placing.IsCanceled)
        {
            return;
        }

        if (placing.IsFaulted)
        {
            // Plants are only looks: the view goes on without them, and the log says why.
            _failed = true;
            GD.PushError($"Couldn't place the plants: {placing.Exception?.GetBaseException()}");
            return;
        }

        Placed placed = placing.Result;
        if (placed.Far)
        {
            _billboards.Show(placed.Billboards);
            _farGroup.Shown(placed);
            return;
        }

        foreach ((var key, Batch batch) in _meshes)
        {
            batch.Show(placed.Meshes.GetValueOrDefault(key));
        }

        _grass.Show(placed.Grass);
        _nearGroup.Shown(placed);
    }

    // Places the near plants (grass, undergrowth, and trees' shapes) or the far ones (trees'
    // pictures) around the eye, into `placed` (emptied first): on a worker.
    private Placed Place(bool far, Placed placed, Vector3D eye, Vector3D eyeBase,
        double eyeHeightMeters, PlantGround ground, CancellationToken stop)
    {
        if (ground.CoverVersion != _squaresCover)
        {
            _squares.Clear();
            _squaresCover = ground.CoverVersion;
        }

        placed.Reset(far, eye);
        ReadOnlySpan<(int, int, double)> layers = far
            ? [(TreeLayer, _treeLevel, _reach.Trees)]
            :
            [
                (TreeLayer, _treeLevel, _reach.TreeMeshes),
                (UndergrowthLayer, _undergrowthLevel, _reach.Undergrowth),
                (GrassLayer, _grassLevel, _reach.Grass),
            ];
        var wanted = new HashSet<(int, GroundTile)>();
        foreach ((int layer, int level, double reach) in layers)
        {
            // Seen from high up, nothing's within reach.
            if (eyeHeightMeters >= reach)
            {
                continue;
            }

            foreach (GroundTile tile in PlantPatches.Around(_surface, eyeBase,
                reach / _radiusMeters, level))
            {
                stop.ThrowIfCancellationRequested();
                wanted.Add((layer, tile));
                if (!_squares.TryGetValue((layer, tile), out Spot[]? spots))
                {
                    spots = Choose(layer, tile, ground);
                    _squares[(layer, tile)] = spots;
                }

                foreach (Spot spot in spots)
                {
                    PlaceSpot(spot, far, eyeBase, eyeHeightMeters, ground, placed);
                }
            }
        }

        // Squares left behind are let go: the trees' by the far placing, which reaches
        // farthest, the rest by the near.
        foreach (var key in _squares.Keys
            .Where(key => (key.Layer == TreeLayer) == far && !wanted.Contains(key))
            .ToList())
        {
            _squares.Remove(key);
        }

        return placed;
    }

    // What grows on a square's spots (those where anything does), before the ground's
    // heights are known.
    private Spot[] Choose(int layer, GroundTile tile, PlantGround ground)
    {
        double area = PlantPatches.AreaSquareMeters(_surface, tile, _radiusMeters);
        bool grass = layer == GrassLayer;
        double most = grass
            ? PlantMix.MostGrassPerSquareMeter * _reach.GrassDensity
            : PlantMix.MostPerHectare((PlantLayer)layer) / 10_000 * _reach.Density;
        var spots = new List<Spot>();
        foreach (ScatterPoint point in PlantScatter.Points(_seed, layer, tile, most * area))
        {
            (double u, double v) = tile.OnRoot(point.Across, point.Down);
            Vector3D basePoint = _surface.BasePoint(tile.Root, u, v);
            (PlantCover cover, Color grassColor) = ground.CoverAt(basePoint);
            float phase = (float)point.Turn;
            if (grass)
            {
                (double perSquareMeter, double heightMeters) = PlantMix.Grass(cover);
                if (point.Pick * PlantMix.MostGrassPerSquareMeter < perSquareMeter)
                {
                    float shade = 0.85f + 0.3f * (float)point.Shape;
                    spots.Add(new Spot(basePoint, SpotKind.Grass, default, 0,
                        heightMeters * (0.7 + 0.6 * point.Size), point.Turn, 0,
                        new Color(grassColor.R * shade, grassColor.G * shade,
                            grassColor.B * shade, phase)));
                }

                continue;
            }

            if (PlantMix.Choose(cover, (PlantLayer)layer, point.Pick) is not PlantShare share)
            {
                continue;
            }

            int shapes = _models.Shapes[share.Model].Length;
            double shapeAt = point.Shape * shapes;
            int shape = Math.Min((int)shapeAt, shapes - 1);
            float bright = 0.88f + 0.24f * (float)point.Size;
            spots.Add(new Spot(basePoint,
                PlantModels.IsTree(share.Model) ? SpotKind.Tree : SpotKind.Undergrowth,
                share.Model, shape,
                share.MinHeightMeters
                    + (share.MaxHeightMeters - share.MinHeightMeters) * point.Size,
                point.Turn, shapeAt - shape, new Color(bright, bright, bright, phase)));
        }

        return spots.ToArray();
    }

    // Puts one plant on the drawn ground, if it's in reach (of the near plants or the far)
    // and the ground there will have it.
    private void PlaceSpot(Spot spot, bool far, Vector3D eyeBase, double eyeHeightMeters,
        PlantGround ground, Placed placed)
    {
        double across = _surface.Across(eyeBase, spot.Base) * _radiusMeters;
        double away = Math.Sqrt(across * across + eyeHeightMeters * eyeHeightMeters);
        bool tree = spot.Kind == SpotKind.Tree;
        if (far)
        {
            // Pictures overlap the trees' shapes where they hand over; far trees thin out,
            // the same ones always going first.
            double thinFrom = _reach.TreeMeshes * 2.5;
            if (away > _reach.Trees || away < _reach.TreeMeshes * 0.9
                || (away > thinFrom && spot.Rank > thinFrom / away))
            {
                return;
            }
        }
        else if (away > spot.Kind switch
        {
            SpotKind.Grass => _reach.Grass,
            SpotKind.Undergrowth => _reach.Undergrowth,
            _ => _reach.TreeMeshes,
        })
        {
            return;
        }

        if (ground.Height(spot.Base) is not double height
            || ground.Wet(spot.Base, height)
            || ground.InRiver(spot.Base)
            || ground.Snowy(spot.Base, height, tree ? TreeLineMeters : 0))
        {
            return;
        }

        Vector3D up = _surface.Place(spot.Base, height + 1e-3) - _surface.Place(spot.Base, height);
        up *= 1 / up.Length;
        Vector3D side = Math.Abs(up.Z) < 0.9 ? new Vector3D(0, 0, 1) : new Vector3D(1, 0, 0);
        Vector3D right = side.Cross(up);
        right *= 1 / right.Length;
        Vector3D forward = right.Cross(up);
        if (!far && !LevelEnough(spot, height, right, forward, ground))
        {
            return;
        }

        double sink = Math.Min(spot.HeightMeters * SinkShare, 0.5);
        Vector3D at = _surface.Place(spot.Base, height - sink / _radiusMeters) - placed.Origin;
        double angle = spot.Turn * Math.Tau;
        double scale = spot.HeightMeters / _radiusMeters;
        Vector3D x = (right * Math.Cos(angle) + forward * Math.Sin(angle)) * scale;
        Vector3D y = up * scale;
        Vector3D z = (forward * Math.Cos(angle) - right * Math.Sin(angle)) * scale;
        if (far)
        {
            (int cell, float grow) = _impostors.CellOf(spot.Model, spot.Shape);
            placed.Billboards.Add(x, y, z, at, new Color(cell, grow, spot.Tint.R, spot.Tint.A));
        }
        else if (spot.Kind == SpotKind.Grass)
        {
            placed.Grass.Add(x, y, z, at, spot.Tint);
        }
        else
        {
            bool simple = tree && away > _reach.TreeDetail
                && _models.Shapes[spot.Model][spot.Shape].Simple is not null;
            placed.MeshesFor((spot.Model, spot.Shape, simple)).Add(x, y, z, at, spot.Tint);
        }
    }

    // Whether the ground at a spot is level enough for what grows there.
    private bool LevelEnough(Spot spot, double height, Vector3D right, Vector3D forward,
        PlantGround ground)
    {
        double step = SlopeStepMeters / _radiusMeters;
        if (ground.Height(spot.Base + right * step) is not double toRight
            || ground.Height(spot.Base + forward * step) is not double toForward)
        {
            return false;
        }

        double riseRight = (toRight - height) / step, riseForward = (toForward - height) / step;
        double level = 1 / Math.Sqrt(1 + riseRight * riseRight + riseForward * riseForward);
        return level >= spot.Kind switch
        {
            SpotKind.Tree => TreeLevel,
            _ when spot.Model is PlantModel.Boulder or PlantModel.MossyBoulder => StoneLevel,
            _ => PlantLevel,
        };
    }

    // How far and how thickly each layer is drawn at a detail (meters, and shares of
    // PlantMix's thickness): trees in full out to TreeDetail, simpler to TreeMeshes, then as
    // pictures.
    private readonly record struct Reach(double Grass, double Undergrowth, double TreeDetail,
        double TreeMeshes, double Trees, double Density, double GrassDensity)
    {
        public static Reach For(StandingPlantDetail detail) => detail switch
        {
            StandingPlantDetail.Low => new(20, 40, 25, 90, 800, 0.6, 0.5),
            StandingPlantDetail.High => new(50, 100, 80, 300, 3000, 1, 1),
            _ => new(30, 60, 40, 150, 1500, 1, 0.8),
        };
    }

    private enum SpotKind
    {
        Tree,
        Undergrowth,
        Grass,
    }

    // A spot something grows on: where, what, how tall (meters), which way it faces (0 to 1),
    // the even random number that thins far trees (0 to 1), and its tint (rgb) and sway (a).
    private readonly record struct Spot(Vector3D Base, SpotKind Kind, PlantModel Model,
        int Shape, double HeightMeters, double Turn, double Rank, Color Tint);

    // The near plants or the far: where they were placed around and for which ground, when,
    // and a spare set of lists the next placing fills (the engine copies what it's shown, so
    // the lists shown last are reused).
    private sealed class Group
    {
        private readonly System.Diagnostics.Stopwatch _sincePlaced =
            System.Diagnostics.Stopwatch.StartNew();

        public (Vector3D Eye, long Version)? PlacedFor { get; set; }

        public Vector3D Origin { get; private set; }

        public Placed Spare { get; private set; } = new();

        // Whether they should be placed again: never yet, moved `moveMeters`, or the ground
        // changed `groundSeconds` ago or more.
        public bool Due(Vector3D eye, long version, double radiusMeters, double moveMeters,
            double groundSeconds) =>
            PlacedFor is not var (lastEye, lastVersion)
            || (eye - lastEye).Length * radiusMeters >= moveMeters
            || (lastVersion != version && _sincePlaced.Elapsed.TotalSeconds >= groundSeconds);

        public void Start(Vector3D eye, long version)
        {
            PlacedFor = (eye, version);
            _sincePlaced.Restart();
        }

        public void Shown(Placed placed)
        {
            Origin = placed.Origin;
            Spare = placed;
        }
    }

    // Plants placed around a point, ready for their batches.
    private sealed class Placed
    {
        public bool Far { get; private set; }

        public Vector3D Origin { get; private set; }

        public Dictionary<(PlantModel, int, bool), Instances> Meshes { get; } = [];

        public Instances Billboards { get; } = new();

        public Instances Grass { get; } = new();

        public Instances MeshesFor((PlantModel, int, bool) key)
        {
            if (!Meshes.TryGetValue(key, out Instances? list))
            {
                Meshes[key] = list = new Instances();
            }

            return list;
        }

        public void Reset(bool far, Vector3D origin)
        {
            Far = far;
            Origin = origin;
            Billboards.Clear();
            Grass.Clear();
            foreach (Instances list in Meshes.Values)
            {
                list.Clear();
            }
        }
    }

    // A batch's plants as the engine keeps them: a transform's rows, then the custom data,
    // in room for a power of two of them (so a batch's room seldom changes).
    private sealed class Instances
    {
        public float[] Data { get; private set; } = new float[Stride * 64];

        public int Count { get; private set; }

        public void Clear()
        {
            // The old plants past the new count are never drawn (VisibleInstanceCount).
            Count = 0;
        }

        public void Add(Vector3D x, Vector3D y, Vector3D z, Vector3D origin, Color custom)
        {
            if ((Count + 1) * Stride > Data.Length)
            {
                float[] grown = new float[Data.Length * 2];
                Array.Copy(Data, grown, Count * Stride);
                Data = grown;
            }

            int i = Count * Stride;
            float[] d = Data;
            d[i] = (float)x.X; d[i + 1] = (float)y.X; d[i + 2] = (float)z.X;
            d[i + 3] = (float)origin.X;
            d[i + 4] = (float)x.Y; d[i + 5] = (float)y.Y; d[i + 6] = (float)z.Y;
            d[i + 7] = (float)origin.Y;
            d[i + 8] = (float)x.Z; d[i + 9] = (float)y.Z; d[i + 10] = (float)z.Z;
            d[i + 11] = (float)origin.Z;
            d[i + 12] = custom.R; d[i + 13] = custom.G; d[i + 14] = custom.B;
            d[i + 15] = custom.A;
            Count++;
        }
    }

    // One batch of plants drawn together.
    private sealed class Batch(MultiMeshInstance3D node, MultiMesh multi)
    {
        public MultiMeshInstance3D Node { get; } = node;

        public void Show(Instances? instances)
        {
            int count = instances?.Count ?? 0;
            if (count == 0)
            {
                multi.VisibleInstanceCount = 0;
                Node.Visible = false;
                return;
            }

            // The batch has the list's room, so the list is sent as it is.
            int room = instances!.Data.Length / Stride;
            if (multi.InstanceCount != room)
            {
                multi.InstanceCount = room;
            }

            RenderingServer.MultimeshSetBuffer(multi.GetRid(), instances.Data);
            multi.VisibleInstanceCount = count;
            Node.Visible = true;
        }
    }
}
