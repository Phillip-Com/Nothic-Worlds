namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Where a lake's water lies: its cells (see <see cref="WaterCells"/>), the lowest cell of its
/// shore (where a river flows out), or why it can't be filled.
/// </summary>
/// <param name="Cells">The cells under its water.</param>
/// <param name="Outflow">The lowest cell around it, or null if none (or it can't fill).</param>
/// <param name="Problem">Why the lake has no water, or null.</param>
public sealed record LakeShape(IReadOnlySet<int> Cells, int? Outflow, string? Problem);
