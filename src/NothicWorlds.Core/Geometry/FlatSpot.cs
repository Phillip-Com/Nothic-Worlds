namespace NothicWorlds.Core.Geometry;

/// <summary>
/// A spot on a flat world's surface (VISION.md REN-06; see <see cref="FlatWalk"/>), in the
/// matching globe's radii like <see cref="FlatDisc"/>.
/// </summary>
/// <param name="Face">Which part of the surface it's on.</param>
/// <param name="A">
/// On the top or bottom face, across toward +X; on the rim, the angle around (radians,
/// clockwise from +Z seen from above, as longitude runs).
/// </param>
/// <param name="B">
/// On the top or bottom face, across toward +Z; on the rim, the height from the disc's middle
/// (from −<see cref="FlatDisc.HalfThickness"/> to +<see cref="FlatDisc.HalfThickness"/>).
/// </param>
public readonly record struct FlatSpot(FlatFace Face, double A, double B);
