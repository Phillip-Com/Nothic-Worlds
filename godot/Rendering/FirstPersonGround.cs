using Godot;
using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Rendering;

/// <summary>
/// The cloud deck seen from above the clouds around a first-person eye (VISION.md REN-06):
/// rings of mesh around a point on the globe at a set radius, fine underfoot and coarser out
/// toward the horizon. Its positions are kept relative to that point rather than the globe's
/// middle, so they hold their precision near the eye; each point's direction from the middle
/// comes along in CUSTOM0. (The ground itself is drawn by <see cref="GroundTiles"/>.)
/// </summary>
public partial class FirstPersonGround : MeshInstance3D
{
    private const int Rings = 96;
    private const int Segments = 72;

    public FirstPersonGround()
    {
        Name = "FirstPersonGround";
        CastShadow = ShadowCastingSetting.Off;
    }

    private double[] _radii = [];

    /// <summary>The point the rings are around: its direction from the globe's middle.</summary>
    public Vector3D Center { get; private set; }

    /// <summary>How far out the rings are drawn at the center, in the globe's radii.</summary>
    public double CenterRadius { get; private set; }

    /// <summary>
    /// Builds the rings around <paramref name="center"/> (a unit direction in the globe's own
    /// frame), from <paramref name="innerAngle"/> to <paramref name="outerAngle"/> radians of
    /// arc away, at the radius <paramref name="radiusAt"/> gives for each direction (the cloud
    /// layer's).
    /// </summary>
    public void Build(Func<Vector3D, double> radiusAt, Vector3D center, double innerAngle,
        double outerAngle)
    {
        Center = center;
        CenterRadius = radiusAt(center);
        Vector3D middle = center * CenterRadius;
        (Vector3D east, Vector3D north) = Tangents(center);

        int count = 1 + Rings * Segments;
        var pointDirections = new Vector3D[count];
        pointDirections[0] = center;
        for (int ring = 0; ring < Rings; ring++)
        {
            double angle = innerAngle * Math.Pow(outerAngle / innerAngle, ring / (Rings - 1.0));
            for (int segment = 0; segment < Segments; segment++)
            {
                double bearing = Math.Tau * segment / Segments;
                pointDirections[1 + ring * Segments + segment] = center * Math.Cos(angle)
                    + (north * Math.Cos(bearing) + east * Math.Sin(bearing)) * Math.Sin(angle);
            }
        }

        _radii = new double[count];
        Parallel.For(0, count, i => _radii[i] = radiusAt(pointDirections[i]));
        var positions = new Vector3[count];
        var normals = new Vector3[count];
        var directions = new float[count * 4];
        for (int i = 0; i < count; i++)
        {
            Add(i, pointDirections[i]);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = positions;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Custom0] = directions;
        arrays[(int)Mesh.ArrayType.Index] = Triangles();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays,
            flags: (Mesh.ArrayFormat)((int)Mesh.ArrayCustomFormat.RgbaFloat
                << (int)Mesh.ArrayFormat.FormatCustom0Shift));
        Mesh = mesh;

        // Each point, lifted to the drawn ground and kept relative to the middle point.
        void Add(int index, Vector3D direction)
        {
            Vector3D point = direction * _radii[index] - middle;
            positions[index] = new Vector3((float)point.X, (float)point.Y, (float)point.Z);
            normals[index] = new Vector3((float)direction.X, (float)direction.Y,
                (float)direction.Z);
            directions[index * 4] = (float)direction.X;
            directions[index * 4 + 1] = (float)direction.Y;
            directions[index * 4 + 2] = (float)direction.Z;
            directions[index * 4 + 3] = 1;
        }
    }

    // A fan around the middle, then a strip between each ring and the next, wound clockwise
    // seen from above (Godot's front faces), so they face up from the ground.
    private static int[] Triangles()
    {
        var indices = new List<int>((Segments + (Rings - 1) * Segments * 2) * 3);
        for (int segment = 0; segment < Segments; segment++)
        {
            int next = (segment + 1) % Segments;
            indices.AddRange([0, 1 + segment, 1 + next]);
        }

        for (int ring = 0; ring < Rings - 1; ring++)
        {
            int inner = 1 + ring * Segments, outer = inner + Segments;
            for (int segment = 0; segment < Segments; segment++)
            {
                int next = (segment + 1) % Segments;
                indices.AddRange([inner + segment, outer + segment, inner + next]);
                indices.AddRange([inner + next, outer + segment, outer + next]);
            }
        }

        return [.. indices];
    }

    // East and north along the ground at a point (at a pole, any pair at right angles).
    private static (Vector3D East, Vector3D North) Tangents(Vector3D up)
    {
        var east = new Vector3D(up.Z, 0, -up.X);
        if (east.Length < 1e-9)
        {
            east = new Vector3D(1, 0, 0);
        }

        east *= 1 / east.Length;
        var north = new Vector3D(
            up.Y * east.Z - up.Z * east.Y,
            up.Z * east.X - up.X * east.Z,
            up.X * east.Y - up.Y * east.X);
        return (east, north);
    }
}
