using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A stone highway's tunnel (VISION.md BOD-13; owner's choices, 2026-10-12): a paved floor
/// under a round stone vault, lit by torches that glow (<see cref="TorchMeters"/> apart, on
/// alternate sides) but light nothing, darker the farther it is from its portals; and at each
/// portal, a stone front with a round arch across the end of the cutting, built out as a block
/// over the hill's face. Built in pieces, each in its own frame as a bridge is
/// (<see cref="BridgePoint"/>: x along the road into the tunnel, y across it to the left, z up,
/// heights as the ground's). An old road's tunnel (owner's choice, 2026-10-12) is the same of
/// weathered stone, with no torches.
/// </summary>
public static class StoneTunnel
{
    /// <summary>How high the vault's walls rise before its arch springs, in meters.</summary>
    public const double SpringMeters = 4;

    /// <summary>How far apart the torches are along a tunnel, in meters.</summary>
    public const double TorchMeters = 25;

    /// <summary>
    /// How far a portal's front stands out in front of it, and how far its block reaches back
    /// over the tunnel, in meters.
    /// </summary>
    public const double FrontMeters = 1, BlockMeters = 6;

    /// <summary>
    /// How far in front of a portal, and behind its front (under its block), the coarser ground
    /// is sunk out of sight, in meters (in front, the cutting's fine strips cover it), so the
    /// arch opens onto the vault and none of the hill's face shows past the front.
    /// </summary>
    public const double HiddenInFrontMeters = 20, HiddenBehindMeters = BlockMeters - 1;

    /// <summary>How far under the road there the coarser ground is sunk, in meters.</summary>
    public const double HiddenBelowMeters = 2;

    /// <summary>
    /// The most a portal's front stands over what covers the vault, in meters.
    /// </summary>
    public const double MostOverCoverMeters = 8;

    // How far a portal's front reaches into the cutting's sides, and over the ground, in meters.
    private const double EmbedMeters = 1, OverGroundMeters = 2;

    // How far each piece of the vault reaches past its ends, so bends leave no gaps, in meters.
    private const double JointMeters = 0.3;

    // How high the torches are over the floor, how far they stand off the wall, and their
    // flames' size, in meters.
    private const double TorchHeightMeters = 2.5, TorchOutMeters = 0.25, FlameMeters = 0.2;

    // How fast it darkens going in (meters for most of the light to go), and the darkest it is.
    private const double DarkeningMeters = 12, Darkest = 0.92;

    // How many steps round the arch.
    private const int ArchSteps = 12;

    /// <summary>How high the crown of the vault is over the road, in meters.</summary>
    public static double CrownMeters(double innerHalfMeters) => SpringMeters + innerHalfMeters;

    /// <summary>
    /// The least ground over the road that covers a tunnel this wide inside (half of it,
    /// <paramref name="innerHalfMeters"/>), in meters.
    /// </summary>
    public static double CoverMeters(double innerHalfMeters) =>
        CrownMeters(innerHalfMeters) + 2;

    /// <summary>
    /// How dark a tunnel <paramref name="lengthMeters"/> long is
    /// <paramref name="alongMeters"/> from its first portal (<see cref="BridgePoint.Dark"/>).
    /// </summary>
    public static double DarkAt(double alongMeters, double lengthMeters)
    {
        double fromPortal = Math.Max(0, Math.Min(alongMeters, lengthMeters - alongMeters));
        return Darkest * (1 - Math.Exp(-fromPortal / DarkeningMeters));
    }

    /// <summary>
    /// How high a portal's front stands over the road, in meters, where the ground is
    /// <paramref name="depthMeters"/> over it: a little over the ground, but no higher than
    /// <see cref="MostOverCoverMeters"/> over what covers the vault (at a cliff, the rock rises
    /// on over it).
    /// </summary>
    public static double FrontHeightMeters(double innerHalfMeters, double depthMeters)
    {
        double cover = CoverMeters(innerHalfMeters);
        return Math.Clamp(depthMeters + OverGroundMeters, cover, cover + MostOverCoverMeters);
    }

    /// <summary>
    /// How far a portal's front reaches out to each side at its top, in meters (it closes the
    /// cutting's end, whose sides rise at <see cref="RoadCarving.CutSlope"/>).
    /// </summary>
    public static double FrontHalfWidthMeters(double innerHalfMeters, double heightMeters) =>
        innerHalfMeters + EmbedMeters + heightMeters / RoadCarving.CutSlope;

    /// <summary>
    /// How far out to each side of a portal the coarser ground is sunk out of sight
    /// (<see cref="HiddenInFrontMeters"/>), in meters, for a tunnel
    /// <paramref name="innerHalfMeters"/> from its middle to each wall, with the ground
    /// <paramref name="depthMeters"/> over its road there: a little less than its front's
    /// reach, so its block covers the edge.
    /// </summary>
    public static double HiddenHalfWidthMeters(double innerHalfMeters, double depthMeters) =>
        FrontHalfWidthMeters(innerHalfMeters, FrontHeightMeters(innerHalfMeters, depthMeters))
        - EmbedMeters;

