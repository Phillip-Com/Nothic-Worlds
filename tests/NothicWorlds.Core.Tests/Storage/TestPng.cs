using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace NothicWorlds.Core.Tests.Storage;

/// <summary>
/// Writes 8- or 16-bit greyscale PNG images the way other image tools might, with any of the
/// five PNG row filters. Written separately from the app's own encoder, so tests can check
/// that the app reads images it didn't write itself.
/// </summary>
internal static class TestPng
{
    /// <summary>
    /// Encodes <paramref name="pixels"/> (row by row; 16-bit pixels big-endian) as a PNG,
    /// filtering every row with <paramref name="filter"/> (0 none, 1 sub, 2 up, 3 average,
    /// 4 Paeth).
    /// </summary>
    public static byte[] Greyscale(
        int width, int height, byte[] pixels, byte filter, int bitDepth = 8)
    {
        int rowBytes = width * bitDepth / 8;
        int back = bitDepth / 8;  // Filters predict from the same byte of the pixel to the left
        using var file = new MemoryStream();
        file.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = (byte)bitDepth;
        WriteChunk(file, "IHDR", header);

        var filtered = new byte[height * (rowBytes + 1)];
        for (int y = 0; y < height; y++)
        {
            filtered[y * (rowBytes + 1)] = filter;
            for (int x = 0; x < rowBytes; x++)
            {
                int left = x < back ? 0 : pixels[y * rowBytes + x - back];
                int up = y == 0 ? 0 : pixels[(y - 1) * rowBytes + x];
                int upLeft = x < back || y == 0 ? 0 : pixels[(y - 1) * rowBytes + x - back];
                int prediction = filter switch
                {
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => 0,
                };
                filtered[y * (rowBytes + 1) + 1 + x] =
                    (byte)(pixels[y * rowBytes + x] - prediction);
            }
        }

        using var rows = new MemoryStream();
        using (var zlib = new ZLibStream(rows, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(filtered);
        }

        WriteChunk(file, "IDAT", rows.ToArray());
        WriteChunk(file, "IEND", []);
        return file.ToArray();
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        int estimate = left + up - upLeft;
        int toLeft = Math.Abs(estimate - left);
        int toUp = Math.Abs(estimate - up);
        int toUpLeft = Math.Abs(estimate - upLeft);
        return toLeft <= toUp && toLeft <= toUpLeft ? left : toUp <= toUpLeft ? up : upLeft;
    }

    private static void WriteChunk(Stream file, string type, byte[] data)
    {
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        var number = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        file.Write(number);
        file.Write(typeBytes);
        file.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc([.. typeBytes, .. data]));
        file.Write(number);
    }

    // Bit-by-bit CRC-32 (slow but obviously right; test images are small enough).
    private static uint Crc(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFF;
    }
}
