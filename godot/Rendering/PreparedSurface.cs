using Godot;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Rendering;

/// <summary>
/// A globe's terrain and height images made ahead, off the main thread (see
/// <see cref="SurfaceImages.Prepare"/>), for <see cref="PlanetSurface.ShowPrepared"/>: every
/// face for a globe with no images yet, or only the faces that differ from what it shows.
/// </summary>
/// <param name="Terrain">The terrain the images show.</param>
/// <param name="TerrainBefore">
/// The terrain the globe showed when they were made, or null if it had no terrain images.
/// </param>
/// <param name="Palette">The colors the far-away images were made in.</param>
/// <param name="TerrainFaces">
/// Each face's terrain codes (null for a face that didn't change), or null for none at all.
/// </param>
/// <param name="FarFaces">Each face's far-away colors, likewise.</param>
/// <param name="Heights">The heights the images show.</param>
/// <param name="HeightsBefore">
/// The heights the globe showed when they were made, or null if it had no height images.
/// </param>
/// <param name="HeightFaces">Each face's heights, likewise.</param>
public sealed record PreparedSurface(TerrainGrid Terrain, TerrainGrid? TerrainBefore,
    byte[] Palette, Image?[]? TerrainFaces, Image?[]? FarFaces, HeightGrid Heights,
    HeightGrid? HeightsBefore, Image?[]? HeightFaces);
