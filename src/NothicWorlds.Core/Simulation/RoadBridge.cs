namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A stone bridge along a highway (<see cref="RoadProfile"/>, <see cref="StoneBridge"/>):
/// from the profile's point <paramref name="First"/> to its point <paramref name="Last"/> (the
/// dry ground either side), over water at <paramref name="WaterMeters"/> and a bed at
/// <paramref name="BedMeters"/> (the highest water and lowest bed under it).
/// </summary>
public sealed record RoadBridge(int First, int Last, double WaterMeters, double BedMeters)
{
    /// <summary>How long it is along the road, in meters.</summary>
    public double LengthMeters { get; init; }
}
