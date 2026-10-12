using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A point of the strip along a river or a road (see <see cref="RiverBanks"/>,
/// <see cref="RoadStrips"/>, <see cref="PathStrands"/>).
/// </summary>
/// <param name="Direction">Where it is, as a unit direction.</param>
/// <param name="Meters">The ground's height there, in meters.</param>
/// <param name="Shaped">
/// How far its shading follows its own slope, from 1 (bed and banks) to 0 (the skirt's edge,
/// shaded as the coarser ground around it is).
/// </param>
/// <param name="Surface">What it is: the ground, a road's paving, or a path's worn ground.</param>
/// <param name="Wear">
/// On worn ground, how worn it is, from 0 (its edges) to 1 (its middle); else 0.
/// </param>
public readonly record struct BankPoint(Vector3D Direction, double Meters, double Shaped,
    StripSurface Surface = StripSurface.Ground, double Wear = 0);
