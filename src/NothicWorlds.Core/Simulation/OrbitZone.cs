namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A range of orbit sizes around a body, in km from its center (VISION.md SIM-04).
/// </summary>
/// <param name="InnerKm">The smallest orbit in the range.</param>
/// <param name="OuterKm">The largest, or infinity when nothing limits it.</param>
public sealed record OrbitZone(double InnerKm, double OuterKm);
