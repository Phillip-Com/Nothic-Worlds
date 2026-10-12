namespace NothicWorlds.Core.Simulation;

/// <summary>What a road crosses on (VISION.md BOD-13).</summary>
public enum BridgeKind
{
    /// <summary>A stone highway's arch bridge over a river (<see cref="StoneBridge"/>).</summary>
    Stone,

    /// <summary>A dirt path's wooden bridge over a river (<see cref="WoodenBridge"/>).</summary>
    Wooden,

    /// <summary>A dirt path's boardwalk over marsh (<see cref="WoodenBridge"/>).</summary>
    Boardwalk,
}
