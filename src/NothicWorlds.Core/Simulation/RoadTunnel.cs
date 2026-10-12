namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A tunnel along a stone highway (<see cref="RoadProfile"/>; VISION.md BOD-13): from the
/// profile's point <paramref name="First"/> to its point <paramref name="Last"/>, its portals.
/// The road keeps its grade under the hill between them, and the ground over it is left alone.
/// </summary>
public sealed record RoadTunnel(int First, int Last)
{
    /// <summary>How long it is along the road, in meters.</summary>
    public double LengthMeters { get; init; }
}
