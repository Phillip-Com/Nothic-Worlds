using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class StoneBridgeTests
{
    [Theory]
    [InlineData(12, 1)]
    [InlineData(20, 1)]
    [InlineData(45, 3)]
    public void EachArch_IsAtMostItsWidest(double lengthMeters, int arches) =>
        Assert.Equal(arches, StoneBridge.ArchCount(lengthMeters));

    [Fact]
    public void TheRoad_ClearsItsArches()
    {
        // One 20 m arch between 4 m piers: 8 m round, springing 0.3 m over the water.
        Assert.Equal(0.3 + 8 + StoneBridge.CrownMeters, StoneBridge.ClearanceMeters(20), 9);
    }

    [Fact]
    public void ItsStone_ReachesFromBelowTheBedToItsWalls_EachFaceTurnedTheWayItFaces()
    {
        double deck = 10 + StoneBridge.ClearanceMeters(60);
        (List<BridgePoint> points, List<int> triangles) = StoneBridge.Build(60, 4, deck, deck,
            10, 7);

        Assert.Equal(0, triangles.Count % 3);
        Assert.Equal(deck + StoneBridge.WallHeightMeters, points.Max(p => p.Position.Z), 9);
        Assert.Equal(6, points.Min(p => p.Position.Z), 9);  // A meter below the bed
        Assert.Equal(4 + StoneBridge.WallThicknessMeters, points.Max(p => p.Position.Y), 9);
        Assert.InRange(points.Min(p => p.Position.X), 0, 0);
        Assert.InRange(points.Max(p => p.Position.X), 60, 60);
        for (int t = 0; t < triangles.Count; t += 3)
        {
            BridgePoint a = points[triangles[t]], b = points[triangles[t + 1]];
            BridgePoint c = points[triangles[t + 2]];
            Vector3D facing = (b.Position - a.Position).Cross(c.Position - a.Position);
            Assert.True(facing.Dot(a.Normal) > 0);
        }

        // Only faces looking straight up are paved: its road and its walls' tops.
        Assert.All(points.Where(p => p.Material == BridgeMaterial.Paving), p =>
            Assert.Equal(1, p.Normal.Z, 9));
        Assert.Contains(points, p =>
            p.Material == BridgeMaterial.Paving && Math.Abs(p.Position.Z - deck) < 1e-9);
    }

    [Fact]
    public void ItsArches_RiseUnderTheRoad_WithStoneOverEachCrown()
    {
        double deck = 10 + StoneBridge.ClearanceMeters(60);
        (List<BridgePoint> points, _) = StoneBridge.Build(60, 4, deck, deck, 10, 7);

        // The underside's highest points are the arches' crowns.
        double crown = points.Where(p => p.Normal.Z < -0.99).Max(p => p.Position.Z);
        Assert.Equal(deck - StoneBridge.CrownMeters, crown, 6);
    }

    [Fact]
    public void AnOldBridge_IsWeathered_ItsWallsBroken_TheSameEveryTime()
    {
        double deck = 10 + StoneBridge.ClearanceMeters(60);
        (List<BridgePoint> points, List<int> triangles) = StoneBridge.Build(60, 4, deck, deck,
            10, 7, ruinSeed: 42);

        Assert.All(points, p => Assert.Equal(BridgeMaterial.Weathered, p.Material));
        Assert.Equal(0, triangles.Count % 3);
        for (int t = 0; t < triangles.Count; t += 3)
        {
            BridgePoint a = points[triangles[t]], b = points[triangles[t + 1]];
            BridgePoint c = points[triangles[t + 2]];
            Vector3D facing = (b.Position - a.Position).Cross(c.Position - a.Position);
            Assert.True(facing.Dot(a.Normal) > 0);
        }

        // Each side's wall, in pieces: most standing, some fallen to a stub, some gone (each
        // piece a box, with 12 of its points at its top).
        int pieces = (int)Math.Round(60 / StoneBridge.WallPieceMeters);
        foreach (double side in new[] { 1.0, -1.0 })
        {
            int TopsAt(double meters) => points.Count(p => p.Position.Y * side > 4 - 1e-9
                && Math.Abs(p.Position.Z - (deck + meters)) < 1e-9) / 12;
            int standing = TopsAt(StoneBridge.WallHeightMeters), stubs = TopsAt(0.3);
            Assert.InRange(standing, pieces / 2, pieces - 1);
            Assert.InRange(stubs, 1, pieces / 2);
            Assert.True(standing + stubs < pieces);
        }

        // The road runs out under its walls, so where one's gone, there's no hole.
        Assert.Contains(points, p => p.Normal.Z > 0.99 && Math.Abs(p.Position.Z - deck) < 1e-9
            && Math.Abs(p.Position.Y) > 4 + StoneBridge.WallThicknessMeters - 1e-9);

        (List<BridgePoint> again, _) = StoneBridge.Build(60, 4, deck, deck, 10, 7, 42);
        Assert.Equal(points, again);
        (List<BridgePoint> other, _) = StoneBridge.Build(60, 4, deck, deck, 10, 7, 43);
        Assert.NotEqual(points, other);
    }
}
