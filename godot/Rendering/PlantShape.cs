using Godot;

namespace NothicWorlds.Rendering;

/// <summary>
/// One shape a plant is drawn with (<see cref="PlantModels"/>): its mesh, 1 tall with its base
/// at 0, a simpler one for a tree a little way off, and how far it reaches out from its
/// middle, for its flat picture far off.
/// </summary>
/// <param name="Mesh">The mesh, colored by its vertices.</param>
/// <param name="Simple">The same with far fewer triangles (trees only), or null.</param>
/// <param name="HalfWidth">The farthest it reaches out across, for each unit of height.</param>
public sealed record PlantShape(ArrayMesh Mesh, ArrayMesh? Simple, float HalfWidth);