    /// <summary>
    /// Whether coarser ground <paramref name="intoMeters"/> into a tunnel from a portal (less
    /// than 0: in front of it) and <paramref name="awayMeters"/> from the road's middle is sunk
    /// out of sight (<see cref="HiddenInFrontMeters"/>).
    /// </summary>
    public static bool Hides(double intoMeters, double awayMeters, double innerHalfMeters,
        double depthMeters) =>
        intoMeters >= -HiddenInFrontMeters && intoMeters <= HiddenBehindMeters
        && awayMeters <= HiddenHalfWidthMeters(innerHalfMeters, depthMeters);

    /// <summary>
    /// A piece of a tunnel's vault <paramref name="lengthMeters"/> long, the road in it at
    /// <paramref name="startMeters"/> at its start and <paramref name="endMeters"/> at its end,
    /// <paramref name="innerHalfMeters"/> from the middle to each wall;
    /// <paramref name="alongMeters"/> from the tunnel's first portal in a tunnel
    /// <paramref name="tunnelMeters"/> long (for how dark it is, and where its torches are);
    /// an <paramref name="old"/> road's weathered, without torches.
    /// </summary>
    public static (List<BridgePoint> Points, List<int> Triangles) Vault(double lengthMeters,
        double innerHalfMeters, double startMeters, double endMeters, double alongMeters,
        double tunnelMeters, bool old = false)
    {
        var mesh = new BridgeMesh
        {
            Dark = point => DarkAt(alongMeters + point.X, tunnelMeters),
        };
        double Floor(double x) => startMeters + (endMeters - startMeters)
            * (lengthMeters > 0 ? x / lengthMeters : 0);

        // Past its ends a little, unless an end is a portal's (which meets the front).
        double x0 = alongMeters > 0 ? -JointMeters : 0;
        double x1 = alongMeters + lengthMeters < tunnelMeters
            ? lengthMeters + JointMeters
            : lengthMeters;
        double inner = innerHalfMeters;
        mesh.Quad(new(x0, -inner, Floor(x0)), new(x1, -inner, Floor(x1)),
            new(x1, inner, Floor(x1)), new(x0, inner, Floor(x0)), new(0, 0, 1),
            BridgeMaterial.Paving);
        Arch(mesh, x0, x1, inner, Floor);
        if (old)
        {
            return Weathered(mesh);
        }

        // The torches whose places along the tunnel fall in this piece.
        double first = Math.Ceiling((alongMeters - TorchMeters / 2) / TorchMeters);
        for (double n = Math.Max(first, 0); ; n++)
        {
            double at = TorchMeters / 2 + n * TorchMeters;
            if (at >= alongMeters + lengthMeters || at > tunnelMeters - TorchMeters / 2)
            {
                break;
            }

            double side = n % 2 == 0 ? 1 : -1;
            Torch(mesh, at - alongMeters, side * inner, side, Floor(at - alongMeters));
        }

        return (mesh.Points, mesh.Triangles);
    }

