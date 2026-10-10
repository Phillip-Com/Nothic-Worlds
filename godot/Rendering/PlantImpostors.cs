using Godot;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Rendering;

/// <summary>
/// Flat pictures of the trees (VISION.md REN-06), drawn in their place past a distance while
/// standing: each tree shape in <see cref="PlantModels"/> is drawn once from the side, in its
/// flat colors, into one sheet of pictures (a cell each), a frame or two after this is added
/// to the scene. Nothing is kept on disk, so a model swapped in gets its picture too.
/// </summary>
public partial class PlantImpostors : SubViewport
{
    /// <summary>How many cells a row of the sheet has.</summary>
    public const int Columns = 5;

    // Pixels across (and down) a cell.
    private const int CellPixels = 256;

    private static readonly Shader _flatShader = new()
    {
        Code = """
            shader_type spatial;
            render_mode unshaded, cull_disabled;
            void fragment() { ALBEDO = COLOR.rgb; }
            """,
    };

    private readonly Dictionary<(PlantModel Model, int Shape), (int Cell, float Scale)> _cells
        = [];

    private int _framesWaited;

    /// <summary>Lays out a picture of every tree shape in <paramref name="models"/>.</summary>
    public PlantImpostors(PlantModels models)
    {
        Name = "PlantImpostors";
        OwnWorld3D = true;
        TransparentBg = true;
        RenderTargetUpdateMode = UpdateMode.Once;
        Msaa3D = Msaa.Disabled;

        var trees = models.Shapes
            .Where(pair => PlantModels.IsTree(pair.Key))
            .SelectMany(pair => pair.Value.Select((shape, index) => (pair.Key, index, shape)))
            .ToList();
        Rows = Math.Max(1, (trees.Count + Columns - 1) / Columns);
        Size = new Vector2I(Columns * CellPixels, Rows * CellPixels);

        // Each cell is a unit square, its tree standing on its bottom edge, shrunk to fit if
        // it's wider than tall: the billboard grows by as much (Scale) to stand as tall.
        var flat = new ShaderMaterial { Shader = _flatShader };
        for (int cell = 0; cell < trees.Count; cell++)
        {
            (PlantModel model, int index, PlantShape shape) = trees[cell];
            float fit = Math.Min(1f, 0.98f / (2 * shape.HalfWidth));
            int column = cell % Columns, row = cell / Columns;
            AddChild(new MeshInstance3D
            {
                Mesh = shape.Mesh,
                MaterialOverride = flat,
                Position = new Vector3(column + 0.5f, Rows - row - 1, 0),
                Scale = Vector3.One * fit,
            });
            _cells[(model, index)] = (cell, 1 / fit);
        }

        AddChild(new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            KeepAspect = Camera3D.KeepAspectEnum.Height,
            Size = Rows,
            Position = new Vector3(Columns / 2f, Rows / 2f, 50),
            Near = 1,
            Far = 100,
        });
    }

    /// <summary>Required by Godot; the pictures are made with the other constructor.</summary>
    public PlantImpostors()
    {
    }

    /// <summary>How many rows of cells the sheet has.</summary>
    public int Rows { get; }

    /// <summary>
    /// The sheet of pictures, mipmapped, once drawn; null until then (or if it can't be, as
    /// when run without a screen).
    /// </summary>
    public ImageTexture? Sheet { get; private set; }

    /// <summary>
    /// A tree shape's cell in the sheet, and how much bigger than the tree its square is.
    /// </summary>
    public (int Cell, float Scale) CellOf(PlantModel model, int shape) => _cells[(model, shape)];

    public override void _Process(double delta)
    {
        // Drawn once, at the end of the frame it was added in; read back after it.
        if (Sheet is not null || ++_framesWaited < 3)
        {
            return;
        }

        Image? image = GetTexture()?.GetImage();
        if (image is null || image.IsEmpty())
        {
            SetProcess(false);
            return;
        }

        image.GenerateMipmaps();
        Sheet = ImageTexture.CreateFromImage(image);
        SetProcess(false);
    }
}
