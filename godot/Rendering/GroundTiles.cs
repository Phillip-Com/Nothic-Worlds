using System.Collections.Concurrent;
using System.Diagnostics;
using Godot;
using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Rendering;

/// <summary>
/// The ground around a first-person eye (VISION.md REN-06), and the water's surface over it:
/// tiles that stay put on the ground (<see cref="GroundTileSelection"/>), finer near the eye,
/// their points morphing onto the coarser tile's shape before it takes over, so the ground
/// neither swims nor pops as the eye moves. Tiles are built on worker threads and kept for
/// reuse; until a tile is built a coarser one stands in. The tiles drawn are joined into one
/// mesh for the ground and one for the water, nearest first, on a worker too, the last ones
/// drawn until they're ready: a few hundred meshes cost far more to draw than their
/// triangles, and drawn nearest first, nearer ground hides what's behind it before that's
/// shaded (this renderer has no depth pass first). Positions are kept relative to a point near
/// the eye, so they hold their precision a meter from it;
/// each point's direction from the globe's middle (on a flat world, its place) rides in
/// CUSTOM0, as the globe's material reads it, and how far it moves as it morphs, and by what
/// distance from the eye it has, in CUSTOM1.
/// </summary>
public partial class GroundTiles : Node3D
{
    // How far a tile's skirt hangs below its edge, for each unit of the tile's width.
    private const double SkirtShare = 0.05;

    // The eye counts as standing still while it's moved less than this share of the narrowest
    // tile's width.
    private const double StillShare = 0.05;

    // How far the eye goes (in meters) before the meshes are joined again around it.
    private const double RebaseMeters = 1000;

    // How many tile widths from the eye a tile is split. At 1.5 or more, a tile's neighbors
    // are never more than one level coarser, so their edges meet once it has morphed (any
    // less, and a hairline crack can open between them).
    private const double SplitFactor = 1.5;

    // The narrowest a tile gets, in meters, whatever the detail: the detail sets how many
    // squares it's split into (8, 16, or 32 at Low, Standard, and High: squares 8, 4, or 2 m
    // across underfoot). The ground's heights come from a much coarser grid anyway (VISION.md
    // REN-06, Limits), and rivers' beds and banks have their own fine strip.
    private const double FinestTileMeters = 64;

    // How many built tiles are kept for reuse: enough for the eye to turn round and come back.
    private const int CacheTiles = 600;

    // The least time between joinings of the tiles drawn, in seconds: moving fast, the drawn
    // set changes nearly every frame, and each joining sends the whole mesh to the GPU.
    private const double MinJoinSeconds = 0.2;

    private readonly ITileSurface _surface;
    private readonly GroundTileSelection _selection;
    private readonly int _cells;
    private readonly int[] _triangles;
    private readonly int _maxBuilding = Math.Clamp(System.Environment.ProcessorCount - 1, 1, 6);
    private readonly Dictionary<GroundTile, Built> _built = [];
    private readonly Dictionary<GroundTile, long> _building = [];
    private readonly Dictionary<GroundTile, long> _failed = [];
    private readonly ConcurrentQueue<Finished> _finished = new();
    private readonly ConcurrentQueue<Joined> _joined = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly double _rebaseRadii;
    private readonly MeshInstance3D _ground = MakeInstance("Ground");
    private readonly MeshInstance3D _water = MakeInstance("Water");
    private readonly Stopwatch _sinceJoined = Stopwatch.StartNew();
    private HashSet<GroundTile> _drawn = [];
    private int _deepest;            // The finest level among them
    private (Vector3D Eye, Vector3D ReachCenter, double Reach, long Recipes)? _chosenFor;
    private Vector3D? _origin;       // The point the meshes are joined around
    private Vector3D _joinedOrigin;  // ... as last shown
    private Dictionary<GroundTile, TileMeshes> _joinedTiles = [];  // The tiles last shown
    private bool _joinDue;           // The tiles drawn have changed since they were last joined
    private bool _joining;           // A joining is under way on a worker
    private long _frame;

