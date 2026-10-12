namespace NothicWorlds.Core.Simulation;

/// <summary>
/// What a face of a bridge or tunnel is made of, as it's drawn (VISION.md BOD-13).
/// </summary>
public enum BridgeMaterial
{
    /// <summary>A stone bridge's paved road, and its walls' tops.</summary>
    Paving,

    /// <summary>A stone bridge's bare stone.</summary>
    Stone,

    /// <summary>A wooden bridge's or a boardwalk's planks and posts.</summary>
    Wood,

    /// <summary>A tunnel's torches' flames, which glow.</summary>
    Flame,

    /// <summary>An old road's bridge or tunnel's weathered, mossy stone, its road too.</summary>
    Weathered,
}
