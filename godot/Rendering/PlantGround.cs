using Godot;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Rendering;

/// <summary>
/// What plants are placed against while standing (<see cref="StandingPlants"/>): the ground
/// drawn, where it's wet or snowy, and what grows where. Read on worker threads, so every
/// function must be safe off the main one, and keep giving the same answers.
/// </summary>
/// <param name="Version">Names the answers given: plants are placed again when it changes.
/// </param>
/// <param name="CoverVersion">Names what grows where: each square's plants are chosen again
/// when it changes.</param>
/// <param name="Height">The drawn ground's height at a base point (as the
/// <see cref="ITileSurface"/> measures height), or null where none is drawn.</param>
/// <param name="InRiver">Whether a base point is in a river or on its banks.</param>
/// <param name="Wet">Whether a base point, given its ground's height, is under water.</param>
/// <param name="Snowy">Whether a base point, given its ground's height, is this many meters
/// below the snow line or higher.</param>
/// <param name="CoverAt">What grows at a base point, and the color of its short grass.</param>
public sealed record PlantGround(long Version, long CoverVersion,
    Func<Vector3D, double?> Height,
    Func<Vector3D, bool> InRiver,
    Func<Vector3D, double, bool> Wet,
    Func<Vector3D, double, double, bool> Snowy,
    Func<Vector3D, (PlantCover Cover, Color Grass)> CoverAt);
