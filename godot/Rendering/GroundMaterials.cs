using Godot;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Rendering;

/// <summary>
/// The photo materials of the ground up close while standing (VISION.md REN-06): a color and a
/// normal map for each <see cref="GroundKind"/> (CC0, from ambientCG and Poly Haven; see
/// <c>Assets/Ground/CREDITS.md</c>), loaded into two texture arrays, one layer per kind in the
/// enum's order, which planet_surface.gdshaderinc picks from per pixel. Made when the standing
/// view opens and let go when it closes, so the globe views never hold them.
/// </summary>
public sealed class GroundMaterials
{
    // The texture files' names, in GroundKind order.
    private static readonly string[] _fileNames =
        ["grass", "dry_grass", "forest_floor", "sand", "mud", "rock", "snow", "gravel"];

    // How many pixels across and down each color map's average is taken from.
    private const int AverageSamples = 32;

    // The highest snow reaches down to at the equator on a world as warm as Earth (15 °C on
    // average), in meters, and how far it moves for each degree warmer or colder: about the
    // fall in temperature with height (6.5 °C a km).
    private const double EquatorSnowLineMeters = 5_000;
    private const double SnowLineMetersPerDegree = 150;
    private const double EarthAverageC = 15;

    private readonly Texture2DArray _colors;
    private readonly Texture2DArray _normals;
    private readonly Vector3[] _averages;
    private PlanetSurface? _shownOn;

    private GroundMaterials(Texture2DArray colors, Texture2DArray normals, Vector3[] averages)
    {
        _colors = colors;
        _normals = normals;
        _averages = averages;
    }

    /// <summary>
    /// Loads the materials (a few milliseconds: the textures are already compressed and
    /// mipmapped by the import).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A texture is missing or doesn't match.
    /// </exception>
    public static GroundMaterials Load()
    {
        if (_fileNames.Length != Enum.GetValues<GroundKind>().Length)
        {
            throw new InvalidOperationException("Every ground kind needs its textures.");
        }

        var colors = new Godot.Collections.Array<Image>();
        var normals = new Godot.Collections.Array<Image>();
        var averages = new Vector3[_fileNames.Length];
        for (int kind = 0; kind < _fileNames.Length; kind++)
        {
            Image color = LoadImage($"{_fileNames[kind]}_color");
            averages[kind] = Average(color);
            colors.Add(color);
            normals.Add(LoadImage($"{_fileNames[kind]}_normal"));
        }

        var colorArray = new Texture2DArray();
        var normalArray = new Texture2DArray();
        if (colorArray.CreateFromImages(colors) != Error.Ok
            || normalArray.CreateFromImages(normals) != Error.Ok)
        {
            throw new InvalidOperationException(
                "The ground textures don't all share one size and format.");
        }

        return new GroundMaterials(colorArray, normalArray, averages);
    }

    /// <summary>
    /// Shows the materials on <paramref name="globe"/>'s ground around the eye, with
    /// <paramref name="body"/>'s snow line, taking them off any other globe they were on.
    /// Cheap to call every frame: it does nothing when nothing has changed.
    /// </summary>
    public void ShowOn(PlanetSurface globe, Body body)
    {
        ShaderMaterial material = globe.Material;
        if (!ReferenceEquals(globe, _shownOn))
        {
            Hide();
            material.SetShaderParameter("ground_colors", _colors);
            material.SetShaderParameter("ground_normals", _normals);
            material.SetShaderParameter("ground_averages", _averages);
            globe.ShowsGroundMaterials = true;
            _shownOn = globe;
        }

        // No air, no snow; otherwise the line sits lower on colder worlds.
        material.SetShaderParameter("has_snow_line", body.HasAtmosphere);
        material.SetShaderParameter("snow_line_meters", (float)(EquatorSnowLineMeters
            + (body.AverageTemperatureC - EarthAverageC) * SnowLineMetersPerDegree));
    }

    /// <summary>
    /// Takes the materials off the globe they were shown on, if any (and if it's still there).
    /// </summary>
    public void Hide()
    {
        if (_shownOn is null)
        {
            return;
        }

        if (GodotObject.IsInstanceValid(_shownOn))
        {
            _shownOn.ShowsGroundMaterials = false;
            _shownOn.Material.SetShaderParameter("ground_colors", default(Variant));
            _shownOn.Material.SetShaderParameter("ground_normals", default(Variant));
        }

        _shownOn = null;
    }

    private static Image LoadImage(string name)
    {
        string path = $"res://Assets/Ground/{name}.jpg";
        Texture2D texture = GD.Load<Texture2D>(path)
            ?? throw new InvalidOperationException($"The ground texture {path} is missing.");
        return texture.GetImage();
    }

    // The color map's average color in linear light (as the shader sees it), from a grid of
    // its pixels: what the material looks like from far enough away that its detail is gone.
    private static Vector3 Average(Image compressed)
    {
        Image image = (Image)compressed.Duplicate();
        if (image.IsCompressed())
        {
            image.Decompress();
        }

        Vector3 total = Vector3.Zero;
        for (int row = 0; row < AverageSamples; row++)
        {
            for (int column = 0; column < AverageSamples; column++)
            {
                Color pixel = image.GetPixel(
                    (column * 2 + 1) * image.GetWidth() / (AverageSamples * 2),
                    (row * 2 + 1) * image.GetHeight() / (AverageSamples * 2)).SrgbToLinear();
                total += new Vector3(pixel.R, pixel.G, pixel.B);
            }
        }

        return total / (AverageSamples * AverageSamples);
    }
}
