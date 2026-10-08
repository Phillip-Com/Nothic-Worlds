namespace NothicWorlds.Core.Simulation;

/// <summary>A step of the clock, measured on a body (see <see cref="TimeSteps"/>).</summary>
public enum TimeUnit
{
    /// <summary>A standard hour.</summary>
    Hour,

    /// <summary>One of the body's days.</summary>
    Day,

    /// <summary>A month of the body's calendar.</summary>
    Month,

    /// <summary>A year of the body's calendar, or one trip round its star without one.</summary>
    Year,
}
