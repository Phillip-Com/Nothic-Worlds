using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A piece of one of a dirt path's worn strands or a cart track's ruts (<see cref="DirtPath"/>),
/// lying on the ground; or, not <see cref="Worn"/>, ground kept clear of plants (between a
/// cart track's ruts).
/// </summary>
/// <param name="Points">Its way, as unit directions, first first.</param>
/// <param name="HalfWidthMeters">Half its worn width, in meters.</param>
/// <param name="Index">
/// Which of the path's strands it's a piece of (0: the main one), so overlapping strands are
/// drawn in a steady order.
/// </param>
public sealed record PathStrand(IReadOnlyList<Vector3D> Points, double HalfWidthMeters,
    int Index)
{
    /// <summary>Whether it's worn (drawn); else only kept clear of plants.</summary>
    public bool Worn { get; init; } = true;

    /// <summary>Whether it's a cart track's rut, pressed harder than a path's strand.</summary>
    public bool Rut { get; init; }
}
