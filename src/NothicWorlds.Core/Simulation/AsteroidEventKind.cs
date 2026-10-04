namespace NothicWorlds.Core.Simulation;

/// <summary>
/// What happens when an asteroid from a belt comes by (see <see cref="AsteroidEvent"/>).
/// </summary>
public enum AsteroidEventKind
{
    /// <summary>It passes close by.</summary>
    ClosePass,

    /// <summary>It hits.</summary>
    Impact,
}
