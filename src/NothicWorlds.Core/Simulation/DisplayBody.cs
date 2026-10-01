using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Where a body is drawn and how big, in display units (see <see cref="SystemLayout"/>).
/// </summary>
/// <param name="Position">Its center, from the system's center.</param>
/// <param name="Radius">Its radius.</param>
public readonly record struct DisplayBody(Vector3D Position, double Radius);