    /// <summary>
    /// Makes the tiles for a surface (a globe's radius measured in radii, 1 at its base; a flat
    /// world's top face, heights above it) of a body <paramref name="radiusMeters"/> in
    /// radius, as finely as <paramref name="detail"/> says.
    /// </summary>
    public GroundTiles(ITileSurface surface, double radiusMeters, StandingGroundDetail detail)
    {
        Name = "GroundTiles";
        _surface = surface;
        _rebaseRadii = RebaseMeters / radiusMeters;
        _cells = detail switch
        {
            StandingGroundDetail.Low => 8,
            StandingGroundDetail.High => 32,
            _ => 16,
        };
        _triangles = GroundTileGrid.Triangles(_cells);
        _selection = new GroundTileSelection(surface, SplitFactor,
            FinestTileMeters / radiusMeters);
        AddChild(_ground);
        AddChild(_water);
    }

    /// <summary>Required by Godot; the tiles are made with the other constructor.</summary>
    public GroundTiles()
        : this(new GlobeTileSurface(), 6371000, StandingGroundDetail.Standard)
    {
    }

    /// <summary>
    /// Sets up the ground's mesh each time it's joined, given the point its positions are kept
    /// relative to (in the body's own space): FirstPersonMode places the fine ground detail
    /// there.
    /// </summary>
    public Action<GeometryInstance3D, Vector3D>? PrepareGround { get; set; }

    /// <summary>As <see cref="PrepareGround"/>, for the water's surface (its waves).</summary>
    public Action<GeometryInstance3D, Vector3D>? PrepareWater { get; set; }

    /// <summary>
    /// Goes up each time the ground shown changes (see <see cref="ShownHeights"/>).
    /// </summary>
    public int ShownVersion { get; private set; }

    /// <summary>Whether any ground is drawn yet (none until the first tiles are built).</summary>
    public bool HasGround => _ground.Mesh is not null;

    /// <summary>The material the ground is drawn with (the globe's own).</summary>
    public Material? GroundMaterial
    {
        get => _ground.MaterialOverride;
        set => _ground.MaterialOverride = value;
    }

    /// <summary>The material the water's surface is drawn with.</summary>
    public Material? WaterMaterial
    {
        get => _water.MaterialOverride;
        set => _water.MaterialOverride = value;
    }

    /// <summary>
    /// Keeps the right tiles drawn for an eye at <paramref name="eye"/> (in the body's own
    /// space), out to <paramref name="reach"/> along the surface from
    /// <paramref name="reachCenter"/> (a base point): takes in tiles finished since last time,
    /// chooses the tiles, starts building those wanted, and joins those drawn.
    /// <paramref name="recipeFor"/> gives how a tile is built (given the space it takes up, or
    /// a guess at it); a tile built another way is built again. <paramref name="recipes"/>
    /// names the recipes it gives: the tiles are chosen again only when it changes, a tile has
    /// been built, or the eye or the reach has moved, so standing still costs next to nothing.
    /// </summary>
    public void Update(Vector3D eye, Vector3D reachCenter, double reach, long recipes,
        Func<GroundTile, TileExtent, GroundTileRecipe> recipeFor)
    {
        TakeInJoined();
        int taken = TakeInFinished();
        double still = _selection.FinestWidth * StillShare;
        bool moved = _chosenFor is not var (lastEye, lastCenter, lastReach, lastRecipes)
            || lastRecipes != recipes || lastReach != reach
            || (eye - lastEye).Length >= still || (reachCenter - lastCenter).Length >= still;
        if (taken > 0 || moved)
        {
            _chosenFor = (eye, reachCenter, reach, recipes);
            Choose(eye, reachCenter, reach, recipeFor);
        }

        if (_origin is not Vector3D origin || (eye - origin).Length > _rebaseRadii)
        {
            _origin = eye;
            _joinDue = true;
        }

        if (_joinDue && !_joining && _sinceJoined.Elapsed.TotalSeconds >= MinJoinSeconds)
        {
            Join();
        }
    }

