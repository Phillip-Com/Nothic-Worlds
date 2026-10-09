namespace NothicWorlds.Core.Geometry;

/// <summary>What a <see cref="GroundTile"/> needs before it can be drawn as it should be.</summary>
public enum TileNeed
{
    /// <summary>
    /// Nothing: it's built from the heights as they are now (or is being built again from them).
    /// </summary>
    None,

    /// <summary>Built, but from heights that have changed since: drawn until rebuilt.</summary>
    Stale,

    /// <summary>Not built yet (it may be on its way).</summary>
    Missing,
}
