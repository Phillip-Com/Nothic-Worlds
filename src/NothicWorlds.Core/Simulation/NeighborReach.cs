namespace NothicWorlds.Core.Simulation;

/// <summary>
/// How far a body's pull reaches across the distances from its parent (VISION.md SIM-04): an
/// orbit between <paramref name="InnerKm"/> and <paramref name="OuterKm"/> would be pulled off
/// course by it, so another body fits only outside it.
/// </summary>
/// <param name="BodyId">The body whose pull it is.</param>
/// <param name="InnerKm">The nearest to the parent its pull reaches, in km.</param>
/// <param name="OuterKm">The farthest, in km.</param>
public sealed record NeighborReach(Guid BodyId, double InnerKm, double OuterKm);