    /// <summary>
    /// Puts the ground and water in the scene: <paramref name="at"/> gives the transform for a
    /// point in the body's own space.
    /// </summary>
    public void Place(Func<Vector3D, Transform3D> at)
    {
        Transform3D transform = at(_joinedOrigin);
        _ground.GlobalTransform = transform;
        _water.GlobalTransform = transform;
    }

    /// <summary>
    /// The highest the drawn ground reaches around a point on or over the surface (as the
    /// surface measures height): the highest corner of the square of mesh it's over, morphed or
    /// not. The mesh is flat between its points, so where the ground curves or steps it can
    /// stand above the ground's own height; an eye kept above this never sinks into it. Null
    /// if no drawn tile is under it.
    /// </summary>
    public double? HighestAround(Vector3D point)
    {
        if (Square(point, _deepest,
            tile => _drawn.Contains(tile) ? _built[tile].Meshes : null)
            is not var (meshes, column, row, _, _))
        {
            return null;
        }

        double highest = double.MinValue;
        foreach ((int c, int r) in (ReadOnlySpan<(int, int)>)
            [(column, row), (column + 1, row), (column, row + 1), (column + 1, row + 1)])
        {
            int index = GroundTileGrid.Index(c, r, _cells);
            highest = Math.Max(highest, Math.Max(meshes.Heights[index],
                meshes.MorphHeights[index]));
        }

        return highest;
    }

    /// <summary>
    /// The base point <paramref name="across"/> and <paramref name="down"/> a tile (0 to 1
    /// each), as its heights are asked for.
    /// </summary>
    public Vector3D BasePoint(GroundTile tile, double across, double down)
    {
        (double u, double v) = tile.OnRoot(across, down);
        return _surface.BasePoint(tile.Root, u, v);
    }

    /// <summary>The width of a tile's squares, in the surface's units.</summary>
    public double CellWidth(GroundTile tile) => _selection.Width(tile) / _cells;

    /// <summary>
    /// The widest squares drawn <paramref name="away"/> from the eye (in the surface's units):
    /// a tile is split until it's SplitFactor of its widths away (or as narrow as tiles get),
    /// and its parent may stand in for it until it's built.
    /// </summary>
    public double WidestCellAt(double away) =>
        2 * Math.Max(away / SplitFactor, 2 * _selection.FinestWidth) / _cells;

    /// <summary>
    /// The height of the ground shown now (the tiles last joined) at a point on or over the
    /// surface, as the mesh's flat triangles lie there (unmorphed); null if no tile shown is
    /// under it. It stays as it is when other ground is shown, so it can be read on any thread.
    /// </summary>
    public Func<Vector3D, double?> ShownHeights()
    {
        Dictionary<GroundTile, TileMeshes> shown = _joinedTiles;
        int deepest = shown.Count == 0 ? 0 : shown.Keys.Max(tile => tile.Level);
        return point =>
        {
            if (Square(point, deepest, tile => shown.GetValueOrDefault(tile)) is not var
                (meshes, column, row, x, y))
            {
                return null;
            }

            double[] heights = meshes.Heights;
            double topLeft = heights[GroundTileGrid.Index(column, row, _cells)];
            double topRight = heights[GroundTileGrid.Index(column + 1, row, _cells)];
            double bottomLeft = heights[GroundTileGrid.Index(column, row + 1, _cells)];
            double bottomRight = heights[GroundTileGrid.Index(column + 1, row + 1, _cells)];

            // The square's two triangles meet along its diagonal from top left to bottom right.
            return x >= y
                ? topLeft + (topRight - topLeft) * x + (bottomRight - topRight) * y
                : topLeft + (bottomLeft - topLeft) * y + (bottomRight - bottomLeft) * x;
        };
    }

    public override void _ExitTree() => _stop.Cancel();

