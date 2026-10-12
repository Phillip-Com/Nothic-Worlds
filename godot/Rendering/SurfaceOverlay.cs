using Godot;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Rendering;

/// <summary>
/// The materials for lines and fills drawn on a globe's ground (roads, rivers, regions). They're
/// built on the ground itself and drawn over it, hills or not, hidden only past the horizon: the
/// same rule as the points clicked to draw them, so a finished line shows where it was clicked.
/// (Lifted off the ground to show over it, a line shifts on screen when seen from close by.)
/// Unshaded, so night doesn't hide them, and colored by their vertex colors, alpha too.
/// </summary>
public static class SurfaceOverlay
{
    /// <summary>The order overlays are drawn in, each over those before it.</summary>
    public enum Layer
    {
        /// <summary>A river's ribbon, its true width.</summary>
        RiverRibbon,

        /// <summary>A river's line, over its ribbon.</summary>
        RiverLine,

        /// <summary>Roads, over rivers.</summary>
        Road,

        /// <summary>A region's tint, over roads.</summary>
        RegionFill,

        /// <summary>A region's outline, over its tint.</summary>
        RegionOutline,
    }

    private static readonly Shader _shader =
        GD.Load<Shader>("res://Rendering/surface_overlay.gdshader");

    /// <summary>
    /// A material for overlays of a layer on a globe of a shape (a flat world's overlays are on
    /// its top face, which faces the camera only from above).
    /// </summary>
    public static ShaderMaterial Create(Layer layer, BodyShape shape)
    {
        var material = new ShaderMaterial { Shader = _shader, RenderPriority = (int)layer };
        material.SetShaderParameter("flat_world", shape == BodyShape.FlatDisc);
        return material;
    }
}
