using Godot;
using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Rendering;

/// <summary>
/// The ground around a first-person eye (VISION.md REN-06), or the cloud deck above it (the
/// same rings at the cloud layer's height, with their own material): rings of mesh around a
/// point on
/// the globe, fine underfoot and coarser out toward the horizon, drawn with the globe's own
/// material so the map, terrain, relief, and style match. Its positions are kept relative to
/// that point rather than the globe's middle, so they hold their precision a meter from the
/// eye (a whole globe's positions only resolve to about half a meter on an Earth-sized one);
/// each point's direction from the middle comes along in CUSTOM0, which planet.gdshader reads
/// for this mesh. It sits over the globe's own mesh, whose flat facets lie a little lower.
/// </summary>
public partial class FirstPersonGround : MeshInstance3D
{
    private const int Rings = 96;
    private const int Segments = 72;

    // How far out each point was put (in radii), and what's needed to find the points around a
    // direction: the rings' angles from the center, and the center's east and north.
    private double[] _radii = [];
    private double _innerAngle, _outerAngle;
    private Vector3D _east, _north;

    public FirstPersonGround()
    {
        Name = "FirstPersonGround";
        CastShadow = ShadowCastingSetting.Off;
    }

    /// <summary>The point the rings are around: its direction from the globe's middle.</summary>
    public Vector3D Center { get; private set; }

    /// <summary>How far out the ground is drawn at the center, in the globe's radii.</summary>
    public double CenterRadius { get; private set; }

    /// <summary>
    /// Builds the rings around <paramref name="center"/> (a unit direction in the globe's own
    /// frame), from <paramref name="innerAngle"/> to <paramref name="outerAngle"/> radians of
    /// arc away, the drawn ground's height read from <paramref name="globe"/> (a body
    /// <paramref name="radiusKm"/> in radius) as seen up close, cliffs kept steep.
    /// </summary>
    public void Build(PlanetSurface globe, double radiusKm, Vector3D center, double innerAngle,
        double outerAngle) => Build(direction => globe.GroundRadiusAt(direction, radiusKm),
            center, innerAngle, outerAngle);

    /// <summary>
    /// Builds the rings as <see cref="Build(PlanetSurface, Vector3D, double, double)"/>, at
    /// the radius <paramref name="radiusAt"/> gives for each direction (the cloud deck: the
    /// cloud layer's).
    /// </summary>
    public void Build(Func<Vector3D, double> radiusAt, Vector3D center, double innerAngle,
        double outerAngle)
    {
        Center = center;
        CenterRadius = radiusAt(center);
        Vector3D middle = center * CenterRadius;
        (Vector3D east, Vector3D north) = Tangents(center);
        (_innerAngle, _outerAngle, _east, _north) = (innerAngle, outerAngle, east, north);

        int count = 1 + Rings * Segments;
        _radii = new double[count];
        var positions = new Vector3[count];
        var normals = new Vector3[count];
        var directions = new float[count * 4];
        Add(0, center);
        for (int ring = 0; ring < Rings; ring++)
        {
            double angle = innerAngle * Math.Pow(outerAngle / innerAngle, ring / (Rings - 1.0));
            for (int segment = 0; segment < Segments; segment++)
            {
                double bearing = Math.Tau * segment / Segments;
                Vector3D direction = center * Math.Cos(angle)
                    + (north * Math.Cos(bearing) + east * Math.Sin(bearing)) * Math.Sin(angle);
                Add(1 + ring * Segments + segment, direction);
            }
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
            _radii[index] = radiusAt(direction);
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

    /// <summary>
    /// The highest the drawn ground reaches around a direction, in radii: the highest corner of
    /// the mesh's cell it falls in. The mesh is flat between its points, so where the ground
    /// curves or steps it can stand above the ground's own height; an eye kept above this
    /// never sinks into it. Null if the direction is off the rings (or nothing is built).
    /// </summary>
    public double? HighestAround(Vector3D direction)
    {
        if (_radii.Length == 0)
        {
            return null;
        }

        double angle = Math.Acos(Math.Clamp(direction.Dot(Center), -1, 1));
        if (angle >= _outerAngle)
        {
            return null;
        }

        double bearing = Math.Atan2(direction.Dot(_east), direction.Dot(_north));
        double around = (bearing / Math.Tau + 1) % 1 * Segments;
        int segment = (int)around % Segments, next = (segment + 1) % Segments;
        if (angle < _innerAngle)
        {
            // In the fan around the middle.
            return Math.Max(_radii[0], Math.Max(_radii[1 + segment], _radii[1 + next]));
        }

        int ring = Math.Clamp((int)(Math.Log(angle / _innerAngle)
            / Math.Log(_outerAngle / _innerAngle) * (Rings - 1)), 0, Rings - 2);
        int inner = 1 + ring * Segments, outer = inner + Segments;
        return Math.Max(Math.Max(_radii[inner + segment], _radii[inner + next]),
            Math.Max(_radii[outer + segment], _radii[outer + next]));
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