    // Chooses the tiles to draw, starts building those wanted, and lets go of the oldest
    // unused ones.
    private void Choose(Vector3D eye, Vector3D reachCenter, double reach,
        Func<GroundTile, TileExtent, GroundTileRecipe> recipeFor)
    {
        _frame++;
        var recipes = new Dictionary<GroundTile, GroundTileRecipe>();
        var extents = new Dictionary<GroundTile, TileExtent>();
        GroundTileChoice choice = _selection.Choose(eye, reachCenter, reach,
            tile => extents[tile] = ExtentOf(tile),
            tile => NeedOf(tile, extents[tile], recipes, recipeFor));
        StartBuilding(choice.Wanted, recipes);
        var drawn = new HashSet<GroundTile>(choice.Drawn);
        if (!drawn.SetEquals(_drawn))
        {
            _drawn = drawn;
            _deepest = drawn.Count == 0 ? 0 : drawn.Max(tile => tile.Level);
            _joinDue = true;
        }

        foreach (GroundTile tile in choice.Kept)
        {
            if (_built.TryGetValue(tile, out Built? built))
            {
                built.LastKept = _frame;
            }
        }

        LetGo(choice.Kept);
    }

    // The space a tile takes up: its own, if it's built; else a guess from the heights of the
    // nearest tile above it that is, over its patch (or the bare surface's, with none).
    private TileExtent ExtentOf(GroundTile tile)
    {
        if (_built.TryGetValue(tile, out Built? built))
        {
            return built.Meshes.Extent;
        }

        for (GroundTile above = tile; above.Level > 0;)
        {
            above = above.Parent();
            if (_built.TryGetValue(above, out Built? holding))
            {
                (double lowest, double highest) = HeightsOver(holding.Meshes, tile);
                return _selection.Estimate(tile, lowest, highest);
            }
        }

        return _selection.Estimate(tile, BaseHeight, BaseHeight);
    }

    // The lowest and highest of a built tile's heights at its grid points over a smaller tile
    // within it (and just round it): the mesh is flat between them.
    private (double Lowest, double Highest) HeightsOver(TileMeshes above, GroundTile tile)
    {
        (double u0, double v0) = tile.OnRoot(0, 0);
        (double u1, double v1) = tile.OnRoot(1, 1);
        (double left, double top) = above.Tile.FromRoot(u0, v0);
        (double right, double bottom) = above.Tile.FromRoot(u1, v1);
        int Floor(double share) => Math.Clamp((int)Math.Floor(share * _cells), 0, _cells);
        int Ceiling(double share) => Math.Clamp((int)Math.Ceiling(share * _cells), 0, _cells);
        double lowest = double.MaxValue, highest = double.MinValue;
        for (int row = Floor(top); row <= Ceiling(bottom); row++)
        {
            for (int column = Floor(left); column <= Ceiling(right); column++)
            {
                double height = above.Heights[GroundTileGrid.Index(column, row, _cells)];
                (lowest, highest) = (Math.Min(lowest, height), Math.Max(highest, height));
            }
        }

        return (lowest, highest);
    }

    // What a tile needs, from how it would be built now (noted for StartBuilding).
    private TileNeed NeedOf(GroundTile tile, TileExtent extent,
        Dictionary<GroundTile, GroundTileRecipe> recipes,
        Func<GroundTile, TileExtent, GroundTileRecipe> recipeFor)
    {
        bool built = _built.TryGetValue(tile, out Built? existing);
        GroundTileRecipe recipe = recipeFor(tile, extent);
        recipes[tile] = recipe;
        if (!built)
        {
            return TileNeed.Missing;
        }

        bool current = existing!.Meshes.Stamp == recipe.Stamp
            || (_building.TryGetValue(tile, out long stamp) && stamp == recipe.Stamp);
        return current ? TileNeed.None : TileNeed.Stale;
    }

    private double BaseHeight => _surface is GlobeTileSurface ? 1 : 0;

