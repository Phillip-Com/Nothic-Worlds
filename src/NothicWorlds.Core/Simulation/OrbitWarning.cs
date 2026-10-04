namespace NothicWorlds.Core.Simulation;

/// <summary>An orbit real gravity wouldn't keep steady (VISION.md SIM-04).</summary>
/// <param name="BodyId">The body the warning is about.</param>
/// <param name="Message">What's wrong, in plain words, ready to show.</param>
public sealed record OrbitWarning(Guid BodyId, string Message);
