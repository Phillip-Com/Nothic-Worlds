using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The stone arch bridge a highway crosses a river on (VISION.md BOD-13; owner's choice,
/// 2026-10-12: round arches, each at most <see cref="MaxArchMeters"/> across, piers in the
/// water, and low walls along its sides). Built in the bridge's own frame, in meters: x along
/// it from its start, y across it (to the left), z up (heights as the ground's), so it can be
/// drawn wherever the road puts it. Arches spring from just over the water; piers and the
/// abutments at the ends reach down below the river's bed, into the ground.
/// </summary>
public static class StoneBridge
{
    /// <summary>The widest an arch is, in meters.</summary>
    public const double MaxArchMeters = 20;

    /// <summary>How high and thick the walls along the sides are, in meters.</summary>
    public const double WallHeightMeters = 1, WallThicknessMeters = 0.5;

    /// <summary>The least stone over the crown of each arch, in meters.</summary>
    public const double CrownMeters = 0.8;

    /// <summary>How far over the water the arches spring, in meters.</summary>
    public const double SpringAboveWaterMeters = 0.3;

    // How far below the bed the piers and abutments reach, in meters.
    private const double FootingMeters = 1;

    // How many steps round each arch.
    private const int ArchSteps = 12;

    /// <summary>How many arches a bridge this long has.</summary>
    public static int ArchCount(double lengthMeters) =>
        Math.Max(1, (int)Math.Ceiling(lengthMeters / MaxArchMeters));

    /// <summary>How thick the piers between arches each this far apart are, in meters.</summary>
    public static double PierMeters(double spanMeters) => Math.Max(1, 0.2 * spanMeters);

    /// <summary>
    /// How high over the water a bridge this long must carry its road, in meters, for its
    /// arches to fit under it.
    /// </summary>
    public static double ClearanceMeters(double lengthMeters)
    {
        double span = lengthMeters / ArchCount(lengthMeters);
        return SpringAboveWaterMeters + ArchRadius(span) + CrownMeters;
    }

    /// <summary>
    /// The bridge's stone: its triangles (each three indices into its points, turning
    /// counterclockwise seen from the way they face) for a bridge
    /// <paramref name="lengthMeters"/> long carrying a road <paramref name="halfWidthMeters"/>
    /// to each side, its road at <paramref name="startMeters"/> at the start and
    /// <paramref name="endMeters"/> at the end (straight between), over water at
    /// <paramref name="waterMeters"/> and a bed at <paramref name="bedMeters"/>.
    /// </summary>
    public static (List<BridgePoint> Points, List<int> Triangles) Build(double lengthMeters,
        double halfWidthMeters, double startMeters, double endMeters, double waterMeters,
        double bedMeters)
    {
        var mesh = new Builder();
        double deckHalf = halfWidthMeters, outer = halfWidthMeters + WallThicknessMeters;
        double foot = Math.Min(bedMeters, waterMeters) - FootingMeters;
        double Deck(double x) => startMeters + (endMeters - startMeters)
            * (lengthMeters > 0 ? x / lengthMeters : 0);
        double WallTop(double x) => Deck(x) + WallHeightMeters;

        List<(double X, double Bottom)> underside = Underside(lengthMeters, waterMeters, foot);

        // The faces along each side, from the underside to the walls' tops.
        for (int i = 1; i < underside.Count; i++)
        {
            (double x0, double b0) = underside[i - 1];
            (double x1, double b1) = underside[i];
            if (x1 - x0 <= 1e-9)
            {
                continue;
            }

            mesh.Quad(new(x0, outer, b0), new(x1, outer, b1), new(x1, outer, WallTop(x1)),
                new(x0, outer, WallTop(x0)), new(0, 1, 0), paved: false);
            mesh.Quad(new(x1, -outer, b1), new(x0, -outer, b0), new(x0, -outer, WallTop(x0)),
                new(x1, -outer, WallTop(x1)), new(0, -1, 0), paved: false);
        }

        // The underside: the arches' curves, and the piers' faces under where they spring.
        for (int i = 1; i < underside.Count; i++)
        {
            (double x0, double b0) = underside[i - 1];
            (double x1, double b1) = underside[i];
            double dx = x1 - x0, dz = b1 - b0, length = Math.Sqrt(dx * dx + dz * dz);
            if (length <= 1e-9)
            {
                continue;
            }

            var normal = new Vector3D(dz / length, 0, -dx / length);
            mesh.Quad(new(x0, -outer, b0), new(x1, -outer, b1), new(x1, outer, b1),
                new(x0, outer, b0), normal, paved: false);
        }

        // The road over it, the walls' inner faces and tops, and the ends.
        mesh.Quad(new(0, -deckHalf, Deck(0)), new(lengthMeters, -deckHalf, Deck(lengthMeters)),
            new(lengthMeters, deckHalf, Deck(lengthMeters)), new(0, deckHalf, Deck(0)),
            new(0, 0, 1), paved: true);
        foreach (double side in new[] { 1.0, -1.0 })
        {
            double inner = side * deckHalf, edge = side * outer;
            mesh.Quad(new(0, inner, Deck(0)), new(lengthMeters, inner, Deck(lengthMeters)),
                new(lengthMeters, inner, WallTop(lengthMeters)), new(0, inner, WallTop(0)),
                new(0, -side, 0), paved: false);
            mesh.Quad(new(0, inner, WallTop(0)), new(lengthMeters, inner, WallTop(lengthMeters)),
                new(lengthMeters, edge, WallTop(lengthMeters)), new(0, edge, WallTop(0)),
                new(0, 0, 1), paved: true);
        }

        mesh.Quad(new(0, outer, foot), new(0, outer, WallTop(0)), new(0, -outer, WallTop(0)),
            new(0, -outer, foot), new(-1, 0, 0), paved: false);
        mesh.Quad(new(lengthMeters, -outer, foot), new(lengthMeters, -outer, WallTop(lengthMeters)),
            new(lengthMeters, outer, WallTop(lengthMeters)), new(lengthMeters, outer, foot),
            new(1, 0, 0), paved: false);
        return (mesh.Points, mesh.Triangles);
    }

    // The radius of each arch between piers this far apart, in meters.
    private static double ArchRadius(double spanMeters) =>
        (spanMeters - PierMeters(spanMeters)) / 2;

    // The underside along the bridge, as heights at points along it from start to end: down
    // at `foot` under the abutments and piers, round each arch, springing just over the water.
    // A pier's faces are two points at one place, the foot's and the arch's spring.
    private static List<(double X, double Bottom)> Underside(double lengthMeters,
        double waterMeters, double foot)
    {
        int arches = ArchCount(lengthMeters);
        double span = lengthMeters / arches, pier = PierMeters(span), radius = ArchRadius(span);
        double spring = waterMeters + SpringAboveWaterMeters;
        var points = new List<(double X, double Bottom)> { (0, foot) };
        for (int a = 0; a < arches; a++)
        {
            double middle = (a + 0.5) * span;
            points.Add((middle - radius, foot));
            for (int step = 0; step <= ArchSteps; step++)
            {
                double angle = Math.PI * (1 - (double)step / ArchSteps);
                points.Add((middle + radius * Math.Cos(angle), spring + radius * Math.Sin(angle)));
            }

            points.Add((middle + radius, foot));
        }

        points.Add((lengthMeters, foot));
        return points;
    }

    // Gathers the bridge's points and triangles, each face with its own points so its edges
    // stay sharp.
    private sealed class Builder
    {
        public List<BridgePoint> Points { get; } = [];

        public List<int> Triangles { get; } = [];

        // A face of four corners in order round it, facing `normal`; the triangles turn
        // counterclockwise seen from that side.
        public void Quad(Vector3D a, Vector3D b, Vector3D c, Vector3D d, Vector3D normal,
            bool paved)
        {
            int first = Points.Count;
            foreach (Vector3D corner in new[] { a, b, c, d })
            {
                Points.Add(new BridgePoint(corner, normal, paved));
            }

            bool forward = (b - a).Cross(c - a).Dot(normal) >= 0;
            Triangles.AddRange(forward
                ? [first, first + 1, first + 2, first, first + 2, first + 3]
                : [first, first + 2, first + 1, first, first + 3, first + 2]);
        }
    }
}
