using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>A straight piece of a world tree, tapering from one radius to another.</summary>
/// <param name="From">Where it starts, in the tree's radii.</param>
/// <param name="To">Where it ends.</param>
/// <param name="FromRadius">How thick it is at its start.</param>
/// <param name="ToRadius">How thick it is at its end.</param>
public readonly record struct TreePiece(
    Vector3D From, Vector3D To, double FromRadius, double ToRadius);
