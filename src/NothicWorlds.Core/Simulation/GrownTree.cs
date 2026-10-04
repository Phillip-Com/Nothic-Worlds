using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>A grown world tree (see <see cref="WorldTreeShape.Grow"/>).</summary>
/// <param name="Pieces">The trunk, branches, twigs, and roots.</param>
/// <param name="BranchTips">
/// The end of each great branch, in order: where realms can hang (one per branch).
/// </param>
/// <param name="Leaves">Where foliage grows: the branch and twig ends.</param>
public sealed record GrownTree(
    IReadOnlyList<TreePiece> Pieces,
    IReadOnlyList<Vector3D> BranchTips,
    IReadOnlyList<Vector3D> Leaves);
