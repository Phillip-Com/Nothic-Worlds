namespace NothicWorlds.Core.Simulation;

/// <summary>
/// How the system view draws sizes and distances (see <see cref="SystemLayout"/>).
/// </summary>
public enum SystemScale
{
    /// <summary>Compressed so everything is visible and clickable (the default).</summary>
    Readable,

    /// <summary>True proportions.</summary>
    True,
}
