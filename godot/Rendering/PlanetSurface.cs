using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Maps;

namespace NothicWorlds.Rendering;

/// <summary>
/// Controls what's drawn on the planet's surface through <c>planet.gdshader</c>: an imported
/// map (VISION.md MAP-01), how it wraps onto the globe (MAP-03, MAP-04), and the
/// latitude/longitude grid.
/// The grid shows by default, hides when a map is applied, and G toggles it.
/// </summary>
public partial class PlanetSurface : MeshInstance3D
{
    // Samples per calibration lookup table: about 0.09° of latitude and 0.18° of longitude
    // apart, blended smoothly by the GPU in between.
    private const int TableSamples = 2048;

    private ShaderMaterial _material = null!;
    private ImageTexture? _latitudeTable;
    private ImageTexture? _longitudeTable;

    /// <summary>True if a map image is currently applied.</summary>
    public bool HasMap { get; private set; }

    /// <summary>The map image currently wrapped onto the planet, or null.</summary>
    public Texture2D? MapTexture { get; private set; }

    /// <summary>Whether the latitude/longitude grid is drawn.</summary>
    public bool ShowGrid
    {
        get => (bool)GetParameter("show_grid");
        set => _material.SetShaderParameter("show_grid", value);
    }

    /// <summary>
    /// How the map image wraps onto the globe (VISION.md MAP-03). Changing it takes effect
    /// immediately, with no reload.
    /// </summary>
    public MapProjection Projection
    {
        get => (MapProjection)(int)GetParameter("projection");
        set => _material.SetShaderParameter("projection", (int)value);
    }

    /// <summary>
    /// Color used where the map doesn't cover the globe, e.g. a flat map's polar caps or a polar
    /// map's southern hemisphere (VISION.md MAP-03, MAP-04). Map types that cover the whole
    /// globe ignore it.
    /// </summary>
    public Color FillColor
    {
        get => (Color)GetParameter("fill_color");
        set => _material.SetShaderParameter("fill_color", value);
    }

    public override void _Ready()
    {
        if (GetActiveMaterial(0) is not ShaderMaterial material)
        {
            GD.PushError("PlanetSurface needs a ShaderMaterial using planet.gdshader.");
            SetProcessUnhandledInput(false);
            return;
        }

        _material = material;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActions.ToggleGrid))
        {
            ShowGrid = !ShowGrid;
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>
    /// Applies a grid calibration (VISION.md MAP-05), or removes it with null. Cheap enough to
    /// call on every mouse movement while dragging a guide line.
    /// </summary>
    public void SetCalibration(MapCalibration? calibration)
    {
        if (calibration is null)
        {
            _material.SetShaderParameter("has_calibration", false);
            return;
        }

        _latitudeTable = UpdateTable(_latitudeTable, calibration.BakeLatitudeTable(TableSamples));
        _longitudeTable =
            UpdateTable(_longitudeTable, calibration.BakeLongitudeTable(TableSamples));
        _material.SetShaderParameter("calibration_latitudes", _latitudeTable);
        _material.SetShaderParameter("calibration_longitudes", _longitudeTable);
        _material.SetShaderParameter("has_calibration", true);
    }

    /// <summary>Wraps a map texture onto the planet and hides the grid.</summary>
    public void SetMap(Texture2D texture)
    {
        _material.SetShaderParameter("surface_map", texture);
        _material.SetShaderParameter("map_aspect", (float)texture.GetWidth() / texture.GetHeight());
        _material.SetShaderParameter("has_map", true);
        MapTexture = texture;
        HasMap = true;
        ShowGrid = false;
    }

    /// <summary>Removes the map, freeing its memory, and shows the grid again.</summary>
    public void ClearMap()
    {
        _material.SetShaderParameter("surface_map", default);
        _material.SetShaderParameter("has_map", false);
        SetCalibration(null);
        MapTexture = null;
        HasMap = false;
        ShowGrid = true;
    }

    // Writes a table of 32-bit floats into a one-pixel-tall texture, reusing the existing
    // texture when there is one (much cheaper while dragging).
    private static ImageTexture UpdateTable(ImageTexture? texture, float[] values)
    {
        byte[] bytes = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        Image image = Image.CreateFromData(values.Length, 1, false, Image.Format.Rf, bytes);
        if (texture is null)
        {
            return ImageTexture.CreateFromImage(image);
        }

        texture.Update(image);
        return texture;
    }

    // A material only stores parameters that have been set. Until then, the shader uses the
    // default written in planet.gdshader, but the material reports "nothing" (which would read
    // as false, 0, or black). Fall back to the shader's default so reads match what's drawn.
    private Variant GetParameter(string name)
    {
        Variant value = _material.GetShaderParameter(name);
        return value.VariantType != Variant.Type.Nil
            ? value
            : RenderingServer.ShaderGetParameterDefault(_material.Shader.GetRid(), name);
    }
}
