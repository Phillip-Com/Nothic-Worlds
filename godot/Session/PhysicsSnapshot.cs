using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Session;

/// <summary>One answer from physics mode's simulation (VISION.md SIM-03).</summary>
/// <param name="Simulation">The simulation it came from (a restart makes a new one).</param>
/// <param name="Positions">
/// Every remaining body's true position, in km from the system's center.
/// </param>
/// <param name="CaughtUp">
/// True if these are for the time asked; false while a long jump is still being worked out.
/// </param>
/// <param name="Collisions">Every collision so far.</param>
public sealed record PhysicsSnapshot(GravitySimulation Simulation,
    IReadOnlyDictionary<Guid, Vector3D> Positions, bool CaughtUp,
    IReadOnlyList<Collision> Collisions);
