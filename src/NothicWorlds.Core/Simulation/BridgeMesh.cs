using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Gathers a bridge's points and triangles (<see cref="StoneBridge"/>,
/// <see cref="WoodenBridge"/>), each face with its own points so its edges stay sharp.
/// </summary>
internal sealed class BridgeMesh
{
    public List<BridgePoint> Points { get; } = [];

    public List<int> Triangles { get; } = [];

    /// <summary>
    /// A face of four corners in order round it, facing <paramref name="normal"/>, made of
    /// <paramref name="material"/>; the triangles turn counterclockwise seen from that side.
    /// On wood, its grain runs along <paramref name="grain"/> (where that lies across the
    /// face; else along the face's other way).
    /// </summary>
    public void Quad(Vector3D a, Vector3D b, Vector3D c, Vector3D d, Vector3D normal,
        BridgeMaterial material, Vector3D grain = default)
    {
        (Vector3D along, Vector3D across) = GrainOn(normal, grain);
        int first = Points.Count;
        foreach (Vector3D corner in new[] { a, b, c, d })
        {
            Points.Add(new BridgePoint(corner, normal, material, corner.Dot(along),
                corner.Dot(across)));
        }

        bool forward = (b - a).Cross(c - a).Dot(normal) >= 0;
        Triangles.AddRange(forward
            ? [first, first + 1, first + 2, first, first + 2, first + 3]
            : [first, first + 2, first + 1, first, first + 3, first + 2]);
    }

    /// <summary>
    /// A box from <paramref name="low"/> to <paramref name="high"/> (its least and greatest
    /// x, y, z), its grain along <paramref name="grain"/>, but for its top, which runs from
    /// <paramref name="topStart"/> at its start (least x) to <paramref name="topEnd"/> at its
    /// end, and its bottom, likewise from <paramref name="bottomStart"/> to
    /// <paramref name="bottomEnd"/> (so it can follow a sloping deck). No face is left out.
    /// </summary>
    public void Box(double x0, double x1, double y0, double y1, double bottomStart,
        double bottomEnd, double topStart, double topEnd, BridgeMaterial material,
        Vector3D grain)
    {
        Vector3D a = new(x0, y0, bottomStart), b = new(x1, y0, bottomEnd);
        Vector3D c = new(x1, y1, bottomEnd), d = new(x0, y1, bottomStart);
        Vector3D e = new(x0, y0, topStart), f = new(x1, y0, topEnd);
        Vector3D g = new(x1, y1, topEnd), h = new(x0, y1, topStart);
        Vector3D up = Unit((f - e).Cross(h - e));
        Vector3D down = Unit((d - a).Cross(b - a));
        Quad(e, f, g, h, up, material, grain);
        Quad(a, d, c, b, down, material, grain);
        Quad(a, b, f, e, new(0, -1, 0), material, grain);
        Quad(c, d, h, g, new(0, 1, 0), material, grain);
        Quad(d, a, e, h, new(-1, 0, 0), material, grain);
        Quad(b, c, g, f, new(1, 0, 0), material, grain);
    }

    // Two ways across a face facing `normal`: along the grain where it lies across the face,
    // else along the face's longest other axis; and the way across that.
    private static (Vector3D Along, Vector3D Across) GrainOn(Vector3D normal, Vector3D grain)
    {
        Vector3D along = grain - normal * grain.Dot(normal);
        if (along.Length < 0.3)
        {
            along = Math.Abs(normal.Z) < 0.9 ? new Vector3D(0, 0, 1) : new Vector3D(1, 0, 0);
            along -= normal * along.Dot(normal);
        }

        along = Unit(along);
        return (along, Unit(normal.Cross(along)));
    }

    private static Vector3D Unit(Vector3D v) => v * (1 / Math.Max(v.Length, 1e-12));
}
