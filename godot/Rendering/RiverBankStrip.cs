using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Rendering;

/// <summary>
/// The fine strip of ground along the rivers around a first-person eye (VISION.md BOD-11; see
/// <see cref="RiverBanks"/>): each river's bed and banks, drawn with the ground's own material
/// over the coarser ground around the eye, so its flat cells never show along a river; and the
/// same along the roads laid up close (BOD-13, <see cref="RoadStrips"/>), the dirt paths' worn
/// strands (<see cref="PathStrands"/>), or the roads' bridges (<see cref="PrepareBridges"/>).
/// Each point brings its place along to the shader as the ground's do (CUSTOM0), with w from 1
/// to 2 for how far its shading follows the strip's own slope rather than the ground's
/// heights, 2 more on a road's paving and 4 more on a bridge's bare stone; on worn ground, 7
/// plus how worn it is (0 to 1), and on wood, 10 (with its place in the grain as UV). Its
/// positions are kept relative to a point near the eye, so they hold their precision.
/// </summary>
public partial class RiverBankStrip : MeshInstance3D
{
    public RiverBankStrip()
    {
        Name = "RiverBanks";
        CastShadow = ShadowCastingSetting.Off;
    }

    /// <summary>The point the strip is built around, in the body's own space.</summary>
    public Vector3D Middle { get; private set; }

    /// <summary>
    /// Works out the mesh of the strips around <paramref name="middle"/>, each point where
    /// <paramref name="pointAt"/> puts a direction at a height in meters, and
    /// <paramref name="placeAt"/> gives what the shader reads as its place (a direction on a
    /// globe, the point on a flat world's face); null with none. Safe off the main thread;
    /// <see cref="Show"/> draws it.
    /// </summary>
    internal static Surface? Prepare(List<List<BankPoint[]>> strips, Vector3D middle,
        Func<Vector3D, double, Vector3D> pointAt, Func<Vector3D, double, Vector3D> placeAt)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var customs = new List<float>();
        var indices = new List<int>();
        foreach (List<BankPoint[]> strip in strips)
        {
            int width = strip[0].Length, first = positions.Count;
            Vector3D[,] points = new Vector3D[strip.Count, width];
            for (int row = 0; row < strip.Count; row++)
            {
                for (int across = 0; across < width; across++)
                {
                    BankPoint point = strip[row][across];
                    points[row, across] = pointAt(point.Direction, point.Meters);
                }
            }

            for (int row = 0; row < strip.Count; row++)
            {
                for (int across = 0; across < width; across++)
                {
                    BankPoint point = strip[row][across];
                    Vector3D at = points[row, across];
                    Vector3D at0 = at - middle;
                    positions.Add(new Vector3((float)at0.X, (float)at0.Y, (float)at0.Z));
                    normals.Add(NormalAt(points, row, across, pointAt, point));
                    Vector3D place = placeAt(point.Direction, point.Meters);
                    customs.AddRange([(float)place.X, (float)place.Y, (float)place.Z,
                        (float)(point.Surface switch
                        {
                            StripSurface.Paving => PavedW + point.Shaped,
                            StripSurface.Worn => WornW + point.Wear,
                            _ => 1 + point.Shaped,
                        })]);
                    if (row > 0 && across > 0)
                    {
                        int d = first + row * width + across, c = d - 1;
                        int b = d - width, a = b - 1;
                        indices.AddRange([a, b, c, b, d, c]);
                    }
                }
            }
        }

