namespace NothicWorlds.Core.Simulation;

/// <summary>Whose light an eclipse takes away, from the selected body's point of view.</summary>
public enum EclipseKind
{
    /// <summary>
    /// A moon passes in front of the star, as seen from its planet (the selected body, or the
    /// selected moon's planet).
    /// </summary>
    Solar,

    /// <summary>A moon (or the selected moon itself) passes through its planet's shadow.</summary>
    Lunar,
}
