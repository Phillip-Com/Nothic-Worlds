namespace NothicWorlds.Core.Simulation;

/// <summary>What a point of the fine strips near the eye is (<see cref="BankPoint"/>).</summary>
public enum StripSurface
{
    /// <summary>The ground, as the ground around it is drawn.</summary>
    Ground,

    /// <summary>A stone highway's paving (VISION.md BOD-13).</summary>
    Paving,

    /// <summary>A dirt path's worn ground (VISION.md BOD-13).</summary>
    Worn,
}
