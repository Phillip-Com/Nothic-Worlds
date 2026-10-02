namespace NothicWorlds.Core.Simulation;

/// <summary>One tick mark on a time ruler (see <see cref="TimeRuler"/>).</summary>
/// <param name="TimeDays">Where it falls, in standard days.</param>
/// <param name="Label">What it's labelled, e.g. "14:00", "3 Highsun", "Highsun 1203".</param>
public readonly record struct RulerTick(double TimeDays, string Label);
