using Godot;

namespace NothicWorlds.Rendering;

/// <summary>
/// The look of being under water in the first-person view (VISION.md BOD-09): a layer over
/// the whole screen that tints the scene blue-green, makes it waver, and plays ripples of
/// light over it. It sits under the view's readouts, which stay sharp.
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
}
