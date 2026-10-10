using Godot;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Rendering;

/// <summary>
/// The shapes plants are drawn with while standing (VISION.md REN-06): a few low-poly models
/// for each <see cref="PlantModel"/> (CC0, by Quaternius; see <c>Assets/Plants/CREDITS.md</c>),
/// each merged into one mesh colored by its vertices, stood on its base and scaled to 1 tall,
/// so a plant's transform scales it to its height in meters; and the tuft short grass is
/// drawn with. Loaded when plants are first wanted while standing and let go on leaving.
/// </summary>
public sealed class PlantModels
{
    // Each plant's model files (in Assets/Plants, without ".obj").
    private static readonly Dictionary<PlantModel, string[]> _files = new()
    {
        [PlantModel.BroadleafTree] =
            ["CommonTree_1", "CommonTree_2", "CommonTree_3", "CommonTree_4"],
        [PlantModel.Birch] = ["BirchTree_1", "BirchTree_2", "BirchTree_3"],
        [PlantModel.Pine] = ["PineTree_1", "PineTree_2", "PineTree_3", "PineTree_4"],
        [PlantModel.Palm] = ["PalmTree_1", "PalmTree_2", "PalmTree_3"],
        [PlantModel.Willow] = ["Willow_1", "Willow_2", "Willow_3"],
        [PlantModel.Bush] = ["Bush_1", "Bush_2"],
        [PlantModel.BerryBush] = ["BushBerries_1", "BushBerries_2"],
        [PlantModel.LeafyPlant] = ["Plant_1", "Plant_2", "Plant_3", "Plant_4", "Plant_5"],
        [PlantModel.Flowers] = ["Flowers"],
        [PlantModel.TallGrass] = ["Grass", "Grass_2"],
        [PlantModel.Cactus] = ["Cactus_1", "Cactus_2", "Cactus_3", "Cactus_4"],
        [PlantModel.FloweringCactus] =
            ["CactusFlowers_2", "CactusFlowers_3", "CactusFlowers_4"],
        [PlantModel.Boulder] =
            ["Rock_1", "Rock_2", "Rock_3", "Rock_4", "Rock_5", "Rock_6", "Rock_7"],
        [PlantModel.MossyBoulder] = ["Rock_Moss_1", "Rock_Moss_2", "Rock_Moss_3", "Rock_Moss_4"],
        [PlantModel.Stump] = ["TreeStump", "TreeStump_Moss"],
        [PlantModel.Log] = ["WoodLog", "WoodLog_Moss"],
    };

    // How many blades make a tuft of short grass.
    private const int TuftBlades = 9;

    // A tree's simpler shape, drawn past a few tens of meters, keeps at most about this share
    // of its triangles: most trees in view are that far off, and the full shapes cost the
    // baseline laptop several milliseconds a frame in a forest.
    private const double SimpleShare = 0.2;

    private PlantModels(Dictionary<PlantModel, PlantShape[]> shapes, ArrayMesh tuft)
    {
        Shapes = shapes;
        Tuft = tuft;
    }

    /// <summary>Each plant's shapes, in a fixed order.</summary>
    public IReadOnlyDictionary<PlantModel, PlantShape[]> Shapes { get; }

    /// <summary>A tuft of short grass, 1 tall, its blades darker at the root.</summary>
    public ArrayMesh Tuft { get; }

    /// <summary>
    /// Whether a plant is a tree: drawn far off, as a flat picture past a distance
    /// (<see cref="PlantImpostors"/>).
    /// </summary>
    public static bool IsTree(PlantModel model) => model is PlantModel.BroadleafTree
        or PlantModel.Birch or PlantModel.Pine or PlantModel.Palm or PlantModel.Willow;

    /// <summary>Loads the models (about a tenth of a second).</summary>
    /// <exception cref="InvalidOperationException">A model is missing or empty.</exception>
    public static PlantModels Load()
    {
        var shapes = new Dictionary<PlantModel, PlantShape[]>();
        foreach (PlantModel model in Enum.GetValues<PlantModel>())
        {
            if (!_files.TryGetValue(model, out string[]? names))
            {
                throw new InvalidOperationException($"The plant {model} has no models.");
            }

            shapes[model] = names
                .Select(name => Merge(name, LoadMesh(name), PlantModels.IsTree(model)))
                .ToArray();
        }

        return new PlantModels(shapes, MakeTuft());
    }

    private static Mesh LoadMesh(string name)
    {
        string path = $"res://Assets/Plants/{name}.obj";
        return GD.Load<Mesh>(path)
            ?? throw new InvalidOperationException($"The plant model {path} is missing.");
    }

