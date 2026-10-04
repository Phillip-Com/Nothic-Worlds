using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The paths physics mode left bodies on, as designed orbits (VISION.md SIM-03; owner's choice:
/// "Keep as orbits").
/// </summary>
/// <param name="Orbits">The new orbit of each body that can keep one, by body.</param>
/// <param name="NotKept">
/// Why each other orbiting body can't (it was absorbed, or escaped its parent), by body. Those
/// keep their designed orbits.
/// </param>
public sealed record KeptOrbits(
    IReadOnlyDictionary<Guid, Orbit> Orbits, IReadOnlyDictionary<Guid, string> NotKept);
