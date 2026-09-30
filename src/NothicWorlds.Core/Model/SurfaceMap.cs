using NothicWorlds.Core.Maps;

namespace NothicWorlds.Core.Model;

/// <summary>A map image wrapped onto a body's surface.</summary>
public sealed class SurfaceMap
{
    /// <summary>
    /// Name of the image inside the world file, e.g. <c>assets/1a2b….png</c>. The image itself
    /// is the user's original file, unchanged (owner decision).
    /// </summary>
    public required string AssetName { get; init; }

    /// <summary>The map type: how the image wraps onto the globe.</summary>
    public MapProjection Projection { get; set; } = MapProjection.Mercator;
}
