namespace NothicWorlds.Core.Model;

/// <summary>How a road finds its course (VISION.md BOD-13).</summary>
public enum RoadKind
{
    /// <summary>It follows the points the user clicked, and stays put.</summary>
    Drawn,

    /// <summary>
    /// It passes through the points the user clicked, finding the easiest way between them for
    /// its style, and finds it again whenever the ground or the water changes.
    /// </summary>
    Routed,
}
