using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.Rendering;

/// <summary>
/// Puts the world's nebulas on the sky (VISION.md BOD-03; owner's choice: a backdrop, the same
/// from every planet), and, when chosen, its designed stars and constellations (REN-07; owner's
/// choice: optional from orbit). Core paints the nebulas once, in the background, whenever they
/// change; drawing them then costs one texture lookup per background pixel. Without nebulas or
/// stars the background stays the plain color it has always been.
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

    private bool _showStars;
    private bool _showConstellations = true;
    private bool _hasNebulas;

    // What the sky shows, what's being painted, and whether it needs painting again afterwards.
    private IReadOnlyList<Nebula> _shown = [];
    private bool _painting;
    private bool _stale;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The scene's environment, whose background shows the nebulas.</summary>
    [Export] public WorldEnvironment? Environment { get; set; }

    /// <summary>The world's designed stars and constellations.</summary>
    [Export] public StarSky? Stars { get; set; }

    /// <summary>
    /// Whether the designed stars show behind the system (View ▸ Stars from Orbit).
    /// </summary>
    public bool ShowStars
    {
        get => _showStars;
        set
        {
            _showStars = value;
            Refresh();
        }
    }

    /// <summary>
    /// Whether constellation lines are drawn with the stars, here and from a world's surface
    /// (View ▸ Constellation Lines).
    /// </summary>
    public bool ShowConstellations
    {
        get => _showConstellations;
        set
        {
            _showConstellations = value;
            Refresh();
        }
    }

    /// <summary>
    /// The painted sky with the nebulas (laid out as nebula_sky.gdshader reads it), or null when
    /// there are none; for the sky seen from a world's surface (VISION.md REN-06).
    /// </summary>
    public Texture2D? SkyTexture { get; private set; }

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
        if (Stars is not null)
        {
            Stars.Changed += Refresh;
        }

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
        _hasNebulas = pixels.Length > 0;
        if (_hasNebulas)
        {
            var bytes = new byte[pixels.Length * sizeof(float)];
            Buffer.BlockCopy(pixels, 0, bytes, 0, bytes.Length);
            Image image =
                Image.CreateFromData(SkyWidth, SkyHeight, false, Image.Format.Rgbf, bytes);
            SkyTexture = ImageTexture.CreateFromImage(image);
            _material.SetShaderParameter("nebula_sky", SkyTexture);
        }
        else
        {
            SkyTexture = null;
        }

        Refresh();
    }

    // The plain background when there's nothing on the sky; the sky shader otherwise.
    private void Refresh()
    {
        if (Environment?.Environment is not Godot.Environment environment)
        {
            return;
        }

        bool stars = _showStars && Stars?.Stars is not null;
        _material.SetShaderParameter("has_nebulas", _hasNebulas);
        _material.SetShaderParameter("background_color", environment.BackgroundColor);
        if (Stars is not null)
        {
            Stars.ApplyTo(_material, _showConstellations);
        }

        _material.SetShaderParameter("has_star_field", stars);
        environment.BackgroundMode = _hasNebulas || stars
            ? Godot.Environment.BGMode.Sky
            : Godot.Environment.BGMode.Color;
    }
}
