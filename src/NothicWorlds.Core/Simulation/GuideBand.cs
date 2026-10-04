namespace NothicWorlds.Core.Simulation;

/// <summary>
/// One ring of the stable orbit guide (VISION.md SIM-04): orbits around
/// <paramref name="CenterId"/> between two sizes, which gravity would keep steady or not.
/// </summary>
/// <param name="CenterId">The body the ring is around.</param>
/// <param name="InnerKm">The ring's inner edge, in km from the body's center.</param>
/// <param name="OuterKm">Its outer edge, in km.</param>
/// <param name="Steady">
/// True where an orbit would stay steady; false where it would be torn apart (inside the Roche
/// limit) or pulled off course (by a neighbor).
/// </param>
public sealed record GuideBand(Guid CenterId, double InnerKm, double OuterKm, bool Steady);