    // Starts building the wanted tiles, most needed first, as far as there are workers free.
    private void StartBuilding(IReadOnlyList<GroundTile> wanted,
        Dictionary<GroundTile, GroundTileRecipe> recipes)
    {
        foreach (GroundTile tile in wanted)
        {
            if (_building.Count >= _maxBuilding)
            {
                return;
            }

            GroundTileRecipe recipe = recipes[tile];
            if (_building.ContainsKey(tile)
                || (_failed.TryGetValue(tile, out long failed) && failed == recipe.Stamp))
            {
                continue;
            }

            _building[tile] = recipe.Stamp;
            CancellationToken stop = _stop.Token;
            Task.Run(() =>
            {
                if (stop.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    _finished.Enqueue(new Finished(tile, recipe.Stamp, Build(tile, recipe), null));
                }
                catch (Exception error)
                {
                    // Reported on the main thread, and not tried again with the same heights.
                    _finished.Enqueue(new Finished(tile, recipe.Stamp, null, error));
                }
            }, stop);
        }
    }

    // Takes in tiles finished on the workers. Returns how many there were.
    private int TakeInFinished()
    {
        int taken = 0;
        while (_finished.TryDequeue(out Finished? finished))
        {
            taken++;
            if (_building.TryGetValue(finished.Tile, out long stamp) && stamp == finished.Stamp)
            {
                _building.Remove(finished.Tile);
            }

            if (finished.Meshes is not TileMeshes meshes)
            {
                _failed[finished.Tile] = finished.Stamp;
                GD.PushError($"Couldn't build the ground tile {finished.Tile}: "
                    + finished.Error?.Message);
                continue;
            }

            _failed.Remove(finished.Tile);
            if (_built.TryGetValue(meshes.Tile, out Built? built))
            {
                built.Meshes = meshes;
            }
            else
            {
                _built[meshes.Tile] = new Built(meshes);
            }

            if (_drawn.Contains(meshes.Tile))
            {
                _joinDue = true;  // A drawn tile's shape changed
            }
        }

        return taken;
    }

    // Joins the tiles drawn into one mesh for the ground and one for the water, on a worker,
    // unless they're the ones shown already, joined around the same point.
    private void Join()
    {
        _joinDue = false;
        Vector3D origin = _origin ?? Vector3D.Zero;
        Dictionary<GroundTile, TileMeshes> drawn = _drawn.ToDictionary(tile => tile,
            tile => _built[tile].Meshes);
        if (origin == _joinedOrigin && drawn.Count == _joinedTiles.Count
            && drawn.All(entry => _joinedTiles.TryGetValue(entry.Key, out TileMeshes? shown)
                && ReferenceEquals(entry.Value, shown)))
        {
            return;
        }

        _joining = true;
        _sinceJoined.Restart();
        bool pack = PackedSurface.Works;
        CancellationToken stop = _stop.Token;
        Task.Run(() =>
        {
            if (stop.IsCancellationRequested)
            {
                return;
            }

            try
            {
                List<(TileMeshes Meshes, float MorphEnd)> tiles = [.. drawn.Values
                    .OrderBy(meshes => meshes.Extent.DistanceFrom(origin))
                    .Select(meshes => (meshes, (float)_selection.MorphRange(meshes.Tile).End))];
                _joined.Enqueue(new Joined(origin, drawn,
                    JoinMeshes(tiles, origin, water: false, pack),
                    JoinMeshes(tiles, origin, water: true, pack), null));
            }
            catch (Exception error)
            {
                _joined.Enqueue(new Joined(origin, drawn, null, null, error));
            }
        }, stop);
    }

    // Shows the meshes last joined, if they're ready: the ground and water together.
    private void TakeInJoined()
    {
        if (!_joined.TryDequeue(out Joined? joined))
        {
            return;
        }

        _joining = false;
        if (joined.Error is not null)
        {
            GD.PushError($"Couldn't join the ground tiles: {joined.Error.Message}");
            return;
        }

        Show(_ground, joined.Ground, joined.Origin, PrepareGround);
        Show(_water, joined.Water, joined.Origin, PrepareWater);
        _joinedOrigin = joined.Origin;
        _joinedTiles = joined.Tiles;
        ShownVersion++;
    }

