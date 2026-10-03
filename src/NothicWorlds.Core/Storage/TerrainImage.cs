using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Storage;

/// <summary>
/// Stores a body's painted terrain (<see cref="TerrainGrid"/>) as a standard 8-bit greyscale
/// PNG image (docs/world-format.md): one pixel per cell, holding its terrain code, with the six
/// cube faces stacked top to bottom. Any image tool can open it, and any program can read it
/// without Godot.
/// </summary>
/// <remarks>
/// Only the PNG features needed for this are supported: other kinds of PNG are refused as
/// damaged. Kept small and self-contained so Core needs no image library.
/// </remarks>
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

    private static readonly byte[] _signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] _crcTable = CreateCrcTable();

    /// <summary>Encodes a grid as PNG file bytes.</summary>
    public static byte[] Encode(TerrainGrid grid)
    {
        using var file = new MemoryStream();
        file.Write(_signature);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), Height);
        header[8] = 8;  // Bits per pixel
        header[9] = 0;  // Greyscale; bytes 10 to 12 (compression, filter, interlace) stay 0
        WriteChunk(file, "IHDR", header);

        WriteChunk(file, "IDAT", CompressRows(grid));
        WriteChunk(file, "IEND", []);
        return file.ToArray();
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
        if (!file.StartsWith(_signature))
        {
            throw new InvalidDataException("it isn't a PNG image");
        }

        using var compressed = new MemoryStream();
        bool sawHeader = false;
        int position = _signature.Length;
        while (true)
        {
            string type = ReadChunk(file, ref position, out ReadOnlySpan<byte> data);
            if (!sawHeader)
            {
                Require(type == "IHDR", "it has no image header");
                CheckHeader(data);
                sawHeader = true;
            }
            else if (type == "IDAT")
            {
                compressed.Write(data);
            }
            else if (type == "IEND")
            {
                break;
            }
            else
            {
                // Other chunks (e.g. a note an image tool added) are fine to skip, unless the
                // PNG rules say a reader must understand them (an uppercase first letter).
                Require(char.IsLower(type[0]), $"it uses an unsupported PNG feature ({type})");
            }
        }

        Require(compressed.Length > 0, "it has no pixels");
        compressed.Position = 0;
        return TerrainGrid.FromCells(Unfilter(ReadRows(compressed)));
    }

    // Each row is a filter byte (0: stored as-is) and then the row's codes, all compressed.
    private static byte[] CompressRows(TerrainGrid grid)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            var face = new byte[TerrainGrid.CellsPerFace];
            for (int index = 0; index < CubeSphere.FaceCount; index++)
            {
                grid.CopyFace(index, face);
                for (int row = 0; row < TerrainGrid.FaceSize; row++)
                {
                    zlib.WriteByte(0);
                    zlib.Write(face, row * Width, Width);
                }
            }
        }

        return output.ToArray();
    }

    private static void CheckHeader(ReadOnlySpan<byte> header)
    {
        Require(header.Length == 13, "its image header is damaged");
        int width = BinaryPrimitives.ReadInt32BigEndian(header);
        int height = BinaryPrimitives.ReadInt32BigEndian(header[4..]);
        Require(width == Width && height == Height,
            $"it's {width} × {height} pixels, not {Width} × {Height}");
        Require(header[8] == 8 && header[9] == 0, "it isn't an 8-bit greyscale image");
        Require(header[10] == 0 && header[11] == 0 && header[12] == 0,
            "it uses an unsupported PNG feature (compression, filtering, or interlacing)");
    }

    private static byte[] ReadRows(Stream compressed)
    {
        // Exactly one filter byte plus one row of codes per row; anything else is damage.
        var rows = new byte[Height * (Width + 1)];
        try
        {
            using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
            zlib.ReadExactly(rows);
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException("its pixels are cut short");
        }

        return rows;
    }

    // Undoes the PNG row filters (each row may predict its bytes from the left and above).
    private static byte[] Unfilter(byte[] rows)
    {
        var cells = new byte[TerrainGrid.CellCount];
        for (int row = 0; row < Height; row++)
        {
            byte filter = rows[row * (Width + 1)];
            ReadOnlySpan<byte> line = rows.AsSpan(row * (Width + 1) + 1, Width);
            Span<byte> current = cells.AsSpan(row * Width, Width);
            ReadOnlySpan<byte> above = row == 0
                ? new byte[Width]
                : cells.AsSpan((row - 1) * Width, Width);
            for (int x = 0; x < Width; x++)
            {
                int left = x == 0 ? 0 : current[x - 1];
                int up = above[x];
                int upLeft = x == 0 ? 0 : above[x - 1];
                int prediction = filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException("its pixels are damaged"),
                };
                current[x] = (byte)(line[x] + prediction);
            }
        }

        return cells;
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        int estimate = left + up - upLeft;
        int toLeft = Math.Abs(estimate - left);
        int toUp = Math.Abs(estimate - up);
        int toUpLeft = Math.Abs(estimate - upLeft);
        if (toLeft <= toUp && toLeft <= toUpLeft)
        {
            return left;
        }

        return toUp <= toUpLeft ? up : upLeft;
    }

    private static void WriteChunk(Stream file, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        file.Write(number);

        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        file.Write(typeBytes);
        file.Write(data);

        BinaryPrimitives.WriteUInt32BigEndian(number, Crc(typeBytes, data));
        file.Write(number);
    }

    // Reads the chunk at `position` (checking its checksum), returns its type, and moves past.
    private static string ReadChunk(
        ReadOnlySpan<byte> file, ref int position, out ReadOnlySpan<byte> data)
    {
        Require(file.Length - position >= 12, "it's cut short");
        uint length = BinaryPrimitives.ReadUInt32BigEndian(file[position..]);
        Require(length <= file.Length - position - 12, "it's cut short");

        ReadOnlySpan<byte> typeBytes = file.Slice(position + 4, 4);
        foreach (byte letter in typeBytes)
        {
            Require(char.IsAsciiLetter((char)letter), "it's damaged");
        }

        data = file.Slice(position + 8, (int)length);
        uint crc = BinaryPrimitives.ReadUInt32BigEndian(file[(position + 8 + (int)length)..]);
        Require(crc == Crc(typeBytes, data), "it's damaged (a checksum doesn't match)");

        position += 12 + (int)length;
        return Encoding.ASCII.GetString(typeBytes);
    }

    // The CRC-32 checksum PNG uses, over a chunk's type and data.
    private static uint Crc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte value in type)
        {
            crc = _crcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        foreach (byte value in data)
        {
            crc = _crcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFF;
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < 256; index++)
        {
            uint value = index;
            for (int bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }

    private static void Require(bool condition, string problem)
    {
        if (!condition)
        {
            throw new InvalidDataException(problem);
        }
    }
}
