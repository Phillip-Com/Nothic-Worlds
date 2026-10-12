namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A bridge along a road (<see cref="RoadProfile"/>): from the profile's point
/// <paramref name="First"/> to its point <paramref name="Last"/> (the dry ground either side),
/// over water at <paramref name="WaterMeters"/> and a bed at <paramref name="BedMeters"/> (the
/// highest water and lowest bed under it; over marsh, both the ground's lowest).
/// </summary>
public sealed record RoadBridge(int First, int Last, double WaterMeters, double BedMeters)
{
    /// <summary>How long it is along the road, in meters.</summary>
    public double LengthMeters { get; init; }

    /// <summary>What it's like (a stone highway's arches, unless set).</summary>
    public BridgeKind Kind { get; init; }

    /// <summary>
    /// Half its deck's width, in meters; 0 for the road's own (<see cref="RoadProfile"/>).
    /// </summary>
    public double HalfWidthMeters { get; init; }

    /// <summary>How wide the widest water under it is, in meters.</summary>
    public double WaterWidthMeters { get; init; }
}
