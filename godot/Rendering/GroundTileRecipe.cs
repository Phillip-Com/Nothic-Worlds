using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Rendering;

/// <summary>
/// How a ground tile is built (<see cref="GroundTiles"/>): its points' heights, and the water's
/// over them. Read on a worker thread, so both must be safe to call off the main one.
/// </summary>
/// <param name="Stamp">Names the heights given: a tile built with another stamp is out of date
/// and is built again (drawn as it was until then).</param>
/// <param name="Height">The ground's height at a base point, as the
/// <see cref="ITileSurface"/> measures height.</param>
/// <param name="Water">The water's surface over a base point, given the ground's height there
/// and the width of the tile's cells (in radii): above the ground where there's water, a
/// little under it (out of sight) where there's none. Null on a body with no water.</param>
/// <param name="MorphHeight">The ground's height at a base point as the tile's parent (twice
/// as coarse) builds it, which the tile morphs toward so it meets coarser tiles beside it
/// without a step: where finer tiles show finer detail (rough terrain, VISION.md BOD-12).
/// Null where every level builds the same height.</param>
public sealed record GroundTileRecipe(long Stamp, Func<Vector3D, double> Height,
    Func<Vector3D, double, double, double>? Water,
    Func<Vector3D, double>? MorphHeight = null);
