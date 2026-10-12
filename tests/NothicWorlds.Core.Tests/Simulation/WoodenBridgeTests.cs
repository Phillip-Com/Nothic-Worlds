using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class WoodenBridgeTests
{
    [Fact]
    public void ABridge_IsAllWood_ItsDeckOnTop_ItsPostsInTheBed()
    {
        (List<BridgePoint> points, List<int> triangles) = WoodenBridge.Build(18, 1.5, 102, 101,
            95, rails: true);

        Assert.All(points, point => Assert.Equal(BridgeMaterial.Wood, point.Material));
        Assert.Equal(0, triangles.Count % 3);
        Assert.All(triangles, index => Assert.InRange(index, 0, points.Count - 1));

        // The deck's top runs straight from one end to the other; the rails stand over it.
        Assert.Contains(points, p => p.Position.X == 0 && p.Position.Z == 102);
        Assert.Contains(points, p => p.Position.X == 18 && p.Position.Z == 101);
        Assert.Contains(points, p => Math.Abs(p.Position.Z - 102 - WoodenBridge.RailHeightMeters)
            < 1e-9);
        Assert.True(points.Min(p => p.Position.Z) < 95);
        Assert.All(points, p => Assert.InRange(Math.Abs(p.Position.Y), 0, 1.5 + 1e-9));
        Assert.Equal(5, WoodenBridge.BayCount(18));
    }

    [Fact]
    public void ABoardwalk_HasNoRails()
    {
        (List<BridgePoint> points, _) = WoodenBridge.Build(5, 0.75, 100.5, 100.5, 100,
            rails: false);

        Assert.True(points.Max(p => p.Position.Z) <= 100.5 + 1e-9);
        Assert.True(points.Min(p => p.Position.Z) < 100);
    }

    [Fact]
    public void ItsDeck_HasItsGrainAcross_ItsPostsUpright()
    {
        (List<BridgePoint> points, _) = WoodenBridge.Build(8, 1.5, 102, 102, 98, rails: false);

        // On the deck's top, the grain (U) runs across the bridge (along y).
        BridgePoint[] top = [.. points.Where(p => p.Normal.Z > 0.99 && p.Position.Z == 102)];
        Assert.NotEmpty(top);
        Assert.All(top, p => Assert.Equal(p.Position.Y, p.U, 9));
    }
}
