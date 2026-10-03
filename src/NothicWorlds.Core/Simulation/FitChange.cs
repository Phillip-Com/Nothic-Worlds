namespace NothicWorlds.Core.Simulation;

/// <summary>
/// One value that fitting the world to a calendar changes (VISION.md CAL-02), for showing
/// before it's applied.
/// </summary>
/// <param name="BodyId">The body whose value changes.</param>
/// <param name="BodyName">Its name.</param>
/// <param name="What">Which value: its day length or its orbit's period.</param>
/// <param name="Before">The value now (hours for a day, standard days for a period).</param>
/// <param name="After">The fitted value.</param>
public sealed record FitChange(
    Guid BodyId, string BodyName, FitTarget What, double Before, double After);
