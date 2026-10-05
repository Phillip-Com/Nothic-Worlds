namespace NothicWorlds.Core.Geometry;

/// <summary>
/// Which part of a flat world's surface a spot is on (see <see cref="FlatWalk"/>).
/// </summary>
public enum FlatFace
{
    /// <summary>The top face, with the map: the north pole at its center.</summary>
    Top,

    /// <summary>The rim: the disc's edge, a wall as tall as the disc is thick.</summary>
    Rim,

    /// <summary>The bare underside.</summary>
    Bottom,
}
