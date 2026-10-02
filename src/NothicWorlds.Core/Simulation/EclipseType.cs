namespace NothicWorlds.Core.Simulation;

/// <summary>How deep an eclipse goes (see <see cref="Eclipses"/>).</summary>
public enum EclipseType
{
    /// <summary>
    /// Solar: the star is completely hidden somewhere on the body. Lunar: the whole moon is in
    /// full shadow.
    /// </summary>
    Total,

    /// <summary>
    /// The blocking body looks smaller than the star and passes across its middle, leaving a
    /// ring of light.
    /// </summary>
    Annular,

    /// <summary>
    /// Solar: the star is only partly hidden everywhere. Lunar: part of the moon is in full
    /// shadow.
    /// </summary>
    Partial,

    /// <summary>
    /// Lunar only: the moon passes through the faint outer shadow, never the full one.
    /// </summary>
    Penumbral,
}
