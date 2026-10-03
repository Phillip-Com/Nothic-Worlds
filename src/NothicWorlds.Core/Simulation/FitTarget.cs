namespace NothicWorlds.Core.Simulation;

/// <summary>Which of a body's values fitting a calendar changes (VISION.md CAL-02).</summary>
public enum FitTarget
{
    /// <summary>How long the body takes to spin, in hours.</summary>
    DayLength,

    /// <summary>How long the body's orbit takes, in standard days.</summary>
    OrbitPeriod,
}
