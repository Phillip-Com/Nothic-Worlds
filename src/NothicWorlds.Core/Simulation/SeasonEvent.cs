namespace NothicWorlds.Core.Simulation;

/// <summary>A solstice or equinox (see <see cref="Seasons"/>).</summary>
/// <param name="Kind">Which one, named for the northern hemisphere.</param>
/// <param name="TimeDays">When it happens, in standard days.</param>
public readonly record struct SeasonEvent(SeasonEventKind Kind, double TimeDays);
