namespace NothicWorlds.Core.Simulation;

/// <summary>What is falling at a spot right now (VISION.md WTH-02).</summary>
public enum PrecipitationKind
{
    /// <summary>Nothing.</summary>
    None,

    /// <summary>Rain.</summary>
    Rain,

    /// <summary>Snow: the air is at or below freezing.</summary>
    Snow,
}
