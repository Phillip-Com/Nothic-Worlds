using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A step along a river near a first-person eye (see <see cref="RiverChannels"/>).
/// </summary>
/// <param name="Direction">Where it is, as a unit direction.</param>
/// <param name="AlongMeters">How far it is from the river's source, in meters.</param>
/// <param name="HalfWidthMeters">Half the river's width there, in meters.</param>
/// <param name="SurfaceMeters">The height of its water's surface, in meters.</param>
/// <param name="BedMeters">The height of the river's bed, in meters.</param>
/// <param name="FlowMetersPerSecond">How fast the water flows there.</param>
/// <param name="Rapids">How white with rapids the water is, from 0 (calm) to 1.</param>
public readonly record struct ChannelPoint(Vector3D Direction, double AlongMeters,
    double HalfWidthMeters, double SurfaceMeters, double BedMeters,
    double FlowMetersPerSecond, double Rapids);
