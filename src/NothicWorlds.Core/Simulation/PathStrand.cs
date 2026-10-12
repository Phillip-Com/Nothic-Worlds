using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A piece of one of a dirt path's worn strands (<see cref="DirtPath"/>), lying on the ground.
/// </summary>
/// <param name="Points">Its way, as unit directions, first first.</param>
/// <param name="HalfWidthMeters">Half its worn width, in meters.</param>
/// <param name="Index">
/// Which of the path's strands it's a piece of (0: the main one), so overlapping strands are
/// drawn in a steady order.
/// </param>
public sealed record PathStrand(IReadOnlyList<Vector3D> Points, double HalfWidthMeters,
    int Index);
