using System.IO.Compression;
using Microsoft.Xna.Framework;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>
/// The synthetic images of Projects/SpriteCutsceneSample (plan choice C8). Every number is an integer, so the pixels are the
/// same on every machine; a data test decodes the committed PNG files and compares their pixels with this output.
/// </summary>
internal static class SpriteCutsceneSampleImages
{
    /// <summary>Side of the orb and of the ring, in pixels.</summary>
    public const int ShapeSize = 48;

    /// <summary>The shapes sheet: the orb on the left half, the ring on the right half.</summary>
    public const int ShapesWidth = 2 * ShapeSize;

    /// <summary>A white disc that fades to black towards its rim, transparent black outside. Meant to be drawn additively.</summary>
    public static Color OrbPixel(int x, int y)
    {
        var distanceSquared = DoubledDistanceSquared(x, y);
        if (distanceSquared > MaxDistanceSquared)
        {
            return new Color(0, 0, 0, 0);
        }

        var value = 255 - distanceSquared * 255 / MaxDistanceSquared;
        return new Color(value, value, value, 255);
    }

    /// <summary>A grey ring, transparent black elsewhere. Meant to be drawn subtractively, so it has no colour outside the ring.</summary>
    public static Color RingPixel(int x, int y)
    {
        var distanceSquared = DoubledDistanceSquared(x, y);
        return distanceSquared >= InnerDistanceSquared && distanceSquared <= MaxDistanceSquared
            ? new Color(160, 160, 160, 255)
            : new Color(0, 0, 0, 0);
    }

    public static byte[] BuildShapesPng() => WritePng(ShapesWidth, ShapeSize, static (x, y) => x < ShapeSize ? OrbPixel(x, y) : RingPixel(x - ShapeSize, y));

    // Squared distance to the centre of the shape, in half pixels, (2x - 47)^2 + (2y - 47)^2 for a 48 pixel shape. The disc radius is 24 pixels (48 half pixels).
    private const int MaxDistanceSquared = ShapeSize * ShapeSize;
    private const int InnerDistanceSquared = 33 * 33;

    private static int DoubledDistanceSquared(int x, int y)
    {
        var dx = 2 * x - (ShapeSize - 1);
        var dy = 2 * y - (ShapeSize - 1);
        return dx * dx + dy * dy;
    }

    /// <summary>A minimal PNG writer: 8 bit RGBA, no interlacing, filter 0 on every row.</summary>
    private static byte[] WritePng(int width, int height, Func<int, int, Color> pixel)
    {
        var raw = new byte[height * (1 + width * 4)];
        var offset = 0;
        for (var y = 0; y < height; y++)
        {
            raw[offset++] = 0;
            for (var x = 0; x < width; x++)
            {
                var color = pixel(x, y);
                raw[offset++] = color.R;
                raw[offset++] = color.G;
                raw[offset++] = color.B;
                raw[offset++] = color.A;
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        WriteBigEndian(header, 0, width);
        WriteBigEndian(header, 4, height);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        stream.Write(length);

        var typeAndData = new byte[4 + data.Length];
        for (var index = 0; index < 4; index++)
        {
            typeAndData[index] = (byte)type[index];
        }

        data.CopyTo(typeAndData, 4);
        stream.Write(typeAndData);

        var crc = new byte[4];
        WriteBigEndian(crc, 0, unchecked((int)Crc32(typeAndData)));
        stream.Write(crc);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
