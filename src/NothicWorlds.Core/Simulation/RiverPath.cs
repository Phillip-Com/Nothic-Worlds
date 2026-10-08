using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>A river's worked-out course.</summary>
/// <param name="Points">The cells' middles from the source to the mouth.</param>
/// <param name="ReachesWater">False if it ended before reaching water.</param>
public sealed record RiverPath(IReadOnlyList<Vector3D> Points, bool ReachesWater);
