namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Two bodies meeting in physics mode (VISION.md SIM-03; owner's choice: the smaller merges into
/// the bigger). Only the simulation changes; the world is untouched.
/// </summary>
/// <param name="TimeDays">When they met, in days on the world clock.</param>
/// <param name="AbsorbedId">The body that was swallowed.</param>
/// <param name="IntoId">The body it merged into.</param>
public sealed record Collision(double TimeDays, Guid AbsorbedId, Guid IntoId);
