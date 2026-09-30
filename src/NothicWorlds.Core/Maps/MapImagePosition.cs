namespace NothicWorlds.Core.Maps;

/// <summary>
/// A position within a map image, as fractions of its size: U runs 0 (left) to 1 (right) and V
/// runs 0 (top) to 1 (bottom).
/// </summary>
/// <param name="U">Horizontal position, 0 to 1.</param>
/// <param name="V">Vertical position, 0 to 1. Clamped to the image's edge when outside it.</param>
/// <param name="IsOutsideMap">
/// True if the globe position lies beyond what the map covers (e.g. the polar caps of a flat
/// map). <paramref name="V"/> is then the nearest edge, whose pixels are stretched to the pole.
/// </param>
public readonly record struct MapImagePosition(double U, double V, bool IsOutsideMap);
