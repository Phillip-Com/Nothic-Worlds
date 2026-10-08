using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>Where a lake can sit: the bottom of a hollow, and the height of its brim.</summary>
/// <param name="Spot">The hollow's lowest point, as a unit direction.</param>
/// <param name="LevelMeters">How high its water can rise before spilling out.</param>
public sealed record LakeSeat(Vector3D Spot, int LevelMeters);
