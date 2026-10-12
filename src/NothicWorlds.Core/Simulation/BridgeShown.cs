using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A highway's stone bridge as it's drawn near a first-person eye (VISION.md BOD-13): its
/// stone (<see cref="StoneBridge"/>) in its own frame, and where that frame lies on the body.
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
    /// <summary>The bridge <paramref name="bridge"/> along <paramref name="road"/>.</summary>
    public static BridgeShown Of(RoadProfile road, RoadBridge bridge, double radiusMeters)
    {
        Vector3D start = road.Points[bridge.First], end = road.Points[bridge.Last];
        Vector3D side = ((start + end) * 0.5).Cross(end - start);
        side *= 1 / Math.Max(side.Length, 1e-12);
        double length = Math.Max(bridge.LengthMeters,
            (end - start).Length * radiusMeters);
        (List<BridgePoint> points, List<int> triangles) = StoneBridge.Build(length,
            road.HalfWidthMeters, road.RoadMeters[bridge.First], road.RoadMeters[bridge.Last],
            bridge.WaterMeters, bridge.BedMeters);
        return new BridgeShown(start, end, side, length, radiusMeters, points, triangles);
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
