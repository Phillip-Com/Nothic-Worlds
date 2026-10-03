namespace NothicWorlds.Core.Model;

/// <summary>
/// How a body's world is kept fitted to its calendar (VISION.md CAL-02; owner's choice: a
/// lasting switch, and the user picks what changes): what's adjusted so a year lasts exactly
/// one calendar year.
/// </summary>
public enum CalendarFit
{
    /// <summary>
    /// Not fitted: the calendar is the user's design and may drift (the default).
    /// </summary>
    None,

    /// <summary>The year changes length: the period of the orbit that makes the year.</summary>
    YearLength,

    /// <summary>The day changes length: how fast the body spins.</summary>
    DayLength,
}
