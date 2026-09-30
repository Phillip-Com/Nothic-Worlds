using Godot;
using NothicWorlds.Controls;

namespace NothicWorlds.Rendering;

/// <summary>
/// Controls what's drawn on the planet's surface through <c>planet.gdshader</c>: an imported
/// map (VISION.md MAP-01) and/or the latitude/longitude grid. The grid shows by default, hides
/// when a map is applied, and G toggles it.
/// </summary>
public partial class PlanetSurface : MeshInstance3D
{
    private ShaderMaterial _material = null!;

    /// <summary>True if a map image is currently applied.</summary>
    public bool HasMap { get; private set; }

    /// <summary>Whether the latitude/longitude grid is drawn.</summary>
    public bool ShowGrid
    {
        get => (bool)_material.GetShaderParameter("show_grid");
        set => _material.SetShaderParameter("show_grid", value);
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
}
