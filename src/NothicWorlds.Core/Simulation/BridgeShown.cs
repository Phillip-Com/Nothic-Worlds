using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A road's bridge, or a piece of its tunnel, as it's drawn near a first-person eye (VISION.md
/// BOD-13): its stone (<see cref="StoneBridge"/>, <see cref="StoneTunnel"/>) or wood
/// (<see cref="WoodenBridge"/>) in its own frame, and where that frame lies on the body.
/// </summary>
/// <param name="Start">Where it starts, as a unit direction.</param>
/// <param name="End">Where it ends, as a unit direction.</param>
/// <param name="Side">The unit direction across it, to the left looking from start to end.</param>
/// <param name="LengthMeters">How long it is, in meters.</param>
/// <param name="RadiusMeters">The body's radius, in meters.</param>
/// <param name="Points">Its points, in its own frame.</param>
/// <param name="Triangles">Its triangles, three indices into its points each.</param>
public sealed record BridgeShown(Vector3D Start, Vector3D End, Vector3D Side,
    double LengthMeters, double RadiusMeters, IReadOnlyList<BridgePoint> Points,
    IReadOnlyList<int> Triangles)
{
    /// <summary>
    /// The bridge <paramref name="bridge"/> along <paramref name="road"/>; an
    /// <paramref name="old"/> one's stone weathered and its walls broken.
    /// </summary>
    public static BridgeShown Of(RoadProfile road, RoadBridge bridge, double radiusMeters,
        bool old = false)
    {
        Vector3D start = road.Points[bridge.First], end = road.Points[bridge.Last];
        double length = Math.Max(bridge.LengthMeters,
            (end - start).Length * radiusMeters);
        double from = road.RoadMeters[bridge.First], to = road.RoadMeters[bridge.Last];
        double half = bridge.HalfWidthMeters > 0 ? bridge.HalfWidthMeters : road.HalfWidthMeters;
        (List<BridgePoint> points, List<int> triangles) = bridge.Kind == BridgeKind.Stone
            ? StoneBridge.Build(length, half, from, to, bridge.WaterMeters, bridge.BedMeters,
                old ? SeededNoise.Seed(start) : null)
            : WoodenBridge.Build(length, half, from, to, bridge.BedMeters,
                rails: bridge.Kind == BridgeKind.Wooden);
        return Between(start, end, length, radiusMeters, points, triangles);
    }

    /// <summary>
    /// Points and triangles in a frame from <paramref name="start"/> to
    /// <paramref name="end"/> (unit directions), <paramref name="lengthMeters"/> apart, on a
    /// body <paramref name="radiusMeters"/> in radius.
    /// </summary>
    public static BridgeShown Between(Vector3D start, Vector3D end, double lengthMeters,
        double radiusMeters, IReadOnlyList<BridgePoint> points, IReadOnlyList<int> triangles)
    {
        Vector3D side = ((start + end) * 0.5).Cross(end - start);
        side *= 1 / Math.Max(side.Length, 1e-12);
        return new BridgeShown(start, end, side, lengthMeters, radiusMeters, points, triangles);
    }

    /// <summary>
    /// Where a point of the bridge is: as a unit direction, and its height in meters.
    /// </summary>
    public (Vector3D Direction, double Meters) Place(BridgePoint point)
    {
        double t = LengthMeters > 0 ? point.Position.X / LengthMeters : 0;
        Vector3D direction = Start * (1 - t) + End * t
            + Side * (point.Position.Y / RadiusMeters);
        return (direction * (1 / direction.Length), point.Position.Z);
    }
}
