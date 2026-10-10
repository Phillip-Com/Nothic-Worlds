namespace NothicWorlds.Core.Model;

/// <summary>
/// How much of one plant grows on a <see cref="PlantCover"/> (<see cref="PlantMix"/>).
/// </summary>
/// <param name="Model">The plant.</param>
/// <param name="PerHectare">How many stand on a hectare (100 × 100 m) of level ground.</param>
/// <param name="MinHeightMeters">The shortest it grows.</param>
/// <param name="MaxHeightMeters">The tallest it grows.</param>
public readonly record struct PlantShare(PlantModel Model, double PerHectare,
    double MinHeightMeters, double MaxHeightMeters);
