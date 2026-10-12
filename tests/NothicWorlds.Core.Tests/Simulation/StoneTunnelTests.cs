using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class StoneTunnelTests
{
    private const double Inner = 5, Road = 100;

    // The pieces of a vault 100 m long, 10 m each, as a road's tunnel lays them.
    private static List<(double Along, List<BridgePoint> Points, List<int> Triangles)> Pieces()
    {
        var pieces = new List<(double, List<BridgePoint>, List<int>)>();
        for (double along = 0; along < 100; along += 10)
        {
            (List<BridgePoint> points, List<int> triangles) = StoneTunnel.Vault(10, Inner, Road,
                Road, along, 100);
            pieces.Add((along, points, triangles));
        }

        return pieces;
    }

    [Fact]
    public void ItsVault_FacesIn_EachFaceTurnedTheWayItFaces()
    {
        foreach ((double along, List<BridgePoint> points, List<int> triangles) in Pieces())
        {
            for (int t = 0; t < triangles.Count; t += 3)
            {
                BridgePoint a = points[triangles[t]], b = points[triangles[t + 1]];
                BridgePoint c = points[triangles[t + 2]];
                Vector3D facing = (b.Position - a.Position).Cross(c.Position - a.Position);
                Assert.True(facing.Dot(a.Normal) > 0);
            }

            // The walls and arch face the middle of the vault (between the torches, 37.5 m and
            // 62.5 m along, whose brackets stand out from them).
            if (along != 40)
            {
                continue;
            }

            Assert.All(points.Where(p => p.Material == BridgeMaterial.Stone
                && Math.Abs(p.Position.Y) >= Inner - 1e-9), p =>
            {
                var toMiddle = new Vector3D(0, -p.Position.Y,
                    Math.Max(Road + StoneTunnel.SpringMeters - p.Position.Z, 0));
                Assert.True(p.Normal.Dot(toMiddle) > 0);
            });
            Assert.Equal(Road + StoneTunnel.CrownMeters(Inner),
                points.Where(p => p.Material == BridgeMaterial.Stone).Max(p => p.Position.Z), 9);
        }
    }

    [Fact]
    public void ItsTorches_AreSetAlongIt_EachOnce_OnAlternateSides()
    {
        // Each flame is a box of six faces of four points.
        List<(double Along, double Side)> flames = [.. Pieces().SelectMany(piece =>
            piece.Points.Where(p => p.Material == BridgeMaterial.Flame)
                .Chunk(24)
                .Select(box => (piece.Along + box.Average(p => p.Position.X),
                    (double)Math.Sign(box.Average(p => p.Position.Y)))))];

        Assert.Equal([12.5, 37.5, 62.5, 87.5], flames.Select(f => Math.Round(f.Along, 6)));
        Assert.Equal([1.0, -1, 1, -1], flames.Select(f => f.Side));
    }

    [Fact]
    public void ItDarkens_GoingIn_FromEitherPortal()
    {
        Assert.Equal(0, StoneTunnel.DarkAt(0, 100), 9);
        Assert.Equal(0, StoneTunnel.DarkAt(100, 100), 9);
        Assert.Equal(StoneTunnel.DarkAt(20, 100), StoneTunnel.DarkAt(80, 100), 9);
        Assert.True(StoneTunnel.DarkAt(10, 100) < StoneTunnel.DarkAt(20, 100));
        Assert.InRange(StoneTunnel.DarkAt(50, 100), 0.85, 0.95);

        // Its pieces take it on: none at a portal, and the most in the middle.
        Assert.All(Pieces()[5].Points.Where(p => p.Position.X == 0), p =>
            Assert.True(p.Dark > 0.85));
        Assert.All(Pieces()[0].Points.Where(p => p.Position.X == 0), p =>
            Assert.Equal(0, p.Dark, 9));
    }

    [Fact]
    public void APortalsFront_IsOpenOntoTheVault_AndClosesTheCuttingAroundIt()
    {
        (List<BridgePoint> points, List<int> triangles) = StoneTunnel.Portal(Inner, Road, 20);
        bool Covered(double y, double z)
        {
            for (int t = 0; t < triangles.Count; t += 3)
            {
                BridgePoint a = points[triangles[t]], b = points[triangles[t + 1]];
                BridgePoint c = points[triangles[t + 2]];
                if (a.Normal.X < -0.99 && Inside(y, z, a.Position, b.Position, c.Position))
                {
                    return true;
                }
            }

            return false;
        }

        double crown = Road + StoneTunnel.CrownMeters(Inner);
        double height = StoneTunnel.FrontHeightMeters(Inner, 20);
        Assert.False(Covered(0, Road + StoneTunnel.SpringMeters));
        Assert.False(Covered(Inner - 0.5, Road + 1));
        Assert.True(Covered(0, crown + 1));
        Assert.True(Covered(Inner + 0.5, Road + 1));
        Assert.True(Covered(-(Inner + height - 1), Road + height - 0.5));
        Assert.Equal(Road + height, points.Max(p => p.Position.Z), 9);
    }

    [Fact]
    public void APortalsFront_StandsALittleOverTheGround_UnlessAtACliff()
    {
        double cover = StoneTunnel.CoverMeters(Inner);
        Assert.Equal(14, StoneTunnel.FrontHeightMeters(Inner, 12), 9);
        Assert.Equal(cover, StoneTunnel.FrontHeightMeters(Inner, 1), 9);
        Assert.Equal(cover + StoneTunnel.MostOverCoverMeters,
            StoneTunnel.FrontHeightMeters(Inner, 150), 9);
    }

    // Whether (y, z) lies inside the triangle's corners seen along x.
    private static bool Inside(double y, double z, Vector3D a, Vector3D b, Vector3D c)
    {
        static double Side(double y, double z, Vector3D p, Vector3D q) =>
            (q.Y - p.Y) * (z - p.Z) - (q.Z - p.Z) * (y - p.Y);
        double ab = Side(y, z, a, b), bc = Side(y, z, b, c), ca = Side(y, z, c, a);
        return (ab >= 0 && bc >= 0 && ca >= 0) || (ab <= 0 && bc <= 0 && ca <= 0);
    }
}