    /// <summary>
    /// A portal's front and block, for a tunnel <paramref name="innerHalfMeters"/> from its
    /// middle to each wall, its road at <paramref name="roadMeters"/>, and the ground
    /// <paramref name="depthMeters"/> over it there: the front faces out of the tunnel (to
    /// less x), its arch open onto the vault, and the block reaches back over the tunnel. An
    /// <paramref name="old"/> road's is weathered.
    /// </summary>
    public static (List<BridgePoint> Points, List<int> Triangles) Portal(
        double innerHalfMeters, double roadMeters, double depthMeters, bool old = false)
    {
        var mesh = new BridgeMesh();
        double inner = innerHalfMeters, spring = SpringMeters;
        double height = FrontHeightMeters(inner, depthMeters);
        double top = roadMeters + height, front = -FrontMeters;
        double Floor(double x) => roadMeters;

        // The front, round its arch: from each step round the arch out to the front's edge,
        // straight away from the arch's middle (and to the front's top corners).
        double half = FrontHalfWidthMeters(inner, height);
        double cornerAngle = Math.Atan2(height - spring, half);
        List<double> angles = [.. Enumerable.Range(0, ArchSteps + 1)
            .Select(step => Math.PI * step / ArchSteps), cornerAngle, Math.PI - cornerAngle];
        angles.Sort();
        (double Y, double Z) Edge(double angle)
        {
            (double cos, double sin) = (Math.Cos(angle), Math.Sin(angle));
            double side = inner + EmbedMeters + spring, reach = double.PositiveInfinity;
            if (cos - sin > 1e-9)
            {
                reach = Math.Min(reach, side / (cos - sin));
            }

            if (-cos - sin > 1e-9)
            {
                reach = Math.Min(reach, side / (-cos - sin));
            }

            if (sin > 1e-9)
            {
                reach = Math.Min(reach, (height - spring) / sin);
            }

            return (reach * cos, spring + reach * sin);
        }

        Vector3D outward = new(-1, 0, 0);
        for (int i = 1; i < angles.Count; i++)
        {
            (double a0, double a1) = (angles[i - 1], angles[i]);
            if (a1 - a0 <= 1e-9)
            {
                continue;
            }

            (double y0, double z0) = Edge(a0);
            (double y1, double z1) = Edge(a1);
            mesh.Quad(new(front, inner * Math.Cos(a0), roadMeters + spring + inner * Math.Sin(a0)),
                new(front, inner * Math.Cos(a1), roadMeters + spring + inner * Math.Sin(a1)),
                new(front, y1, roadMeters + z1), new(front, y0, roadMeters + z0), outward,
                BridgeMaterial.Stone);
        }

        // Beside the arch, under where it springs.
        double foot = inner + EmbedMeters;
        foreach (double side in new[] { 1.0, -1.0 })
        {
            mesh.Quad(new(front, side * inner, roadMeters),
                new(front, side * foot, roadMeters),
                new(front, side * (foot + spring), roadMeters + spring),
                new(front, side * inner, roadMeters + spring), outward, BridgeMaterial.Stone);
        }

        // The block's top, and the arch through its front (the vault starts behind it).
        mesh.Quad(new(front, -half, top), new(0, -half, top), new(0, half, top),
            new(front, half, top), new(0, 0, 1), BridgeMaterial.Paving);
        mesh.Quad(new(0, -half, top), new(BlockMeters, -half, top), new(BlockMeters, half, top),
            new(0, half, top), new(0, 0, 1), BridgeMaterial.Stone);
        mesh.Quad(new(front, half, top), new(BlockMeters, half, top),
            new(BlockMeters, half, top - OverGroundMeters - 1),
            new(front, half, top - OverGroundMeters - 1), new(0, 1, 0), BridgeMaterial.Stone);
        mesh.Quad(new(front, -half, top - OverGroundMeters - 1),
            new(BlockMeters, -half, top - OverGroundMeters - 1),
            new(BlockMeters, -half, top), new(front, -half, top), new(0, -1, 0),
            BridgeMaterial.Stone);
        Arch(mesh, front, 0, inner, Floor);
        return old ? Weathered(mesh) : (mesh.Points, mesh.Triangles);
    }

    // The mesh's points and triangles, all of its stone and paving weathered.
    private static (List<BridgePoint> Points, List<int> Triangles) Weathered(BridgeMesh mesh) =>
        ([.. mesh.Points.Select(point => point with { Material = BridgeMaterial.Weathered })],
            mesh.Triangles);

    // The vault's walls and arch from x0 to x1 over a floor at `floor(x)`, facing in.
    private static void Arch(BridgeMesh mesh, double x0, double x1, double inner,
        Func<double, double> floor)
    {
        double f0 = floor(x0), f1 = floor(x1);
        foreach (double side in new[] { 1.0, -1.0 })
        {
            double y = side * inner;
            mesh.Quad(new(x0, y, f0), new(x1, y, f1), new(x1, y, f1 + SpringMeters),
                new(x0, y, f0 + SpringMeters), new(0, -side, 0), BridgeMaterial.Stone);
        }

        for (int step = 0; step < ArchSteps; step++)
        {
            double a0 = Math.PI * step / ArchSteps, a1 = Math.PI * (step + 1) / ArchSteps;
            double middle = (a0 + a1) / 2;
            Vector3D In(double x, double angle, double f) =>
                new(x, inner * Math.Cos(angle), f + SpringMeters + inner * Math.Sin(angle));
            mesh.Quad(In(x0, a0, f0), In(x1, a0, f1), In(x1, a1, f1), In(x0, a1, f0),
                new(0, -Math.Cos(middle), -Math.Sin(middle)), BridgeMaterial.Stone);
        }
    }

    // A torch on the wall at `wallY` (its side, `side`: 1 left, -1 right), `x` along the
    // piece, over a floor at `floorMeters`: an iron-dark bracket of stone and a glowing flame.
    private static void Torch(BridgeMesh mesh, double x, double wallY, double side,
        double floorMeters)
    {
        double z = floorMeters + TorchHeightMeters, y = wallY - side * TorchOutMeters;
        double h = FlameMeters / 2;
        mesh.Box(x - 0.05, x + 0.05, Math.Min(wallY, y), Math.Max(wallY, y), z - 0.3, z - 0.3,
            z - 0.2, z - 0.2, BridgeMaterial.Stone, new Vector3D(1, 0, 0));
        mesh.Box(x - h, x + h, y - h, y + h, z - 0.2, z - 0.2, z - 0.2 + 2 * FlameMeters,
            z - 0.2 + 2 * FlameMeters, BridgeMaterial.Flame, new Vector3D(1, 0, 0));
    }
}
