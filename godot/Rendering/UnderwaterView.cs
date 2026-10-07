using Godot;

namespace NothicWorlds.Rendering;

/// <summary>
/// The look of being under water in the first-person view (VISION.md BOD-09): a layer over
/// the whole screen that tints the scene the water's color and makes it waver. Show it by
/// making it visible. It sits under the view's readouts, which stay sharp.
/// </summary>
public partial class UnderwaterView : CanvasLayer
{
    private static readonly Shader _shader =
        GD.Load<Shader>("res://Rendering/underwater.gdshader");

    private readonly ShaderMaterial _material = new() { Shader = _shader };

    public UnderwaterView()
    {
        Name = "UnderwaterView";
        Layer = 4;  // Under FirstPersonHud's 5
        Visible = false;
        var cover = new ColorRect
        {
            Material = _material,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        cover.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(cover);
    }

    /// <summary>
    /// Tints the view toward <paramref name="water"/> (a water terrain's color), or the
    /// standard blue-green: its hue at full strength, kept light so the scene shows through.
    /// </summary>
    public void SetWaterColor(Color? water)
    {
        Color color = water ?? SurfaceSky.StandardWater;
        float brightest = Math.Max(Math.Max(color.R, color.G), Math.Max(color.B, 0.01f));
        Color hue = new(color.R / brightest, color.G / brightest, color.B / brightest);
        _material.SetShaderParameter("tint", Colors.White.Lerp(hue, 0.75f));
    }
}
