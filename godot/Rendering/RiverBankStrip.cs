using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Rendering;

/// <summary>
/// The fine strip of ground along the rivers around a first-person eye (VISION.md BOD-11; see
/// <see cref="RiverBanks"/>): each river's bed and banks, drawn with the ground's own material
/// over the coarser ground around the eye, so its flat cells never show along a river. Each
/// point brings its place along to the shader as the ground's do (CUSTOM0), with w from 1 to
/// 2 for how far its shading follows the strip's own slope rather than the ground's heights.
/// Its positions are kept relative to a point near the eye, so they hold their precision.
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
    /// Builds the strips around <paramref name="middle"/>, each point where
    /// <paramref name="pointAt"/> puts a direction at a height in meters, and
    /// <paramref name="placeAt"/> gives what the shader reads as its place (a direction on a
    /// globe, the point on a flat world's face). None: nothing is drawn.
    /// </summary>
    public void Build(List<List<BankPoint[]>> strips, Vector3D middle,
        Func<Vector3D, double, Vector3D> pointAt, Func<Vector3D, double, Vector3D> placeAt)
    {
        Middle = middle;
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
                        (float)(1 + point.Shaped)]);
                    if (row > 0 && across > 0)
                    {
                        int d = first + row * width + across, c = d - 1;
                        int b = d - width, a = b - 1;
                        indices.AddRange([a, b, c, b, d, c]);
                    }
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
        arrays[(int)Mesh.ArrayType.Custom0] = customs.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays,
            flags: (Mesh.ArrayFormat)((int)Mesh.ArrayCustomFormat.RgbaFloat
                << (int)Mesh.ArrayFormat.FormatCustom0Shift));
        Mesh = mesh;
    }

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
        Vector3D normal = Cross(sideways, along);
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

    private static Vector3D Cross(Vector3D a, Vector3D b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}
