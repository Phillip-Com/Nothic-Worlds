using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Interop;

namespace NothicWorlds.Rendering;

/// <summary>
/// A world tree as drawn (VISION.md BOD-02; owner's choice: grown from settings, and glowing):
/// its bark and glowing foliage, grown by Core's <see cref="WorldTreeShape"/>, and the light it
/// gives off. The mesh is a child of the tree's node, so it turns with the tree and is sized in
/// its radius; the light is kept apart, like a star's.
/// </summary>
public sealed class WorldTreeVisual
{
    // Sides around each bark piece, and how big foliage clumps are (in the tree's radius).
    private const int BarkSides = 8;
    private const float LeafSize = 0.075f;

    // How strongly the foliage glows, for each unit of the tree's glow.
    private const float LeafGlowShare = 0.25f;

    private static readonly SphereMesh _clump = new()
    {
        Radius = 1.0f,
        Height = 2.0f,
        RadialSegments = 10,
        Rings = 6,
    };

    private readonly MeshInstance3D _mesh;
    private readonly StandardMaterial3D _bark = new()
    {
        Roughness = 1.0f,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    private readonly StandardMaterial3D _leaves = new()
    {
        Roughness = 0.9f,
        EmissionEnabled = true,
    };

    private WorldTreeLook? _shown;

    /// <summary>Creates the tree's mesh under its node, and takes charge of its light.</summary>
    public WorldTreeVisual(Node3D root, OmniLight3D light)
    {
        _mesh = new MeshInstance3D { Name = "World tree" };
        root.AddChild(_mesh);
        Light = light;
    }

    /// <summary>The light the tree gives off (placed with it each frame).</summary>
    public OmniLight3D Light { get; }

    /// <summary>
    /// Shows the tree with this look: grown anew only when its shape changed (branches, spread,
    /// or seed); colors and glow just update.
    /// </summary>
    public void Show(WorldTreeLook look)
    {
        if (_shown == look)
        {
            return;
        }

        if (_shown is null || _shown.Branches != look.Branches || _shown.Spread != look.Spread
            || _shown.Seed != look.Seed)
        {
            _mesh.Mesh = BuildMesh(WorldTreeShape.Grow(look));
        }

        _shown = look;
        Color glow = look.Glow.ToGodot();
        _bark.AlbedoColor = look.Bark.ToGodot();
        _leaves.AlbedoColor = look.Leaves.ToGodot();
        _leaves.Emission = glow;
        _leaves.EmissionEnergyMultiplier = (float)look.GlowStrength * LeafGlowShare;
        Light.LightColor = glow;
        Light.LightEnergy = (float)look.GlowStrength;
        Light.Visible = look.GlowStrength > 0;
    }

    private ArrayMesh BuildMesh(GrownTree tree)
    {
        var bark = new SurfaceTool();
        bark.Begin(Mesh.PrimitiveType.Triangles);
        foreach (TreePiece piece in tree.Pieces)
        {
            AddPiece(bark, piece);
        }

        var leaves = new SurfaceTool();
        leaves.Begin(Mesh.PrimitiveType.Triangles);
        Godot.Collections.Array clump = _clump.GetMeshArrays();
        var clumpPoints = clump[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var clumpNormals = clump[(int)Mesh.ArrayType.Normal].AsVector3Array();
        var clumpIndices = clump[(int)Mesh.ArrayType.Index].AsInt32Array();
        foreach (Vector3D leaf in tree.Leaves)
        {
            var centre = new Vector3((float)leaf.X, (float)leaf.Y, (float)leaf.Z);
            foreach (int index in clumpIndices)
            {
                leaves.SetNormal(clumpNormals[index]);
                leaves.AddVertex(centre + clumpPoints[index] * LeafSize);
            }
        }

        ArrayMesh mesh = bark.Commit();
        mesh.SurfaceSetMaterial(0, _bark);
        leaves.Commit(mesh);
        mesh.SurfaceSetMaterial(1, _leaves);
        return mesh;
    }

    // A tapered tube from the piece's start to its end, as BarkSides flat sides.
    private static void AddPiece(SurfaceTool tool, TreePiece piece)
    {
        var from = new Vector3((float)piece.From.X, (float)piece.From.Y, (float)piece.From.Z);
        var to = new Vector3((float)piece.To.X, (float)piece.To.Y, (float)piece.To.Z);
        Vector3 axis = (to - from).Normalized();
        Vector3 side = axis.Cross(Mathf.Abs(axis.Y) < 0.9f ? Vector3.Up : Vector3.Right)
            .Normalized();
        Vector3 across = axis.Cross(side);
        for (int i = 0; i < BarkSides; i++)
        {
            Vector3 a = Around(side, across, i);
            Vector3 b = Around(side, across, i + 1);
            Vector3[] corners =
            [
                from + a * (float)piece.FromRadius, to + a * (float)piece.ToRadius,
                to + b * (float)piece.ToRadius, from + b * (float)piece.FromRadius,
            ];
            Vector3[] normals = [a, a, b, b];
            foreach (int corner in new[] { 0, 1, 2, 0, 2, 3 })
            {
                tool.SetNormal(normals[corner]);
                tool.AddVertex(corners[corner]);
            }
        }
    }

    private static Vector3 Around(Vector3 side, Vector3 across, int step)
    {
        float angle = Mathf.Tau * step / BarkSides;
        return side * Mathf.Cos(angle) + across * Mathf.Sin(angle);
    }
}
