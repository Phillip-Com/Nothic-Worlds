namespace NothicWorlds.Core.Model;

/// <summary>How a river finds its course (VISION.md BOD-11).</summary>
public enum RiverKind
{
    /// <summary>It follows the points the user clicked, and stays put.</summary>
    Drawn,

    /// <summary>
    /// It starts at its source and runs downhill by the path of least resistance until it
    /// reaches water, finding its course again whenever the ground changes.
    /// </summary>
    Natural,
}
