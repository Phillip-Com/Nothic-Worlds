namespace NothicWorlds.Core.Simulation;

/// <summary>Whose light an eclipse takes away, from the selected body's point of view.</summary>
public enum EclipseKind
{
    /// <summary>A body passes in front of the star, as seen from the selected body.</summary>
    Solar,

    /// <summary>A moon (or the selected moon itself) passes through a body's shadow.</summary>
    Lunar,
}
