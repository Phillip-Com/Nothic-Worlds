namespace NothicWorlds.Core.Model;

/// <summary>
/// What a road is like (VISION.md BOD-13; owner's choices, 2026-10-11): how it's built, so how
/// it finds its way and how it looks up close.
/// </summary>
public enum RoadStyle
{
    /// <summary>
    /// One wide paved road, laid out by a work crew: it keeps a straight, even course,
    /// cutting through hills and bridging rivers in stone.
    /// </summary>
    StoneHighway,

    /// <summary>
    /// Many worn paths that split and rejoin within a band, wandering around steep ground and
    /// thick plants, and meeting at a wooden bridge to cross a river.
    /// </summary>
    DirtPaths,

    /// <summary>Two wheel ruts with grass between, following the lie of the land.</summary>
    CartTrack,

    /// <summary>A ruined highway, its paving broken and overgrown.</summary>
    OldRoad,
}