    // One mesh of all a model's surfaces, each colored by its material's color, its base
    // moved to 0 and its height scaled to 1, centered across; and, if `simple` is asked for,
    // a simpler shape of it.
    private static PlantShape Merge(string name, Mesh mesh, bool simple)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var colors = new List<Color>();
        var indices = new List<int>();
        for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++)
        {
            Godot.Collections.Array arrays = mesh.SurfaceGetArrays(surface);
            Vector3[] points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Vector3[] pointNormals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            int[] surfaceIndices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            Color color = mesh.SurfaceGetMaterial(surface) is BaseMaterial3D material
                ? material.AlbedoColor
                : Colors.White;
            int start = positions.Count;
            positions.AddRange(points);
            normals.AddRange(pointNormals.Length == points.Length
                ? pointNormals
                : Enumerable.Repeat(Vector3.Up, points.Length));
            colors.AddRange(Enumerable.Repeat(color, points.Length));
            indices.AddRange(surfaceIndices.Length > 0
                ? surfaceIndices.Select(index => start + index)
                : Enumerable.Range(start, points.Length));
        }

        if (positions.Count == 0)
        {
            throw new InvalidOperationException($"The plant model {name} is empty.");
        }

        float low = positions.Min(point => point.Y), high = positions.Max(point => point.Y);
        float height = Math.Max(high - low, 1e-4f);
        float middleX = (positions.Min(p => p.X) + positions.Max(p => p.X)) / 2;
        float middleZ = (positions.Min(p => p.Z) + positions.Max(p => p.Z)) / 2;
        Vector3[] placed = positions
            .Select(p => new Vector3(p.X - middleX, p.Y - low, p.Z - middleZ) / height)
            .ToArray();
        float halfWidth = placed.Max(p => Math.Max(Math.Abs(p.X), Math.Abs(p.Z)));

        var merged = new Godot.Collections.Array();
        merged.Resize((int)Mesh.ArrayType.Max);
        merged[(int)Mesh.ArrayType.Vertex] = placed;
        merged[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        merged[(int)Mesh.ArrayType.Color] = colors.ToArray();
        merged[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var result = new ArrayMesh();
        result.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, merged);
        return new PlantShape(result, simple ? Simplified(merged, indices.Count / 3) : null,
            halfWidth);
    }

    // The same shape with far fewer triangles (the engine's own mesh simplification, as it
    // makes levels of detail on import): the coarsest level keeping no more than SimpleShare
    // of the `triangles`, or the coarsest there is. Null if none can be made.
    private static ArrayMesh? Simplified(Godot.Collections.Array arrays, int triangles)
    {
        var importer = new ImporterMesh();
        importer.AddSurface(Mesh.PrimitiveType.Triangles, arrays);
        importer.GenerateLods(60, 60, []);
        int[]? chosen = null;
        for (int lod = 0; lod < importer.GetSurfaceLodCount(0); lod++)
        {
            chosen = importer.GetSurfaceLodIndices(0, lod);
            if (chosen.Length / 3 <= triangles * SimpleShare)
            {
                break;
            }
        }

        if (chosen is not { Length: > 0 })
        {
            return null;
        }

        var simple = (Godot.Collections.Array)arrays.Duplicate();
        simple[(int)Mesh.ArrayType.Index] = chosen;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, simple);
        return mesh;
    }

    // A tuft of thin blades leaning out from its middle, each one triangle, their normals
    // pointing up (so they light like the ground under them) and darker at the root.
    private static ArrayMesh MakeTuft()
    {
        var random = new RandomNumberGenerator { Seed = 17 };
        var positions = new List<Vector3>();
        var colors = new List<Color>();
        for (int blade = 0; blade < TuftBlades; blade++)
        {
            float angle = blade * Mathf.Tau / TuftBlades + random.RandfRange(-0.3f, 0.3f);
            var outward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var side = new Vector3(-outward.Z, 0, outward.X);
            Vector3 root = outward * random.RandfRange(0, 0.12f);
            float tall = random.RandfRange(0.6f, 1f);
            Vector3 tip = root + outward * random.RandfRange(0.1f, 0.3f) + Vector3.Up * tall;
            float width = 0.025f;
            positions.AddRange([root - side * width, root + side * width, tip]);
            colors.AddRange([new Color(0.55f, 0.55f, 0.55f), new Color(0.55f, 0.55f, 0.55f),
                Colors.White]);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = positions.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = Enumerable.Repeat(Vector3.Up, positions.Count)
            .ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        var tuft = new ArrayMesh();
        tuft.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return tuft;
    }
}
