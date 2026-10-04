using Godot;
using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Rendering;

/// <summary>
/// A unit sphere made of the six faces of <see cref="CubeSphere"/>, for sculpted globes
/// (VISION.md BOD-04): its vertices are spaced like the height cells, and the planet shader
/// lifts each one by the height under it. Shared by every sculpted globe.
/// </summary>
/// <remarks>
/// Vertices on the edges between faces are shared (welded), so the two faces always lift them
/// the same and the surface can't crack open along a seam.
/// </remarks>
public static class CubeSphereMesh
{
    // Squares along each edge of a face: about 11 height cells each. The shader shades every
    // cell, so the mesh only needs to carry the ground's shape (and its outline).
    private const int Divisions = 96;

    private static ArrayMesh? _mesh;

    /// <summary>The mesh, built the first time it's needed.</summary>
    public static ArrayMesh Shared => _mesh ??= Build();

    private static ArrayMesh Build()
    {
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        var welded = new Dictionary<(long, long, long), int>();
        for (int face = 0; face < CubeSphere.FaceCount; face++)
        {
            var grid = new int[Divisions + 1, Divisions + 1];
            for (int down = 0; down <= Divisions; down++)
            {
                for (int across = 0; across <= Divisions; across++)
                {
                    Vector3D direction = CubeSphere.Direction(
                        face, (double)across / Divisions, (double)down / Divisions);
                    grid[across, down] = Weld(vertices, welded, direction);
                }
            }

            // Two triangles a square, clockwise seen from outside (Godot's front faces).
            for (int down = 0; down < Divisions; down++)
            {
                for (int across = 0; across < Divisions; across++)
                {
                    int topLeft = grid[across, down];
                    int topRight = grid[across + 1, down];
                    int bottomLeft = grid[across, down + 1];
                    int bottomRight = grid[across + 1, down + 1];
                    indices.AddRange([topLeft, topRight, bottomRight]);
                    indices.AddRange([topLeft, bottomRight, bottomLeft]);
                }
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        Vector3[] points = [.. vertices];
        arrays[(int)Mesh.ArrayType.Vertex] = points;
        arrays[(int)Mesh.ArrayType.Normal] = points;  // A unit sphere's normals are its points
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    // The index of the vertex at a direction, adding it unless a face already has.
    private static int Weld(
        List<Vector3> vertices, Dictionary<(long, long, long), int> welded, Vector3D direction)
    {
        const double grain = 1e6;
        var key = ((long)Math.Round(direction.X * grain), (long)Math.Round(direction.Y * grain),
            (long)Math.Round(direction.Z * grain));
        if (!welded.TryGetValue(key, out int index))
        {
            index = vertices.Count;
            vertices.Add(new Vector3((float)direction.X, (float)direction.Y, (float)direction.Z));
            welded[key] = index;
        }

        return index;
    }
}
