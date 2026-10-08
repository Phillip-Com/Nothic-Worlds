using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>A point of the strip along a river (see <see cref="RiverBanks"/>).</summary>
/// <param name="Direction">Where it is, as a unit direction.</param>
/// <param name="Meters">The ground's height there, in meters.</param>
/// <param name="Shaped">
/// How far its shading follows its own slope, from 1 (bed and banks) to 0 (the skirt's edge,
/// shaded as the coarser ground around it is).
/// </param>
public readonly record struct BankPoint(Vector3D Direction, double Meters, double Shaped);
