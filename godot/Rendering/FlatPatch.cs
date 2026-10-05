using Godot;
using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Rendering;

/// <summary>
/// The ground around a first-person eye on a flat world (VISION.md REN-06), or the cloud deck
/// over it: a patch of mesh on the face the eye is on, fine underfoot and coarser out to its
/// edge, kept relative to its middle so it holds its precision a meter from the eye. On the top
/// face each point's place on the disc rides in CUSTOM0, so the globe's material draws the map
/// there as on the disc itself; the rim and underside are bare rock.
/// </summary>
public partial class FlatPatch : MeshInstance3D
{
    private const int Rings = 96;
    private const int Segments = 72;
    private const int RimColumns = 96;
    private const int RimRows = 12;

    /// <summary>Makes an empty patch.</summary>
    public FlatPatch()
    {
        CastShadow = ShadowCastingSetting.Off;
    }

    /// <summary>
    /// The patch's middle, in the body's own space (globe radii): its points are kept relative
    /// to it.
    /// </summary>
    public Vector3D Middle { get; private set; }

    /// <summary>
    /// Builds the patch around <paramref name="spot"/>, out to <paramref name="outer"/> globe
    /// radii, <paramref name="lift"/> radii off the face (the cloud deck's height, or 0), with
    /// <paramref name="custom"/> giving each point's CUSTOM0 (by default, the point itself).
    /// </summary>
    public void Build(FlatSpot spot, double outer, double lift = 0,
        Func<Vector3D, Vector3D>? custom = null)
    {
        Middle = FlatWalk.Point(spot, lift);
        (List<Vector3D> points, List<int> indices) = spot.Face == FlatFace.Rim
            ? RimPatch(spot, outer, lift)
            : FacePatch(spot, outer, lift);
        Vector3D up = FlatWalk.Frame(spot).Up;
        FaceOutward(points, indices, up);

        var positions = new Vector3[points.Count];
        var normals = new Vector3[points.Count];
        var customs = new float[points.Count * 4];
        for (int i = 0; i < points.Count; i++)
        {
            positions[i] = ToGodot(points[i] - Middle);
            normals[i] = ToGodot(spot.Face == FlatFace.Rim ? Outward(points[i]) : up);
            Vector3D extra = custom?.Invoke(points[i]) ?? points[i];
            customs[i * 4] = (float)extra.X;
            customs[i * 4 + 1] = (float)extra.Y;
            customs[i * 4 + 2] = (float)extra.Z;
            customs[i * 4 + 3] = 1;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = positions;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Custom0] = customs;
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays,
            flags: (Mesh.ArrayFormat)((int)Mesh.ArrayCustomFormat.RgbaFloat
                << (int)Mesh.ArrayFormat.FormatCustom0Shift));
        Mesh = mesh;
    }

    // Rings around the spot on the top or bottom face, spreading out to the edge of the patch;
    // points past the rim are pulled back onto it.
    private static (List<Vector3D>, List<int>) FacePatch(FlatSpot spot, double outer,
        double lift)
    {
        double height = (spot.Face == FlatFace.Top ? 1 : -1) * (FlatDisc.HalfThickness + lift);
        double inner = Math.Min(2e-7, outer / 1000);
        var points = new List<Vector3D> { OnFace(spot.A, spot.B, height) };
        for (int ring = 0; ring < Rings; ring++)
        {
            double across = inner * Math.Pow(outer / inner, ring / (Rings - 1.0));
            for (int segment = 0; segment < Segments; segment++)
            {
                double angle = Math.Tau * segment / Segments;
                points.Add(OnFace(spot.A + across * Math.Sin(angle),
                    spot.B + across * Math.Cos(angle), height));
            }
        }

        var indices = new List<int>();
        for (int segment = 0; segment < Segments; segment++)
        {
            indices.AddRange([0, 1 + segment, 1 + (segment + 1) % Segments]);
        }

        for (int ring = 0; ring < Rings - 1; ring++)
        {
            int first = 1 + ring * Segments, next = first + Segments;
            for (int segment = 0; segment < Segments; segment++)
            {
                int after = (segment + 1) % Segments;
                indices.AddRange([first + segment, next + segment, first + after]);
                indices.AddRange([first + after, next + segment, next + after]);
            }
        }

        return (points, indices);
    }

    // A strip of the rim around the spot: the whole wall's height, and as far round as the
    // patch reaches.
    private static (List<Vector3D>, List<int>) RimPatch(FlatSpot spot, double outer, double lift)
    {
        double span = Math.Min(outer / FlatDisc.Radius, Math.PI);
        var points = new List<Vector3D>();
        for (int row = 0; row <= RimRows; row++)
        {
            double height = -FlatDisc.HalfThickness + 2 * FlatDisc.HalfThickness * row / RimRows;
            for (int column = 0; column <= RimColumns; column++)
            {
                double around = spot.A + span * (2.0 * column / RimColumns - 1);
                points.Add(new Vector3D(Math.Sin(around) * (FlatDisc.Radius + lift), height,
                    Math.Cos(around) * (FlatDisc.Radius + lift)));
            }
        }

        var indices = new List<int>();
        for (int row = 0; row < RimRows; row++)
        {
            for (int column = 0; column < RimColumns; column++)
            {
                int a = row * (RimColumns + 1) + column, b = a + 1;
                int c = a + RimColumns + 1, d = c + 1;
                indices.AddRange([a, c, b]);
                indices.AddRange([b, c, d]);
            }
        }

        return (points, indices);
    }

    // Turns each triangle so its front (Godot's: wound clockwise as seen) faces the face's up
    // (on the rim, outward from the disc's axis at that triangle).
    private static void FaceOutward(List<Vector3D> points, List<int> indices, Vector3D up)
    {
        for (int i = 0; i < indices.Count; i += 3)
        {
            Vector3D a = points[indices[i]], b = points[indices[i + 1]], c = points[indices[i + 2]];
            Vector3D facing = Cross(b - a, c - a);
            Vector3D outward = up.Y == 0 ? Outward(a + b + c) : up;
            if (facing.Dot(outward) > 0)
            {
                (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
            }
        }
    }

    // A point on the top or bottom face, pulled back onto the rim if it's past it.
    private static Vector3D OnFace(double x, double z, double height)
    {
        double across = Math.Sqrt(x * x + z * z);
        double scale = across > FlatDisc.Radius ? FlatDisc.Radius / across : 1;
        return new Vector3D(x * scale, height, z * scale);
    }

    private static Vector3D Outward(Vector3D point)
    {
        double across = Math.Sqrt(point.X * point.X + point.Z * point.Z);
        return across == 0 ? new Vector3D(0, 0, 1) : new Vector3D(point.X / across, 0,
            point.Z / across);
    }

    private static Vector3D Cross(Vector3D a, Vector3D b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X);

    private static Vector3 ToGodot(Vector3D v) => new((float)v.X, (float)v.Y, (float)v.Z);
}
