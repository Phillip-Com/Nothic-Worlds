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
    /// Works out the water of <paramref name="channels"/> (none: null) around
    /// <paramref name="middle"/>, on a body <paramref name="radiusMeters"/> in radius, each
    /// point where <paramref name="pointAt"/> puts a direction at a height in meters. Safe off
    /// the main thread; <see cref="Show"/> draws it.
    /// </summary>
    internal static Surface? Prepare(RiverChannels? channels, Vector3D middle,
        Func<Vector3D, double, Vector3D> pointAt, double radiusMeters)
    {
        if (channels is null)
        {
            return null;
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
                Vector3D side = point.Direction.Cross(along);
                side *= 1 / Math.Max(side.Length, 1e-12);

                // Up and downstream where the water's placed (on a flat world, the face's).
                Vector3D up = pointAt(point.Direction, 1) - pointAt(point.Direction, 0);
                Vector3D downstream = pointAt(point.Direction + along, point.SurfaceMeters)
                    - pointAt(point.Direction, point.SurfaceMeters);
                downstream *= 1 / Math.Max(downstream.Length, 1e-12);
                Vector3 normal = ToGodot(up).Normalized();

                // Between the banks: wider than the bed, as they slope out.
                double half = point.HalfWidthMeters + TuckMeters
                    + Math.Max(0, point.SurfaceMeters - point.BedMeters) / RiverCarving.BankSlope;
                float u = (float)(origin + point.AlongMeters - start);
                var color = new Color((float)(point.FlowMetersPerSecond
                    / RiverProfile.MaxFlow), (float)point.Rapids, 0, 1);
                foreach (double across in new[] { -half, half })
                {
                    Vector3D direction = point.Direction + side * (across / radiusMeters);
                    direction *= 1 / direction.Length;
                    Vector3D at = pointAt(direction, point.SurfaceMeters) - middle;
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

        return indices.Count == 0
            ? null
            : new Surface(middle, [.. positions], [.. normals], [.. tangents], [.. uvs],
                [.. colors], [.. indices]);
    }

    /// <summary>
    /// Draws the water worked out by <see cref="Prepare"/> (none: nothing is drawn) around
    /// <paramref name="middle"/>.
    /// </summary>
    internal void Show(Surface? surface, Vector3D middle)
    {
        Middle = surface?.Middle ?? middle;
        if (surface is null)
        {
            Mesh = null;
            return;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = surface.Positions;
        arrays[(int)Mesh.ArrayType.Normal] = surface.Normals;
        arrays[(int)Mesh.ArrayType.Tangent] = surface.Tangents;
        arrays[(int)Mesh.ArrayType.TexUV] = surface.Uvs;
        arrays[(int)Mesh.ArrayType.Color] = surface.Colors;
        arrays[(int)Mesh.ArrayType.Index] = surface.Indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        Mesh = mesh;
    }

    /// <summary>Tells the shader how many meters one of the scene's units is.</summary>
    public void SetScale(double metersPerUnit) =>
        ((ShaderMaterial)MaterialOverride).SetShaderParameter("meters_per_unit",
            (float)metersPerUnit);

    private static Vector3 ToGodot(Vector3D v) => new((float)v.X, (float)v.Y, (float)v.Z);

    /// <summary>
    /// The water's mesh as worked out (<see cref="Prepare"/>), around its middle.
    /// </summary>
    internal sealed record Surface(Vector3D Middle, Vector3[] Positions, Vector3[] Normals,
        float[] Tangents, Vector2[] Uvs, Color[] Colors, int[] Indices);
}
