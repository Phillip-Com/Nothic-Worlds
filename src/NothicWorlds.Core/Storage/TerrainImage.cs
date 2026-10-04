using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Storage;

/// <summary>
/// Stores a body's painted terrain (<see cref="TerrainGrid"/>) as a standard 8-bit greyscale
/// PNG image (docs/world-format.md): one pixel per cell, holding its terrain code, with the six
/// cube faces stacked top to bottom.
/// </summary>
internal static class TerrainImage
{
    /// <summary>The image's width: one face across.</summary>
    public const int Width = TerrainGrid.FaceSize;

    /// <summary>The image's height: the six faces stacked.</summary>
    public const int Height = CubeSphere.FaceCount * TerrainGrid.FaceSize;

    /// <summary>
    /// The largest terrain image accepted when reading (guards against damaged or hostile
    /// files). Even random cells compress to less than this.
    /// </summary>
    public const long MaxFileBytes = 16L * 1024 * 1024;

    /// <summary>Encodes a grid as PNG file bytes.</summary>
    public static byte[] Encode(TerrainGrid grid)
    {
        var face = new byte[TerrainGrid.CellsPerFace];
        int loadedFace = -1;
        return GreyscalePng.Encode(Width, Height, 8, (row, pixels) =>
        {
            int faceIndex = row / TerrainGrid.FaceSize;
            if (faceIndex != loadedFace)
            {
                grid.CopyFace(faceIndex, face);
                loadedFace = faceIndex;
            }

            face.AsSpan(row % TerrainGrid.FaceSize * Width, Width).CopyTo(pixels);
        });
    }

    /// <summary>
    /// Decodes PNG file bytes written by <see cref="Encode"/> (or re-saved by an image tool).
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The bytes aren't a PNG, are damaged, or aren't a greyscale terrain image of the right
    /// size. The message says why.
    /// </exception>
    public static TerrainGrid Decode(ReadOnlySpan<byte> file)
    {
        return TerrainGrid.FromCells(GreyscalePng.Decode(file, Width, Height, 8));
    }
}
