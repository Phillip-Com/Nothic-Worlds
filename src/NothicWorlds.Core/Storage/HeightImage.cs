using System.Buffers.Binary;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Storage;

/// <summary>
/// Stores a body's sculpted heights (<see cref="HeightGrid"/>) as a standard 16-bit greyscale
/// PNG image (docs/world-format.md): one pixel per cell, holding its height in meters plus
/// 32,768 (so sea level, 0 m, is mid-grey), with the six cube faces stacked top to bottom, as
/// the terrain image is.
/// </summary>
internal static class HeightImage
{
    /// <summary>What's added to a height to store it (PNG pixels can't be negative).</summary>
    public const int Offset = 32_768;

    /// <summary>The image's width: one face across.</summary>
    public const int Width = HeightGrid.FaceSize;

    /// <summary>The image's height: the six faces stacked.</summary>
    public const int Height = CubeSphere.FaceCount * HeightGrid.FaceSize;

    /// <summary>
    /// The largest height image accepted when reading (guards against damaged or hostile
    /// files): more than the raw pixels, which even random heights compress below.
    /// </summary>
    public const long MaxFileBytes = 32L * 1024 * 1024;

    /// <summary>Encodes a grid as PNG file bytes.</summary>
    public static byte[] Encode(HeightGrid grid)
    {
        var face = new short[HeightGrid.CellsPerFace];
        int loadedFace = -1;
        return GreyscalePng.Encode(Width, Height, 16, (row, pixels) =>
        {
            int faceIndex = row / HeightGrid.FaceSize;
            if (faceIndex != loadedFace)
            {
                grid.CopyFace(faceIndex, face);
                loadedFace = faceIndex;
            }

            int start = row % HeightGrid.FaceSize * Width;
            for (int x = 0; x < Width; x++)
            {
                BinaryPrimitives.WriteUInt16BigEndian(pixels.AsSpan(x * 2),
                    (ushort)(face[start + x] + Offset));
            }
        });
    }

    /// <summary>
    /// Decodes PNG file bytes written by <see cref="Encode"/> (or re-saved by an image tool).
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The bytes aren't a PNG, are damaged, aren't a 16-bit greyscale image of the right size,
    /// or hold a height out of range. The message says why.
    /// </exception>
    public static HeightGrid Decode(ReadOnlySpan<byte> file)
    {
        byte[] pixels = GreyscalePng.Decode(file, Width, Height, 16);
        var cells = new short[HeightGrid.CellCount];
        for (int index = 0; index < cells.Length; index++)
        {
            int stored = BinaryPrimitives.ReadUInt16BigEndian(pixels.AsSpan(index * 2));
            if (stored == 0)
            {
                throw new InvalidDataException(
                    $"it holds a height below {HeightGrid.MinHeightMeters} m");
            }

            cells[index] = (short)(stored - Offset);
        }

        return HeightGrid.FromCells(cells);
    }
}
