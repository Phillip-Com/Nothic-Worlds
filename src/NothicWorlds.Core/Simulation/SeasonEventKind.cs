namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The solstices and equinoxes, named for the northern hemisphere (the south has the
/// opposite: the northern summer solstice is the southern winter solstice).
/// </summary>
public enum SeasonEventKind
{
    /// <summary>The star crosses the equator heading north: northern spring begins.</summary>
    NorthernSpringEquinox,

    /// <summary>The star stands farthest north: northern summer begins.</summary>
    NorthernSummerSolstice,

    /// <summary>The star crosses the equator heading south: northern autumn begins.</summary>
    NorthernAutumnEquinox,

    /// <summary>The star stands farthest south: northern winter begins.</summary>
    NorthernWinterSolstice,
}
