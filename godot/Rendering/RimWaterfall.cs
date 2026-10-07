using Godot;
using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Rendering;

/// <summary>
/// The water pouring over a flat world's rim when its water reaches the edge (VISION.md BOD-10;
/// owner's choice: a waterfall over the edge), seen from orbit and from the ground. The rim is
/// the south pole all the way round, so it pours everywhere or nowhere. Lives under the body's
/// <see cref="PlanetSurface"/>, in the globe's own space (radii); rim_waterfall.gdshader shapes
/// and draws it.
/// </summary>
public partial class RimWaterfall : MeshInstance3D
{
    private const int Segments = 512;
    private const int Rows = 32;

    // How far the curtain reaches, in globe radii: out past the rim (as it's thrown) and down
    // (as it falls); rim_waterfall.gdshader's THROW and FALL, with some room.
    private const float Reach = 0.07f;
    private const float Fall = 0.85f;

    private static readonly Shader _shader =
        GD.Load<Shader>("res://Rendering/rim_waterfall.gdshader");

    private static ArrayMesh? _ring;
    private readonly ShaderMaterial _material = new() { Shader = _shader };

    /// <summary>Makes a waterfall, hidden until <see cref="Show"/> is called.</summary>
    public RimWaterfall()
    {
        Name = "RimWaterfall";
        Mesh = _ring ??= BuildRing();
        MaterialOverride = _material;
        CastShadow = ShadowCastingSetting.Off;
        Visible = false;
        float across = (float)FlatDisc.Radius + Reach;
        CustomAabb = new Aabb(new Vector3(-across, -Fall - 0.1f, -across),
            new Vector3(2 * across, Fall + 0.3f, 2 * across));
    }

    /// <summary>
    /// Pours the water over the edge from the rim's top edge, <paramref name="top"/> globe
    /// radii above the disc's middle, in <paramref name="water"/>'s color (a water terrain's
    /// at the rim) or the standard blue-green (null).
    /// </summary>
    public void Show(float top, Color? water)
    {
        _material.SetShaderParameter("top", top);
        _material.SetShaderParameter("tint", water is Color color
            ? new Color(color, 1)
            : new Color(0, 0, 0, 0));
        Visible = true;
    }

    // A ring of points round the rim's axis, row by row from the top (UV.y 0) to the bottom
    // (UV.y 1), with UV.x running once round; the shader places them.
    private static ArrayMesh BuildRing()
    {
        var points = new Vector3[(Segments + 1) * (Rows + 1)];
        var uvs = new Vector2[points.Length];
        for (int row = 0; row <= Rows; row++)
        {
            for (int segment = 0; segment <= Segments; segment++)
            {
                float angle = Mathf.Tau * segment / Segments;
                int at = row * (Segments + 1) + segment;
                points[at] = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                uvs[at] = new Vector2((float)segment / Segments, (float)row / Rows);
            }
        }

        var indices = new List<int>(Segments * Rows * 6);
        for (int row = 0; row < Rows; row++)
        {
            for (int segment = 0; segment < Segments; segment++)
            {
                int a = row * (Segments + 1) + segment, b = a + 1;
                int c = a + Segments + 1, d = c + 1;
                indices.AddRange([a, c, b, b, c, d]);
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = points;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