        return indices.Count == 0
            ? null
            : new Surface(middle, [.. positions], [.. normals], [.. customs], [.. indices], null);
    }

    /// <summary>
    /// Works out the mesh of the bridges around <paramref name="middle"/> (see
    /// <see cref="Prepare"/>), shaded by their own faces: stone ones' roads and walls' tops
    /// paved, wooden ones' planks along their grain; null with none. Safe off the main thread.
    /// </summary>
    internal static Surface? PrepareBridges(List<BridgeShown> bridges, Vector3D middle,
        Func<Vector3D, double, Vector3D> pointAt, Func<Vector3D, double, Vector3D> placeAt)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var customs = new List<float>();
        var grain = new List<Vector2>();
        var light = new List<Color>();
        var indices = new List<int>();
        foreach (BridgeShown bridge in bridges)
        {
            // The bridge's own frame where it lies, from points along, across, and above its
            // middle.
            double deck = bridge.Points.Count > 0 ? bridge.Points.Max(p => p.Position.Z) : 0;
            Vector3D At(double x, double y, double z)
            {
                (Vector3D direction, double meters) = bridge.Place(
                    new BridgePoint(new Vector3D(x, y, z), default, BridgeMaterial.Stone));
                return pointAt(direction, meters);
            }

            double half = bridge.LengthMeters / 2;
            Vector3D center = At(half, 0, deck);
            Vector3D along = Unit(At(half + 1, 0, deck) - center);
            Vector3D across = Unit(At(half, 1, deck) - center);
            Vector3D up = Unit(At(half, 0, deck + 1) - center);

            int first = positions.Count;
            var placed = new Vector3D[bridge.Points.Count];
            for (int i = 0; i < bridge.Points.Count; i++)
            {
                BridgePoint point = bridge.Points[i];
                (Vector3D direction, double meters) = bridge.Place(point);
                placed[i] = pointAt(direction, meters);
                Vector3D at0 = placed[i] - middle;
                positions.Add(new Vector3((float)at0.X, (float)at0.Y, (float)at0.Z));
                Vector3D normal = Unit(along * point.Normal.X + across * point.Normal.Y
                    + up * point.Normal.Z);
                normals.Add(new Vector3((float)normal.X, (float)normal.Y, (float)normal.Z));
                Vector3D place = placeAt(direction, meters);
                customs.AddRange([(float)place.X, (float)place.Y, (float)place.Z,
                    point.Material switch
                    {
                        BridgeMaterial.Paving => PavedW + 1,
                        BridgeMaterial.Wood => WoodW,
                        BridgeMaterial.Flame => FlameW,
                        _ => StoneW + 1,
                    }]);
                grain.Add(new Vector2((float)point.U, (float)point.V));
                float sky = 1 - (float)point.Dark;
                light.Add(new Color(sky, sky, sky));
            }

            // Each triangle wound clockwise seen from the way it faces, as Godot draws front
            // faces.
            for (int t = 0; t + 2 < bridge.Triangles.Count; t += 3)
            {
                int a = bridge.Triangles[t], b = bridge.Triangles[t + 1];
                int c = bridge.Triangles[t + 2];
                Vector3D facing = (placed[b] - placed[a]).Cross(placed[c] - placed[a]);
                Vector3 normal = normals[first + a];
                bool counter = facing.X * normal.X + facing.Y * normal.Y
                    + facing.Z * normal.Z > 0;
                indices.AddRange(counter
                    ? [first + a, first + c, first + b]
                    : [first + a, first + b, first + c]);
            }
        }

        return indices.Count == 0
            ? null
            : new Surface(middle, [.. positions], [.. normals], [.. customs], [.. indices],
                [.. grain])
            { Light = [.. light] };
    }

    /// <summary>
    /// Draws the strips worked out by <see cref="Prepare"/> (none: nothing is drawn) around
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
        arrays[(int)Mesh.ArrayType.Custom0] = surface.Customs;
        if (surface.Grain is { } grain)
        {
            arrays[(int)Mesh.ArrayType.TexUV] = grain;
        }

        if (surface.Light is { } light)
        {
            arrays[(int)Mesh.ArrayType.Color] = light;
        }

        arrays[(int)Mesh.ArrayType.Index] = surface.Indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays,
            flags: (Mesh.ArrayFormat)((int)Mesh.ArrayCustomFormat.RgbaFloat
                << (int)Mesh.ArrayFormat.FormatCustom0Shift));
        Mesh = mesh;
    }

    // What a paved point's w, and a bridge's bare stone's, start from in place of 1; a worn
    // point's; wood's; and a torch's flame's (planet_surface.gdshaderinc reads w past 2.5 as
    // paving, past 4.5 as stone, past 6.5 as worn ground, past 8.5 as wood, and past 11.5 as a
    // flame).
    private const float PavedW = 3, StoneW = 5, WornW = 7, WoodW = 10, FlameW = 12;

    private static Vector3D Unit(Vector3D v) => v * (1 / Math.Max(v.Length, 1e-30));

    // Which way the strip faces at a point: across its row and along the river, turned to
    // face up from the ground.
    private static Vector3 NormalAt(Vector3D[,] points, int row, int across,
        Func<Vector3D, double, Vector3D> pointAt, BankPoint point)
    {
        int rows = points.GetLength(0), width = points.GetLength(1);
        Vector3D sideways = points[row, Math.Min(across + 1, width - 1)]
            - points[row, Math.Max(across - 1, 0)];
        Vector3D along = points[Math.Min(row + 1, rows - 1), across]
            - points[Math.Max(row - 1, 0), across];
        Vector3D normal = sideways.Cross(along);
        Vector3D up = pointAt(point.Direction, point.Meters + 1)
            - pointAt(point.Direction, point.Meters);
        if (normal.Dot(up) < 0)
        {
            normal *= -1;
        }

        double length = normal.Length;
        normal = length > 0 ? normal * (1 / length) : up * (1 / up.Length);
        return new Vector3((float)normal.X, (float)normal.Y, (float)normal.Z);
    }

    /// <summary>
    /// The strips' mesh as worked out (<see cref="Prepare"/>), around its middle, with each
    /// point's place in the wood's grain on a bridge's mesh (else null).
    /// </summary>
    internal sealed record Surface(Vector3D Middle, Vector3[] Positions, Vector3[] Normals,
        float[] Customs, int[] Indices, Vector2[]? Grain)
    {
        /// <summary>
        /// How much of the sky's light reaches each point (in its color; deep in a tunnel
        /// little does), on a bridge's mesh; else null, all of it.
        /// </summary>
        public Color[]? Light { get; init; }
    }
}
