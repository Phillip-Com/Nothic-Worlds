using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// Where a shape sits in its body's own space, measured in the body's radii (the globe is a
/// unit sphere): its middle, and its three axes, each a unit vector.
/// </summary>
/// <param name="Center">The shape's middle.</param>
/// <param name="Across">Its width's direction (east, before the turn).</param>
/// <param name="Up">Its height's direction: straight up from the surface where it's placed.</param>
/// <param name="Along">Its length's direction (north, before the turn).</param>
public sealed record ShapeFrame(Vector3D Center, Vector3D Across, Vector3D Up, Vector3D Along);
