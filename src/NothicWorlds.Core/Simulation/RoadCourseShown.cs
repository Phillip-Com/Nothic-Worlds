using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>A road as it's drawn: the way it takes, and what it's like.</summary>
/// <param name="RoadId">The road.</param>
/// <param name="Style">What it's like.</param>
/// <param name="Kind">Drawn (its points as clicked) or routed (found between them).</param>
/// <param name="WidthMeters">How wide it is (dirt paths: the band they wander within).</param>
/// <param name="Path">The way it takes, as unit directions, first point first.</param>
/// <param name="Problem">
/// Why some of a routed road couldn't be found (that stretch is drawn straight), or null.
/// </param>
public sealed record RoadCourseShown(Guid RoadId, RoadStyle Style, RoadKind Kind,
    double WidthMeters, IReadOnlyList<Vector3D> Path, string? Problem);
