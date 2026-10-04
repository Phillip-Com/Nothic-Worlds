using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.Rendering;

/// <summary>
/// Puts the world's nebulas on the sky (VISION.md BOD-03; owner's choice: a backdrop, the same
/// from every planet). Core paints the sky once, in the background, whenever the nebulas change;
/// drawing it then costs one texture lookup per background pixel. Without nebulas the
/// background stays the plain color it has always been.
/// </summary>
public partial class NebulaBackdrop : Node
{
    // Nebulas are soft clouds, so this is sharp enough, and quick to paint.
    private const int SkyWidth = 1024;
    private const int SkyHeight = 512;

    private readonly ShaderMaterial _material = new()
    {
        Shader = GD.Load<Shader>("res://Rendering/nebula_sky.gdshader"),
    };

    // What the sky shows, what's being painted, and whether it needs painting again afterwards.
    private IReadOnlyList<Nebula> _shown = [];
    private bool _painting;
    private bool _stale;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The scene's environment, whose background shows the nebulas.</summary>
    [Export] public WorldEnvironment? Environment { get; set; }

    public override void _Ready()
    {
        if (Session is null || Environment?.Environment is not Godot.Environment environment)
        {
            GD.PushError("NebulaBackdrop needs a world session and an environment.");
            return;
        }

        environment.Sky = new Sky
        {
            SkyMaterial = _material,
            RadianceSize = Sky.RadianceSizeEnum.Size32,
        };

        // The sky is only a backdrop: lighting and reflections stay as they were.
        environment.ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled;
        Session.Changed += Update;
        Update();
    }

    public override void _ExitTree()
    {
        if (Session is not null)
        {
            Session.Changed -= Update;
        }
    }

    // Paints the sky again if the nebulas changed (one painting at a time; a change made
    // meanwhile is painted next).
    private async void Update()
    {
        if (Session is null || Environment?.Environment is not Godot.Environment environment)
        {
            return;
        }

        List<Nebula> nebulas = [.. Session.World.Nebulas];
        if (nebulas.SequenceEqual(_shown))
        {
            return;
        }

        if (_painting)
        {
            _stale = true;
            return;
        }

        _painting = true;
        Color background = environment.BackgroundColor;
        var backgroundColor = new RgbColor((byte)background.R8, (byte)background.G8,
            (byte)background.B8);
        float[] pixels = nebulas.Count == 0
            ? []
            : await Task.Run(() =>
                NebulaSky.Paint(nebulas, SkyWidth, SkyHeight, backgroundColor));
        _painting = false;
        _shown = nebulas;
        Show(environment, pixels);
        if (_stale)
        {
            _stale = false;
            Update();
        }
    }

    private void Show(Godot.Environment environment, float[] pixels)
    {
        if (pixels.Length == 0)
        {
            environment.BackgroundMode = Godot.Environment.BGMode.Color;
            return;
        }

        var bytes = new byte[pixels.Length * sizeof(float)];
        Buffer.BlockCopy(pixels, 0, bytes, 0, bytes.Length);
        Image image = Image.CreateFromData(SkyWidth, SkyHeight, false, Image.Format.Rgbf, bytes);
        _material.SetShaderParameter("nebula_sky", ImageTexture.CreateFromImage(image));
        environment.BackgroundMode = Godot.Environment.BGMode.Sky;
    }
}
