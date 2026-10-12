using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A point of a bridge (<see cref="StoneBridge"/>, <see cref="WoodenBridge"/>) or a tunnel
/// (<see cref="StoneTunnel"/>), in its own frame, in meters (x along it, y across to the left,
/// z up), with the way its face points, what that face is made of, and, on wood, where it is in
/// the wood's grain (<paramref name="U"/> along the grain and <paramref name="V"/> across it, in
/// meters).
/// </summary>
public readonly record struct BridgePoint(Vector3D Position, Vector3D Normal,
    BridgeMaterial Material, double U = 0, double V = 0)
{
    /// <summary>
    /// How little of the sky's light reaches it, from 0 (all of it) to 1 (none): deep in a
    /// tunnel, it's dark.
    /// </summary>
    public double Dark { get; init; }
}
