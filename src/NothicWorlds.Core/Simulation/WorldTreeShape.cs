using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The shape a world tree grows from its settings (VISION.md BOD-02): a trunk, great branches
/// that each end in a tip able to hold a realm, smaller twigs, and roots, all as straight pieces.
/// Deterministic: the same settings always grow the same tree.
/// </summary>
/// <remarks>
/// Sizes are in the tree's radius (half its height), in its own space: the trunk stands along
/// +Y, the roots reach down to about −1 and the crown up to about +1, and nothing reaches
/// farther than 1 from the center. The tree turns about its trunk (its day length).
/// </remarks>
public static class WorldTreeShape
{
    private const double TrunkBottom = -0.5;
    private const double TrunkTop = 0.35;
    private const double TrunkRadius = 0.07;

    /// <summary>Grows the tree.</summary>
    public static GrownTree Grow(WorldTreeLook look)
    {
        var random = new SeededRandom(unchecked((ulong)look.Seed), (ulong)look.Branches);
        var pieces = new List<TreePiece>
        {
            new(new Vector3D(0, TrunkBottom, 0), new Vector3D(0, TrunkTop, 0), TrunkRadius,
                TrunkRadius * 0.6),
        };
        var tips = new List<Vector3D>();
        var leaves = new List<Vector3D>();
        double turn = random.Next() * 360;
        for (int i = 0; i < look.Branches; i++)
        {
            // Spread round the trunk by the golden angle, a little jittered, from low on the
            // crown to its top.
            double around = double.DegreesToRadians(turn + i * 137.5 + random.Between(-15, 15));
            double height = TrunkTop * (0.15 + 0.85 * (i + random.Next()) / look.Branches);
            double rise = double.DegreesToRadians(random.Between(20, 50));
            double length = look.Spread * random.Between(0.5, 0.7);
            var outward = new Vector3D(Math.Sin(around), 0, Math.Cos(around));
            var start = new Vector3D(0, height, 0);
            Vector3D bend = Within(start + (outward * Math.Cos(rise * 0.6)
                + new Vector3D(0, Math.Sin(rise * 0.6), 0)) * (length * 0.5));
            Vector3D tip = Within(bend + (outward * Math.Cos(rise)
                + new Vector3D(0, Math.Sin(rise), 0)) * (length * 0.5));
            pieces.Add(new TreePiece(start, bend, TrunkRadius * 0.5, TrunkRadius * 0.3));
            pieces.Add(new TreePiece(bend, tip, TrunkRadius * 0.3, TrunkRadius * 0.12));
            tips.Add(tip);
            leaves.Add(tip);

            // Two twigs from the bend, for a fuller crown (they hold no realms).
            for (int twig = 0; twig < 2; twig++)
            {
                double side = around + (twig == 0 ? 1 : -1) * random.Between(0.5, 0.9);
                var twigWay = new Vector3D(Math.Sin(side), random.Between(0.2, 0.6),
                    Math.Cos(side));
                Vector3D twigEnd = Within(bend + twigWay * (1 / twigWay.Length)
                    * (length * random.Between(0.25, 0.4)));
                pieces.Add(new TreePiece(bend, twigEnd, TrunkRadius * 0.15, TrunkRadius * 0.06));
                leaves.Add(twigEnd);
            }
        }

        int roots = 3 + (int)(random.Next() * 3);
        for (int i = 0; i < roots; i++)
        {
            double around = double.DegreesToRadians(turn + 60 + i * 360.0 / roots
                + random.Between(-20, 20));
            var outward = new Vector3D(Math.Sin(around), 0, Math.Cos(around));
            var start = new Vector3D(0, TrunkBottom + 0.05, 0);
            Vector3D end = Within(new Vector3D(0, random.Between(-0.95, -0.8), 0)
                + outward * random.Between(0.35, 0.55));
            pieces.Add(new TreePiece(start, end, TrunkRadius * 0.45, TrunkRadius * 0.1));
        }

        return new GrownTree(pieces, tips, leaves);
    }

    // Keeps a point within the tree's radius.
    private static Vector3D Within(Vector3D point) =>
        point.Length > 0.98 ? point * (0.98 / point.Length) : point;
}
