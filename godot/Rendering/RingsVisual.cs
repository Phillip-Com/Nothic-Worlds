using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Interop;

namespace NothicWorlds.Rendering;

/// <summary>
/// One planet's rings (VISION.md BOD-03), drawn by <c>rings.gdshader</c> on a shared flat ring
/// in the planet's equatorial plane. A child of the planet's node, so it turns and tilts with it
/// and is sized in planet radii.
/// </summary>
public sealed class RingsVisual
{
    private const int Segments = 256;

    private static readonly Shader _shader = GD.Load<Shader>("res://Rendering/rings.gdshader");

    // A ring from radius 1 to 2 (the shader stretches it to each planet's radii).
    private static ArrayMesh? _mesh;

    // A flat world's spin axis lies across its disc (its +X, see SystemView), so its rings, which
    // circle the spin axis, are turned to face that way; and they're measured in the disc's
    // radius (π times its globe's), so they clear its rim.
    private static readonly Basis _flatWorldTurn =
        new Basis(Vector3.Back, Mathf.Pi / 2).Scaled(Vector3.One * (float)FlatDisc.Radius);

    private readonly ShaderMaterial _material = new() { Shader = _shader };
    private readonly MeshInstance3D _node;
    private (PlanetRings Rings, Guid BodyId, BodyShape Shape)? _shown;

    /// <summary>Creates the rings as a child of the planet's node.</summary>
    public RingsVisual(Node3D planet)
    {
        _node = new MeshInstance3D
        {
            Name = "Rings",
            Mesh = _mesh ??= BuildMesh(),
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        planet.AddChild(_node);
    }

    /// <summary>The normal of the plane the rings lie in, in the planet's own space.</summary>
    public Vector3 Normal => (_node.Basis * Vector3.Up).Normalized();

    /// <summary>The seed of the rings' bands (each planet has its own).</summary>
    public float Seed { get; private set; }

    /// <summary>
    /// Shows these rings, for this body and shape. Returns true if anything changed (so the
    /// planet's shadow of them needs updating too).
    /// </summary>
    public bool Show(PlanetRings rings, Guid bodyId, BodyShape shape)
    {
        if (_shown == (rings, bodyId, shape))
        {
            return false;
        }

        _shown = (rings, bodyId, shape);
        Seed = bodyId.ToByteArray()[3] * 0.37f;
        _node.Basis = shape == BodyShape.FlatDisc ? _flatWorldTurn : Basis.Identity;
        _material.SetShaderParameter("ring_color", rings.Color.ToGodot());
        _material.SetShaderParameter("ring_inner", (float)rings.InnerRadii);
        _material.SetShaderParameter("ring_outer", (float)rings.OuterRadii);
        _material.SetShaderParameter("ring_seed", Seed);
        return true;
    }

    /// <summary>
    /// Lights the rings from the star, given the way to it in the planet's own space.
    /// </summary>
    public void Light(Vector3 toStar) => _material.SetShaderParameter(
        "sun_direction", (_node.Basis.Inverse() * toStar).Normalized());

    /// <summary>Removes the rings.</summary>
    public void Free() => _node.QueueFree();

    private static ArrayMesh BuildMesh()
    {
        var points = new List<Vector3>();
        var indices = new List<int>();
        for (int segment = 0; segment <= Segments; segment++)
        {
            float angle = Mathf.Tau * segment / Segments;
            var outward = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            points.Add(outward);
            points.Add(outward * 2);
            if (segment < Segments)
            {
                int inner = segment * 2;
                indices.AddRange([inner, inner + 1, inner + 2, inner + 2, inner + 1, inner + 3]);
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = points.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = points.Select(_ => Vector3.Up).ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
