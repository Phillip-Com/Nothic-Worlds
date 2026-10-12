using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The stone arch bridge a highway crosses a river on (VISION.md BOD-13; owner's choice,
/// 2026-10-12: round arches, each at most <see cref="MaxArchMeters"/> across, piers in the
/// water, and low walls along its sides). Built in the bridge's own frame, in meters: x along
/// it from its start, y across it (to the left), z up (heights as the ground's), so it can be
/// drawn wherever the road puts it. Arches spring from just over the water; piers and the
/// abutments at the ends reach down below the river's bed, into the ground. An old road's
/// bridge (owner's choice, 2026-10-12) is the same, of weathered stone, its walls broken: a
/// piece in <see cref="WallPieceMeters"/> fallen to a stub here and there, or gone.
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

    /// <summary>How long each piece of an old bridge's walls is, in meters.</summary>
    public const double WallPieceMeters = 2;

    // What share of an old bridge's wall pieces are fallen to a stub, and gone, and how high
    // a stub stands, in meters.
    private const double FallenShare = 0.25, GoneShare = 0.1, StubMeters = 0.3;

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
    /// <paramref name="waterMeters"/> and a bed at <paramref name="bedMeters"/>; an old road's,
    /// weathered and its walls broken as <paramref name="ruinSeed"/> has them, if given.
    /// </summary>
    public static (List<BridgePoint> Points, List<int> Triangles) Build(double lengthMeters,
        double halfWidthMeters, double startMeters, double endMeters, double waterMeters,
        double bedMeters, ulong? ruinSeed = null)
    {
        var mesh = new BridgeMesh();
        bool old = ruinSeed is not null;
        BridgeMaterial stone = old ? BridgeMaterial.Weathered : BridgeMaterial.Stone;
        BridgeMaterial paving = old ? BridgeMaterial.Weathered : BridgeMaterial.Paving;
        double deckHalf = halfWidthMeters, outer = halfWidthMeters + WallThicknessMeters;
        double foot = Math.Min(bedMeters, waterMeters) - FootingMeters;
        double Deck(double x) => startMeters + (endMeters - startMeters)
            * (lengthMeters > 0 ? x / lengthMeters : 0);
        double WallTop(double x) => Deck(x) + WallHeightMeters;

        // An old bridge's sides reach only to its road: its walls are built in pieces.
        double SideTop(double x) => old ? Deck(x) : WallTop(x);

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

            mesh.Quad(new(x0, outer, b0), new(x1, outer, b1), new(x1, outer, SideTop(x1)),
                new(x0, outer, SideTop(x0)), new(0, 1, 0), stone);
            mesh.Quad(new(x1, -outer, b1), new(x0, -outer, b0), new(x0, -outer, SideTop(x0)),
                new(x1, -outer, SideTop(x1)), new(0, -1, 0), stone);
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
                new(x0, outer, b0), normal, stone);
        }

        // The road over it (an old one's out to its sides, under its broken walls), the walls'
        // inner faces and tops, and the ends.
        double road = old ? outer : deckHalf;
        mesh.Quad(new(0, -road, Deck(0)), new(lengthMeters, -road, Deck(lengthMeters)),
            new(lengthMeters, road, Deck(lengthMeters)), new(0, road, Deck(0)),
            new(0, 0, 1), paving);
        foreach (double side in new[] { 1.0, -1.0 })
        {
            if (old)
            {
                BrokenWall(mesh, lengthMeters, side, deckHalf, outer, Deck, ruinSeed!.Value);
                continue;
            }

            double inner = side * deckHalf, edge = side * outer;
            mesh.Quad(new(0, inner, Deck(0)), new(lengthMeters, inner, Deck(lengthMeters)),
                new(lengthMeters, inner, WallTop(lengthMeters)), new(0, inner, WallTop(0)),
                new(0, -side, 0), stone);
            mesh.Quad(new(0, inner, WallTop(0)), new(lengthMeters, inner, WallTop(lengthMeters)),
                new(lengthMeters, edge, WallTop(lengthMeters)), new(0, edge, WallTop(0)),
                new(0, 0, 1), paving);
        }

        mesh.Quad(new(0, outer, foot), new(0, outer, SideTop(0)), new(0, -outer, SideTop(0)),
            new(0, -outer, foot), new(-1, 0, 0), stone);
        mesh.Quad(new(lengthMeters, -outer, foot), new(lengthMeters, -outer, SideTop(lengthMeters)),
            new(lengthMeters, outer, SideTop(lengthMeters)), new(lengthMeters, outer, foot),
            new(1, 0, 0), stone);
        return (mesh.Points, mesh.Triangles);
    }

    // One side's wall on an old bridge (`side` 1: the left): in pieces WallPieceMeters long,
    // some fallen to a stub and some gone, by the seed; the road at `deck` along it.
    private static void BrokenWall(BridgeMesh mesh, double lengthMeters, double side,
        double deckHalf, double outer, Func<double, double> deck, ulong seed)
    {
        (double y0, double y1) = side > 0 ? (deckHalf, outer) : (-outer, -deckHalf);
        int pieces = Math.Max(1, (int)Math.Round(lengthMeters / WallPieceMeters));
        for (int k = 0; k < pieces; k++)
        {
            double chance = (SeededNoise.Hashed(seed, side > 0 ? 1 : 2, k) + 1) / 2;
            if (chance < GoneShare)
            {
                continue;
            }

            double height = chance < GoneShare + FallenShare ? StubMeters : WallHeightMeters;
            double x0 = lengthMeters * k / pieces, x1 = lengthMeters * (k + 1) / pieces;
            mesh.Box(x0, x1, y0, y1, deck(x0), deck(x1), deck(x0) + height, deck(x1) + height,
                BridgeMaterial.Weathered, new Vector3D(1, 0, 0));
        }
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
}
