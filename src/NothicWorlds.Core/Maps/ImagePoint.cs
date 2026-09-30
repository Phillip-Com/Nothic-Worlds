namespace NothicWorlds.Core.Maps;

/// <summary>A point on an image, as fractions of its size: 0–1 from the top-left.</summary>
/// <param name="U">Horizontal position, 0 (left) to 1 (right).</param>
/// <param name="V">Vertical position, 0 (top) to 1 (bottom).</param>
public readonly record struct ImagePoint(double U, double V);
