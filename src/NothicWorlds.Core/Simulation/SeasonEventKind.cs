namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The solstices and equinoxes, named for the northern hemisphere (the south has the
/// opposite: the northern summer solstice is the southern winter solstice). A flat world has
/// no hemispheres: its year turns on <see cref="Midsummer"/> and <see cref="Midwinter"/>.
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

    /// <summary>
    /// On a flat world (VISION.md BOD-02), the star climbs highest at noon (overhead): it lies
    /// in the plane the disc spins in, as at a globe's equinox. Summer begins.
    /// </summary>
    Midsummer,

    /// <summary>
    /// On a flat world, the star's noon height is lowest: it's farthest from the plane the disc
    /// spins in, as at a globe's solstice. Winter begins.
    /// </summary>
    Midwinter,
}
