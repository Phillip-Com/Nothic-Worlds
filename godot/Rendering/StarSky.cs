using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.Rendering;

/// <summary>
/// The open world's designed night sky (VISION.md REN-07) as images for the sky shaders
/// (star_sky.gdshaderinc): its stars, one texel per cell of <see cref="StarField"/>, and its
/// constellation lines. Core makes them in the background whenever the seed or the
/// constellations change; drawing them then costs a texture lookup or two per sky pixel.
/// </summary>
public partial class StarSky : Node
{
    // What the images show, whether they're being made, and whether to make them again after.
    private int? _shownSeed;
    private IReadOnlyList<Constellation> _shownConstellations = [];
    private bool _making;
    private bool _stale;

    /// <summary>Raised when new images are ready.</summary>
    public event Action? Changed;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The stars, laid out as star_sky.gdshaderinc reads them; null until made.</summary>
    public Texture2D? Stars { get; private set; }

    /// <summary>The constellation lines, laid out likewise; null until made.</summary>
    public Texture2D? Lines { get; private set; }

    public override void _Ready()
    {
        if (Session is null)
        {
            GD.PushError("StarSky needs a world session.");
            return;
        }

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

    /// <summary>Gives a sky material the images and whether to draw constellation lines.</summary>
    public void ApplyTo(ShaderMaterial material, bool showLines)
    {
        material.SetShaderParameter("has_star_field", Stars is not null);
        material.SetShaderParameter("show_constellations", showLines && Lines is not null);
        if (Stars is not null && Lines is not null)
        {
            material.SetShaderParameter("star_cells", Stars);
            material.SetShaderParameter("star_lines", Lines);
        }
    }

    // Makes the images again if the sky changed (one at a time; a change made meanwhile is
    // made next).
    private async void Update()
    {
        if (Session is null)
        {
            return;
        }

        int seed = Session.World.StarSeed;
        List<Constellation> constellations = [.. Session.World.Constellations];
        bool newStars = seed != _shownSeed;
        if (!newStars && constellations.SequenceEqual(_shownConstellations))
        {
            return;
        }

        if (_making)
        {
            _stale = true;
            return;
        }

        _making = true;
        (byte[]? stars, byte[] lines) = await Task.Run(() => (
            newStars ? StarField.CellImage(seed) : null,
            ConstellationLines.Paint(seed, constellations)));
        _making = false;
        _shownSeed = seed;
        _shownConstellations = constellations;
        if (stars is not null)
        {
            Stars = ImageTexture.CreateFromImage(Image.CreateFromData(6 * StarField.FaceCells,
                StarField.FaceCells, false, Image.Format.Rgba8, stars));
        }

        int size = ConstellationLines.FaceSize;
        Lines = ImageTexture.CreateFromImage(
            Image.CreateFromData(6 * size, size, false, Image.Format.R8, lines));
        Changed?.Invoke();
        if (_stale)
        {
            _stale = false;
            Update();
        }
    }
}
