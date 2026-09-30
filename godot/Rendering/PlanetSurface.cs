using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Maps;

namespace NothicWorlds.Rendering;

/// <summary>
/// Controls what's drawn on the planet's surface through <c>planet.gdshader</c>: an imported
/// map (VISION.md MAP-01), how it wraps onto the globe (MAP-03), and the latitude/longitude grid.
/// The grid shows by default, hides when a map is applied, and G toggles it.
/// </summary>
public partial class PlanetSurface : MeshInstance3D
{
    private ShaderMaterial _material = null!;

    /// <summary>True if a map image is currently applied.</summary>
    public bool HasMap { get; private set; }

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
    /// Color of the polar caps beyond a flat map's coverage (VISION.md MAP-03). It has no effect
    /// on globe maps, which reach the poles.
    /// </summary>
    public Color PoleColor
    {
        get => (Color)GetParameter("pole_color");
        set => _material.SetShaderParameter("pole_color", value);
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

    /// <summary>Wraps a map texture onto the planet and hides the grid.</summary>
    public void SetMap(Texture2D texture)
    {
        _material.SetShaderParameter("surface_map", texture);
        _material.SetShaderParameter("map_aspect", (float)texture.GetWidth() / texture.GetHeight());
        _material.SetShaderParameter("has_map", true);
        HasMap = true;
        ShowGrid = false;
    }

    /// <summary>Removes the map, freeing its memory, and shows the grid again.</summary>
    public void ClearMap()
    {
        _material.SetShaderParameter("surface_map", default);
        _material.SetShaderParameter("has_map", false);
        HasMap = false;
        ShowGrid = true;
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
