using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>A river as it's drawn: its course from source to mouth, and how wide it is.</summary>
/// <param name="RiverId">The river, or null for a lake's outflow.</param>
/// <param name="LakeId">The lake it flows out of, or null.</param>
/// <param name="Kind">Drawn (its points as clicked) or natural (a cell apart, traced).</param>
/// <param name="WidthKm">How wide it is at its mouth (a fifth of that at its source).</param>
/// <param name="Points">Its course, as unit directions, source first.</param>
/// <param name="ReachesWater">False if it couldn't find its way to water.</param>
/// <param name="Depth">How deep it is (a lake's outflow: Auto).</param>
public sealed record RiverCourseShown(Guid? RiverId, Guid? LakeId, RiverKind Kind,
    double WidthKm, IReadOnlyList<Vector3D> Points, bool ReachesWater,
    RiverDepth? Depth = null);
