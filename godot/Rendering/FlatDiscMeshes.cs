using Godot;
using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Rendering;

/// <summary>
/// The meshes for a flat world (VISION.md BOD-02), sized in the matching globe's radii like
/// <see cref="FlatDisc"/>: the top face, which the planet material draws (and lifts by the
/// ground's heights, BOD-10), and the bare rock of the rim and underside.
/// </summary>
public static class FlatDiscMeshes
{
    // Rings from the center out, and points around each ring. The face is flat and the shader
    // works out the map from each pixel's exact position, so a few rings are plenty; the
    // segments keep the rim round.
    private const int Rings = 4;
    private const int Segments = 256;

    private static ArrayMesh? _top;
    private static ArrayMesh? _rock;
    private static readonly Dictionary<ReliefDetail, ArrayMesh> _reliefTops = [];

    /// <summary>
    /// The top face: a disc of radius π at height <see cref="FlatDisc.HalfThickness"/>.
    /// </summary>
    public static ArrayMesh Top => _top ??= BuildFace(top: true, Rings, Segments);

    /// <summary>
    /// The top face finely divided, for a flat world with heights or water, which the planet
    /// shader lifts point by point: twice <paramref name="detail"/>'s rings out to the rim
    /// and four times as many points around each (about as many points as a sculpted
    /// globe's mesh at that detail).
    /// </summary>
    public static ArrayMesh TopRelief(ReliefDetail detail)
    {
        if (!_reliefTops.TryGetValue(detail, out ArrayMesh? mesh))
        {
            mesh = BuildFace(top: true, 2 * (int)detail, 4 * (int)detail);
            _reliefTops[detail] = mesh;
        }

        return mesh;
    }

    /// <summary>The rim and the underside.</summary>
    public static ArrayMesh Rock => _rock ??= BuildRock(0);

    /// <summary>
    /// The rim and the underside, with the rim's top edge <paramref name="topLift"/> globe
    /// radii above the face's own height, to meet the ground (or water) at the rim.
    /// </summary>
    public static ArrayMesh RockLifted(float topLift) => topLift == 0 ? Rock : BuildRock(topLift);

    private static ArrayMesh BuildFace(bool top, int rings, int segments)
    {
        float height = (float)(top ? FlatDisc.HalfThickness : -FlatDisc.HalfThickness);
        Vector3 normal = top ? Vector3.Up : Vector3.Down;
        var points = new List<Vector3> { new(0, height, 0) };
        for (int ring = 1; ring <= rings; ring++)
        {
            float across = (float)(FlatDisc.Radius * ring / rings);
            for (int segment = 0; segment < segments; segment++)
            {
                points.Add(Around(segment, segments, across, height));
            }
        }

        int RingPoint(int ring, int segment) => 1 + (ring - 1) * segments + (segment % segments);
        var indices = new List<int>();
        for (int segment = 0; segment < segments; segment++)
        {
            AddTriangle(indices, 0, RingPoint(1, segment), RingPoint(1, segment + 1), top);
        }

        for (int ring = 1; ring < rings; ring++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int a = RingPoint(ring, segment);
                int b = RingPoint(ring, segment + 1);
                int c = RingPoint(ring + 1, segment);
                int d = RingPoint(ring + 1, segment + 1);
                AddTriangle(indices, a, c, d, top);
                AddTriangle(indices, a, d, b, top);
            }
        }

        var mesh = new ArrayMesh();
        AddSurface(mesh, points, points.Select(_ => normal).ToArray(), indices);
        return mesh;
    }

    private static ArrayMesh BuildRock(float topLift)
    {
        ArrayMesh mesh = BuildFace(top: false, Rings, Segments);
        float radius = (float)FlatDisc.Radius;
        float half = (float)FlatDisc.HalfThickness;
        var points = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<int>();
        for (int segment = 0; segment <= Segments; segment++)
        {
            Vector3 outward = Around(segment, Segments, 1, 0);
            points.Add(outward * radius + new Vector3(0, half + topLift, 0));
            points.Add(outward * radius - new Vector3(0, half, 0));
            normals.Add(outward);
            normals.Add(outward);
            if (segment < Segments)
            {
                int top = segment * 2;
                indices.AddRange([top, top + 2, top + 1, top + 1, top + 2, top + 3]);
            }
        }

        AddSurface(mesh, points, normals.ToArray(), indices);
        return mesh;
    }

    // A point around a ring of `segments`: segment 0 toward +Z (longitude 0), counting toward
    // +X (east).
    private static Vector3 Around(int segment, int segments, float across, float height)
    {
        float angle = Mathf.Tau * segment / segments;
        return new Vector3(Mathf.Sin(angle) * across, height, Mathf.Cos(angle) * across);
    }

    // Godot draws triangles whose corners run clockwise as seen from the front. Seen from
    // above, a, b, c run counterclockwise (longitude grows from +Z toward +X).
    private static void AddTriangle(List<int> indices, int a, int b, int c, bool facingUp)
    {
        indices.AddRange(facingUp ? [a, c, b] : [a, b, c]);
    }

    private static void AddSurface(
        ArrayMesh mesh, List<Vector3> points, Vector3[] normals, List<int> indices)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = points.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
    }
}
