using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The ground up close as the rivers cut it (<see cref="RiverCarving"/>, VISION.md BOD-11) and
/// the roads then shape it (<see cref="RoadCarving"/>, BOD-13): a road's banks run down into a
/// river's, and under its bridges the river's channel is left as it is. Everything that
/// draws, walks on, or plants the ground up close goes through this, so they all agree.
/// </summary>
public sealed class GroundCarving
{
    private GroundCarving(RiverCarving? rivers, RoadCarving? roads) =>
        (Rivers, Roads) = (rivers, roads);

    /// <summary>The rivers' channels (none: null).</summary>
    public RiverCarving? Rivers { get; }

    /// <summary>The roads' shaping (none: null).</summary>
    public RoadCarving? Roads { get; }

    /// <summary>Whether neither rivers nor roads change the ground.</summary>
    public bool IsEmpty => Rivers is not { IsEmpty: false } && Roads is not { IsEmpty: false };

    /// <summary>
    /// The ground carved by <paramref name="rivers"/> and shaped by <paramref name="roads"/>.
    /// </summary>
    public static GroundCarving Of(RiverCarving? rivers, RoadCarving? roads) =>
        new(rivers is { IsEmpty: false } ? rivers : null,
            roads is { IsEmpty: false } ? roads : null);

    /// <summary>
    /// The ground's height at <paramref name="direction"/> (a unit direction) carved by the
    /// rivers and shaped by the roads, given its height there, <paramref name="groundMeters"/>.
    /// Safe to call from several threads at once.
    /// </summary>
    public double Carve(Vector3D direction, double groundMeters)
    {
        double carved = Rivers?.Carve(direction, groundMeters) ?? groundMeters;
        return Roads?.Shape(direction, carved) ?? carved;
    }

    /// <summary>
    /// For coarser ground with cells <paramref name="cellMeters"/> wide within
    /// <paramref name="radius"/> (in radii) of <paramref name="center"/>: its height at a unit
    /// direction, given the ground's height there, carved (<see cref="Carve"/>) and sunk out of
    /// sight under the strips that draw the rivers and roads finely. Null if neither comes
    /// near. Safe to call from several threads at once.
    /// </summary>
    public Func<Vector3D, double, double>? UnderStripNear(Vector3D center, double radius,
        double cellMeters)
    {
        Func<Vector3D, double, (double Carved, double Sink)>? rivers =
            Rivers?.PartsNear(center, radius, cellMeters);
        Func<Vector3D, double, (double Shaped, double Sink)>? roads =
            Roads?.PartsNear(center, radius, cellMeters);
        if (rivers is null && roads is null)
        {
            return null;
        }

        return (direction, groundMeters) =>
        {
            (double carved, double riverSink) = rivers?.Invoke(direction, groundMeters)
                ?? (groundMeters, 0);
            (double shaped, double roadSink) = roads?.Invoke(direction, carved) ?? (carved, 0);
            return shaped - riverSink - roadSink;
        };
    }

    /// <summary>
    /// How far a unit direction is past the nearest river's banks or road's sides, in meters
    /// (0 on them), looking no farther than <paramref name="withinMeters"/> (which it gives if
    /// none is nearer): rough terrain fades out toward them (VISION.md BOD-12), and plants
    /// don't grow on them. A tunnel's road counts only with <paramref name="tunnels"/> (rough
    /// terrain fades over it, so the hill covers it as its profile was laid for, but plants
    /// grow there). Safe to call from several threads at once.
    /// </summary>
    public double BeyondBanksMeters(Vector3D direction, double withinMeters,
        bool tunnels = true)
    {
        double nearest = Rivers?.BeyondBanksMeters(direction, withinMeters) ?? withinMeters;
        return nearest > 0 && Roads is { } roads
            ? roads.BeyondSidesMeters(direction, nearest, tunnels)
            : nearest;
    }

    /// <summary>
    /// The height of a bridge's road at a unit direction, in meters, if one is there; else
    /// null.
    /// </summary>
    public double? DeckMeters(Vector3D direction) => Roads?.DeckMeters(direction);

    /// <summary>
    /// The height of the road in a tunnel at a unit direction, in meters, if one is there;
    /// else null (<see cref="RoadCarving.TunnelFloorMeters"/>).
    /// </summary>
    public double? TunnelFloorMeters(Vector3D direction) =>
        Roads?.TunnelFloorMeters(direction);
}
