using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A point of a stone bridge (<see cref="StoneBridge"/>), in the bridge's own frame, in meters
/// (x along it, y across to the left, z up), with the way its face points and whether that
/// face is paved (the road and the walls' tops) rather than bare stone.
/// </summary>
public readonly record struct BridgePoint(Vector3D Position, Vector3D Normal, bool Paved);
