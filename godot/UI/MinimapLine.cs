using Godot;

namespace NothicWorlds.UI;

/// <summary>
/// A line drawn over the minimap (VISION.md REN-08, BOD-13: the roads), as points from -1 to 1
/// across the map (x east, y north); points beyond it are fine, the line is cut at its edge.
/// </summary>
/// <param name="Points">The line's points, in order.</param>
/// <param name="Color">Its color.</param>
/// <param name="Width">How wide it's drawn, in pixels.</param>
/// <param name="Dashed">Whether it's dashed (dirt paths, as on the globe).</param>
public sealed record MinimapLine(Vector2[] Points, Color Color, float Width, bool Dashed);
