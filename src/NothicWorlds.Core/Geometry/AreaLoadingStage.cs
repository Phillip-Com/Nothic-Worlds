namespace NothicWorlds.Core.Geometry;

/// <summary>
/// What the area round a first-person eye is waiting on (<see cref="AreaLoading"/>), in the
/// order it comes in.
/// </summary>
public enum AreaLoadingStage
{
    /// <summary>The roads are being laid (the ground is shaped to them).</summary>
    Roads,

    /// <summary>The ground round the eye is being built.</summary>
    Ground,

    /// <summary>The rivers' water, beds, and banks, and the roads' paving, are being drawn.</summary>
    Rivers,

    /// <summary>The plants near the eye are being placed.</summary>
    Plants,

    /// <summary>All of it is in.</summary>
    Done,
}
