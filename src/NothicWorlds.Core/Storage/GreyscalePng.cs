using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace NothicWorlds.Core.Storage;

/// <summary>
/// A small, self-contained reader and writer for standard greyscale PNG images (8 or 16 bits a
/// pixel), so Core needs no image library. Used for painted terrain and sculpted heights
/// (docs/world-format.md): any image tool can open them, and any program can read them without
/// Godot.
/// </summary>
/// <remarks>
/// Only what those images need is supported; other kinds of PNG are refused as damaged. Every
/// row filter is read, so an image re-saved by a paint program still loads.
/// </remarks>
internal static class GreyscalePng
{
    private static readonly byte[] _signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] _crcTable = CreateCrcTable();

    /// <summary>
    /// Encodes an image as PNG file bytes. <paramref name="writeRow"/> fills each row's pixel
    /// bytes, top to bottom (16-bit pixels big-endian, as PNG stores them).
    /// </summary>
    public static byte[] Encode(
        int width, int height, int bitDepth, Action<int, byte[]> writeRow)
    {
        using var file = new MemoryStream();
        file.Write(_signature);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = (byte)bitDepth;
        header[9] = 0;  // Greyscale; bytes 10 to 12 (compression, filter, interlace) stay 0
        WriteChunk(file, "IHDR", header);

        WriteChunk(file, "IDAT", CompressRows(width * bitDepth / 8, height, writeRow));
        WriteChunk(file, "IEND", []);
        return file.ToArray();
    }

    /// <summary>
    /// Decodes PNG file bytes into pixel bytes, row by row from the top (16-bit pixels
    /// big-endian).
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The bytes aren't a PNG, are damaged, or aren't a greyscale image of the given size and
    /// depth. The message says why.
    /// </exception>
    public static byte[] Decode(ReadOnlySpan<byte> file, int width, int height, int bitDepth)
    {
        Require(file.StartsWith(_signature), "it isn't a PNG image");
        using var compressed = new MemoryStream();
        bool sawHeader = false;
        int position = _signature.Length;
        while (true)
        {
            string type = ReadChunk(file, ref position, out ReadOnlySpan<byte> data);
            if (!sawHeader)
            {
                Require(type == "IHDR", "it has no image header");
                CheckHeader(data, width, height, bitDepth);
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
        int rowBytes = width * bitDepth / 8;
        return Unfilter(ReadRows(compressed, rowBytes, height), rowBytes, height, bitDepth / 8);
    }

    // Each row is a filter byte (0: stored as-is) and then the row's pixels, all compressed.
    private static byte[] CompressRows(int rowBytes, int height, Action<int, byte[]> writeRow)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            var row = new byte[rowBytes];
            for (int y = 0; y < height; y++)
            {
                writeRow(y, row);
                zlib.WriteByte(0);
                zlib.Write(row);
            }
        }

        return output.ToArray();
    }

    private static void CheckHeader(ReadOnlySpan<byte> header, int width, int height, int depth)
    {
        Require(header.Length == 13, "its image header is damaged");
        int actualWidth = BinaryPrimitives.ReadInt32BigEndian(header);
        int actualHeight = BinaryPrimitives.ReadInt32BigEndian(header[4..]);
        Require(actualWidth == width && actualHeight == height,
            $"it's {actualWidth} × {actualHeight} pixels, not {width} × {height}");
        Require(header[8] == depth && header[9] == 0, $"it isn't a {depth}-bit greyscale image");
        Require(header[10] == 0 && header[11] == 0 && header[12] == 0,
            "it uses an unsupported PNG feature (compression, filtering, or interlacing)");
    }

    private static byte[] ReadRows(Stream compressed, int rowBytes, int height)
    {
        // Exactly one filter byte plus one row of pixels per row; anything else is damage.
        var rows = new byte[height * (rowBytes + 1)];
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

    // Undoes the PNG row filters (each row may predict its bytes from the pixel to the left,
    // pixelBytes back, and from the row above).
    private static byte[] Unfilter(byte[] rows, int rowBytes, int height, int pixelBytes)
    {
        var pixels = new byte[height * rowBytes];
        var none = new byte[rowBytes];
        for (int row = 0; row < height; row++)
        {
            byte filter = rows[row * (rowBytes + 1)];
            ReadOnlySpan<byte> line = rows.AsSpan(row * (rowBytes + 1) + 1, rowBytes);
            Span<byte> current = pixels.AsSpan(row * rowBytes, rowBytes);
            ReadOnlySpan<byte> above =
                row == 0 ? none : pixels.AsSpan((row - 1) * rowBytes, rowBytes);
            for (int x = 0; x < rowBytes; x++)
            {
                int left = x < pixelBytes ? 0 : current[x - pixelBytes];
                int up = above[x];
                int upLeft = x < pixelBytes ? 0 : above[x - pixelBytes];
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

        return pixels;
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