    // A mesh instance for the ground or water, placed by Place.
    private static MeshInstance3D MakeInstance(string name) => new()
    {
        Name = name,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        TopLevel = true,
    };

    // Shows a joined mesh (or nothing, with none).
    private static void Show(MeshInstance3D instance, JoinedMesh? joined, Vector3D origin,
        Action<GeometryInstance3D, Vector3D>? prepare)
    {
        if (joined is null)
        {
            instance.Mesh = null;
            return;
        }

        if (joined.Packed is PackedSurface packed)
        {
            instance.Mesh = packed.ToMesh();
        }
        else
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = joined.Positions!;
            arrays[(int)Mesh.ArrayType.Custom0] = joined.Directions!;
            arrays[(int)Mesh.ArrayType.Custom1] = joined.Morphs!;
            arrays[(int)Mesh.ArrayType.Index] = joined.Indices!;
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays,
                flags: PackedSurface.Format);
            instance.Mesh = mesh;
        }

        prepare?.Invoke(instance, origin);
    }

    // The tiles' ground (or water) as one mesh around `origin` (on a worker thread): each
    // point's morph distance (where it has fully taken its parent's shape) in CUSTOM1's w;
    // packed as the engine keeps it, if `pack`.
    private JoinedMesh? JoinMeshes(List<(TileMeshes Meshes, float MorphEnd)> tiles,
        Vector3D origin, bool water, bool pack)
    {
        List<(TileMeshes Meshes, float MorphEnd)> parts = [.. tiles.Where(tile =>
            !water || tile.Meshes.WaterPositions is not null)];
        if (parts.Count == 0)
        {
            return null;
        }

        int perTile = GroundTileGrid.VertexCount(_cells);
        var positions = new Vector3[parts.Count * perTile];
        var directions = new float[positions.Length * 4];
        var morphs = new float[positions.Length * 4];
        var indices = new int[parts.Count * _triangles.Length];
        for (int part = 0; part < parts.Count; part++)
        {
            TileMeshes meshes = parts[part].Meshes;
            Vector3D anchor = water ? meshes.WaterAnchor!.Value : meshes.Anchor;
            Vector3 offset = ToGodot(anchor - origin);
            Vector3[] tilePositions = water ? meshes.WaterPositions! : meshes.Positions;
            int first = part * perTile;
            for (int i = 0; i < perTile; i++)
            {
                positions[first + i] = tilePositions[i] + offset;
            }

            Array.Copy(water ? meshes.WaterDirections! : meshes.Directions, 0, directions,
                first * 4, perTile * 4);
            float[] tileMorphs = water ? meshes.WaterMorphs! : meshes.Morphs;
            Array.Copy(tileMorphs, 0, morphs, first * 4, perTile * 4);
            for (int i = 0; i < perTile; i++)
            {
                morphs[(first + i) * 4 + 3] = parts[part].MorphEnd;
            }

            int firstIndex = part * _triangles.Length;
            for (int i = 0; i < _triangles.Length; i++)
            {
                indices[firstIndex + i] = first + _triangles[i];
            }
        }

        return pack
            ? new JoinedMesh(null, null, null, null,
                PackedSurface.Pack(positions, directions, morphs, indices))
            : new JoinedMesh(positions, directions, morphs, indices, null);
    }

    // Lets go of the tiles kept longest out of use, once there are more than the cache holds.
    private void LetGo(IReadOnlySet<GroundTile> kept)
    {
        if (_built.Count <= CacheTiles)
        {
            return;
        }

        foreach (GroundTile tile in _built
            .Where(entry => !kept.Contains(entry.Key))
            .OrderBy(entry => entry.Value.LastKept)
            .Take(_built.Count - CacheTiles)
            .Select(entry => entry.Key)
            .ToList())
        {
            _built.Remove(tile);
        }
    }

    // The tile a point is over, of those `meshesOf` gives meshes for (none at levels below
    // `deepest`), the square of its grid, and where in the square (0 to 1 across and down);
    // null if none is under it.
    private (TileMeshes Meshes, int Column, int Row, double X, double Y)? Square(Vector3D point,
        int deepest, Func<GroundTile, TileMeshes?> meshesOf)
    {
        if (_surface.Locate(point) is not var (root, u, v))
        {
            return null;
        }

        // The tile there, if any, from the finest it could be up.
        for (int level = deepest; level >= 0; level--)
        {
            double across = 1L << level;
            var tile = new GroundTile(root, level,
                (int)Math.Min(Math.Floor(u * across), across - 1),
                (int)Math.Min(Math.Floor(v * across), across - 1));
            if (meshesOf(tile) is not TileMeshes meshes)
            {
                continue;
            }

            (double x, double y) = tile.FromRoot(u, v);
            x *= _cells;
            y *= _cells;
            int column = Math.Min((int)x, _cells - 1), row = Math.Min((int)y, _cells - 1);
            return (meshes, column, row, x - column, y - row);
        }

        return null;
    }

    // Works out a tile's meshes (on a worker thread): its points at the recipe's heights, the
    // skirt under its edge, and where each point morphs to; and the water's surface over it,
    // unless it's all under the ground.
    private TileMeshes Build(GroundTile tile, GroundTileRecipe recipe)
    {
        int gridCount = GroundTileGrid.GridCount(_cells);
        var bases = new Vector3D[gridCount];
        var heights = new double[gridCount];
        for (int row = 0; row <= _cells; row++)
        {
            for (int column = 0; column <= _cells; column++)
            {
                int index = GroundTileGrid.Index(column, row, _cells);
                (double u, double v) = tile.OnRoot((double)column / _cells,
                    (double)row / _cells);
                bases[index] = _surface.BasePoint(tile.Root, u, v);
                heights[index] = recipe.Height(bases[index]);
            }
        }

        double width = _selection.Width(tile);
        Surface ground = Lay(bases, heights, width);

        // Each point morphs toward the parent's shape, at the heights the parent builds: only
        // its points (every other row and column) are needed.
        double[] parentHeights = heights;
        if (recipe.MorphHeight is { } parentHeight)
        {
            parentHeights = new double[gridCount];
            for (int row = 0; row <= _cells; row += 2)
            {
                for (int column = 0; column <= _cells; column += 2)
                {
                    int index = GroundTileGrid.Index(column, row, _cells);
                    parentHeights[index] = parentHeight(bases[index]);
                }
            }
        }

        var morphHeights = new double[gridCount];
        for (int row = 0; row <= _cells; row++)
        {
            for (int column = 0; column <= _cells; column++)
            {
                (int a, int b) = GroundTileGrid.MorphPair(column, row, _cells);
                morphHeights[GroundTileGrid.Index(column, row, _cells)] =
                    (parentHeights[a] + parentHeights[b]) / 2;
            }
        }

        Surface? water = null;
        if (recipe.Water is { } waterAt)
        {
            var waterHeights = new double[gridCount];
            bool any = false;
            for (int i = 0; i < gridCount; i++)
            {
                waterHeights[i] = waterAt(bases[i], heights[i], width / _cells);
                any |= waterHeights[i] > heights[i];
            }

            water = any ? Lay(bases, waterHeights, width) : null;
        }

        var points = new Vector3D[gridCount];
        for (int i = 0; i < gridCount; i++)
        {
            points[i] = _surface.Place(bases[i], heights[i]);
        }

        return new TileMeshes(tile, recipe.Stamp, ground.Anchor,
            TileExtent.Around(points, heights.Min(), heights.Max()), heights, morphHeights,
            ground.Positions, ground.Directions, ground.Morphs,
            water?.Anchor, water?.Positions, water?.Directions, water?.Morphs);
    }

    // A surface over a tile's base points at the heights given, with its skirt: positions
    // relative to its middle point, CUSTOM0, and CUSTOM1 (how far each point moves as it
    // morphs onto the parent tile's shape). It brings no normals: the material takes the way
    // up from CUSTOM0, which saves the engine packing them each time the tiles are joined.
    private Surface Lay(Vector3D[] bases, double[] heights, double width)
    {
        int gridCount = GroundTileGrid.GridCount(_cells);
        int count = GroundTileGrid.VertexCount(_cells);
        var placed = new Vector3D[count];
        var moves = new Vector3D[count];
        var basesAll = new Vector3D[count];
        for (int i = 0; i < gridCount; i++)
        {
            placed[i] = _surface.Place(bases[i], heights[i]);
            basesAll[i] = bases[i];
        }

        for (int row = 0; row <= _cells; row++)
        {
            for (int column = 0; column <= _cells; column++)
            {
                int index = GroundTileGrid.Index(column, row, _cells);
                (int a, int b) = GroundTileGrid.MorphPair(column, row, _cells);
                moves[index] = (placed[a] + placed[b]) * 0.5 - placed[index];
            }
        }

        // The skirt hangs under the edge and moves with it.
        (int Column, int Row)[] edge = GroundTileGrid.Edge(_cells);
        double depth = width * SkirtShare;
        for (int i = 0; i < edge.Length; i++)
        {
            int top = GroundTileGrid.Index(edge[i].Column, edge[i].Row, _cells);
            placed[gridCount + i] = _surface.Place(bases[top], heights[top] - depth);
            moves[gridCount + i] = moves[top];
            basesAll[gridCount + i] = bases[top];
        }

        Vector3D anchor = placed[GroundTileGrid.Index(_cells / 2, _cells / 2, _cells)];
        var positions = new Vector3[count];
        var directions = new float[count * 4];
        var morphs = new float[count * 4];
        bool flat = _surface is FlatTopTileSurface;
        for (int i = 0; i < count; i++)
        {
            positions[i] = ToGodot(placed[i] - anchor);

            // A globe's material reads each point's direction; a flat world's, its place.
            Vector3D custom = flat ? placed[i] : basesAll[i];
            (directions[i * 4], directions[i * 4 + 1], directions[i * 4 + 2]) =
                ((float)custom.X, (float)custom.Y, (float)custom.Z);
            directions[i * 4 + 3] = 1;
            (morphs[i * 4], morphs[i * 4 + 1], morphs[i * 4 + 2]) =
                ((float)moves[i].X, (float)moves[i].Y, (float)moves[i].Z);
        }

        return new Surface(anchor, positions, directions, morphs);
    }

    private static Vector3 ToGodot(Vector3D v) => new((float)v.X, (float)v.Y, (float)v.Z);

    private sealed record Surface(Vector3D Anchor, Vector3[] Positions, float[] Directions,
        float[] Morphs);

    // A tile's meshes as worked out on a worker, and what's needed of it after: the point its
    // positions are relative to, the space it takes up, and its points' heights, unmorphed
    // and fully morphed (grid points only).
    private sealed record TileMeshes(GroundTile Tile, long Stamp, Vector3D Anchor,
        TileExtent Extent, double[] Heights, double[] MorphHeights, Vector3[] Positions,
        float[] Directions, float[] Morphs, Vector3D? WaterAnchor,
        Vector3[]? WaterPositions, float[]? WaterDirections, float[]? WaterMorphs);

    private sealed record Finished(GroundTile Tile, long Stamp, TileMeshes? Meshes,
        Exception? Error);

    // A joined mesh, packed, or as arrays where the engine packs differently.
    private sealed record JoinedMesh(Vector3[]? Positions, float[]? Directions, float[]? Morphs,
        int[]? Indices, PackedSurface? Packed);

    // The tiles drawn, joined around a point: the ground and the water's surface.
    private sealed record Joined(Vector3D Origin, Dictionary<GroundTile, TileMeshes> Tiles,
        JoinedMesh? Ground, JoinedMesh? Water, Exception? Error);

    // A built tile, and when it was last among those chosen.
    private sealed class Built(TileMeshes meshes)
    {
        public TileMeshes Meshes { get; set; } = meshes;

        public long LastKept { get; set; }
    }
}
