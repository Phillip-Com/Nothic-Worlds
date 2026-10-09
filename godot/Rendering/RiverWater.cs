using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Rendering;

/// <summary>
/// The rivers' water around a first-person eye (VISION.md BOD-11; owner's choices: banks
/// carved up close, ripples and rapids): a strip along each stretch of river in reach (see
/// <see cref="RiverChannels"/>), as wide as the water between its banks, drawn with
/// river_water.gdshader, whose ripples flow downstream at the river's speed there and turn
/// white over rapids. As with <see cref="GroundTiles"/>, its positions are kept relative
/// to a point near the eye, so they hold their precision. On a flat world it lies over the top
/// face, each direction at the point of the face that stands for it.
/// </summary>
public partial class RiverWater : MeshInstance3D
{
    // How far the water reaches in under the banks, in meters, so no gap shows between them.
    private const double TuckMeters = 0.3;

    // The ripples repeat this far downstream (WAVE_REPEAT_METERS in river_water.gdshader).
    private const double RepeatMeters = 1000;

    public RiverWater()
    {
        Name = "RiverWater";
        CastShadow = ShadowCastingSetting.Off;
        MaterialOverride = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://Rendering/river_water.gdshader"),
        };
    }

    /// <summary>The point the water is built around, in the body's own space.</summary>
    public Vector3D Middle { get; private set; }

    /// <summary>
    /// Builds the water of <paramref name="channels"/> (none: nothing is drawn) around
    /// <paramref name="middle"/>, on a body <paramref name="radiusMeters"/> in radius, each
    /// point where <paramref name="pointAt"/> puts a direction at a height in meters.
    /// </summary>
    public void Build(RiverChannels? channels, Vector3D middle,
        Func<Vector3D, double, Vector3D> pointAt, double radiusMeters)
    {
        Middle = middle;
        if (channels is null)
        {
            Mesh = null;
            return;
        }

        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var tangents = new List<float>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var indices = new List<int>();
        foreach (IReadOnlyList<ChannelPoint> stretch in channels.Stretches)
        {
            // Ripples repeat every RepeatMeters, so starting the count there keeps them in
            // place as the water's rebuilt; within a stretch it counts on, so they don't jump.
            double start = stretch[0].AlongMeters;
            double origin = start - Math.Floor(start / RepeatMeters) * RepeatMeters;
            int first = positions.Count;
            for (int i = 0; i < stretch.Count; i++)
            {
                ChannelPoint point = stretch[i];
                Vector3D along = stretch[Math.Min(i + 1, stretch.Count - 1)].Direction
                    - stretch[Math.Max(i - 1, 0)].Direction;
                Vector3D side = Cross(point.Direction, along);
                side *= 1 / Math.Max(side.Length, 1e-12);

                // Up and downstream where the water's placed (on a flat world, the face's).
                Vector3D up = pointAt(point.Direction, 1) - pointAt(point.Direction, 0);
                Vector3D downstream = pointAt(point.Direction + along, point.SurfaceMeters)
                    - pointAt(point.Direction, point.SurfaceMeters);
                downstream *= 1 / Math.Max(downstream.Length, 1e-12);
                Vector3 normal = ToGodot(up).Normalized();

                // Between the banks: wider than the bed where it's carved, as they slope out.
                double half = point.HalfWidthMeters + TuckMeters + point.Carved
                    * Math.Max(0, point.SurfaceMeters - point.BedMeters)
                    / RiverChannels.BankSlope;
                float u = (float)(origin + point.AlongMeters - start);
                var color = new Color((float)(point.FlowMetersPerSecond
                    / RiverProfile.MaxFlow), (float)point.Rapids, 0, 1);
                foreach (double across in new[] { -half, half })
                {
                    Vector3D direction = point.Direction + side * (across / radiusMeters);
                    direction *= 1 / direction.Length;
                    Vector3D at = pointAt(direction, point.SurfaceMeters) - Middle;
                    positions.Add(new Vector3((float)at.X, (float)at.Y, (float)at.Z));
                    normals.Add(normal);
                    tangents.AddRange([(float)downstream.X, (float)downstream.Y,
                        (float)downstream.Z, 1]);
                    uvs.Add(new Vector2(u, (float)across));
                    colors.Add(color);
                }

                if (i > 0)
                {
                    int a = first + 2 * (i - 1);
                    indices.AddRange([a, a + 2, a + 1, a + 1, a + 2, a + 3]);
                }
            }
        }

        if (indices.Count == 0)
        {
            Mesh = null;
            return;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = positions.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Tangent] = tangents.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        Mesh = mesh;
    }

    /// <summary>Tells the shader how many meters one of the scene's units is.</summary>
    public void SetScale(double metersPerUnit) =>
        ((ShaderMaterial)MaterialOverride).SetShaderParameter("meters_per_unit",
            (float)metersPerUnit);

    private static Vector3 ToGodot(Vector3D v) => new((float)v.X, (float)v.Y, (float)v.Z);

    private static Vector3D Cross(Vector3D a, Vector3D b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}
