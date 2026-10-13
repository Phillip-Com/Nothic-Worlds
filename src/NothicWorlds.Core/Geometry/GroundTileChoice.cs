namespace NothicWorlds.Core.Geometry;

/// <summary>
/// The ground tiles chosen around a first-person eye (<see cref="GroundTileSelection"/>).
/// </summary>
/// <param name="Drawn">The built tiles to draw: together they cover the ground in reach.</param>
/// <param name="Wanted">The tiles to build, most needed first: missing ones (coarsest and
/// nearest first, so the ground fills in quickly and then sharpens), then drawn ones built from
/// out-of-date heights.</param>
/// <param name="Kept">Every tile looked at: drawn, standing in, or on the way to the drawn
/// ones. None of these should be let go from a cache.</param>
/// <param name="NearMissing">The missing tiles not wholly beyond the near reach (every missing
/// tile, with none given): the ground round the eye is in once they're built.</param>
public sealed record GroundTileChoice(IReadOnlyList<GroundTile> Drawn,
    IReadOnlyList<GroundTile> Wanted, IReadOnlySet<GroundTile> Kept,
    IReadOnlyList<GroundTile> NearMissing);
