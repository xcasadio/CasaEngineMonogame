using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using XnaTextureCube = Microsoft.Xna.Framework.Graphics.TextureCube;

namespace CasaEngine.Framework.Assets.Loaders;

public sealed class TextureCubeLoader : IAssetLoader
{
    private static readonly CubeMapFace[] Faces =
    [
        CubeMapFace.PositiveX,
        CubeMapFace.NegativeX,
        CubeMapFace.PositiveY,
        CubeMapFace.NegativeY,
        CubeMapFace.PositiveZ,
        CubeMapFace.NegativeZ,
    ];

    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        => LoadTextureCube(fileName, assetContentManager.GraphicsDevice);

    public bool IsFileSupported(string fileName)
        => IsTextureCubeFile(fileName);

    public static bool IsTextureCubeFile(string fileName)
        => Path.GetExtension(fileName).Equals(".dds", StringComparison.OrdinalIgnoreCase);

    public static XnaTextureCube LoadTextureCube(string fileName, GraphicsDevice graphicsDevice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        using var stream = File.OpenRead(fileName);
        using var reader = new BinaryReader(stream);
        var header = ReadHeader(reader);

        if (!header.IsCubeMap)
        {
            throw new InvalidOperationException($"DDS file '{fileName}' is not a cubemap.");
        }

        if (header.Width != header.Height)
        {
            throw new NotSupportedException($"DDS cubemap '{fileName}' must be square.");
        }

        if (header.MipMapCount > 1)
        {
            throw new NotSupportedException($"DDS cubemap '{fileName}' uses mipmaps, which are not supported by this loader.");
        }

        var textureCube = new XnaTextureCube(graphicsDevice, header.Width, mipMap: false, header.SurfaceFormat);
        int faceSize = header.GetFaceByteSize();

        for (int faceIndex = 0; faceIndex < Faces.Length; faceIndex++)
        {
            byte[] faceData = reader.ReadBytes(faceSize);
            if (faceData.Length != faceSize)
            {
                throw new EndOfStreamException($"DDS cubemap '{fileName}' ended before face {faceIndex} was fully read.");
            }

            if (header.IsCompressed)
            {
                textureCube.SetData(Faces[faceIndex], faceData);
            }
            else
            {
                textureCube.SetData(Faces[faceIndex], ConvertUncompressedFace(faceData, header));
            }
        }

        textureCube.Name = Path.GetFileName(fileName);
        return textureCube;
    }

    private static DdsHeader ReadHeader(BinaryReader reader)
    {
        const uint ddsMagic = 0x20534444;
        if (reader.ReadUInt32() != ddsMagic)
        {
            throw new InvalidDataException("The DDS header magic is missing.");
        }

        uint headerSize = reader.ReadUInt32();
        if (headerSize != 124)
        {
            throw new InvalidDataException($"Unexpected DDS header size '{headerSize}'.");
        }

        _ = reader.ReadUInt32(); // flags
        int height = checked((int)reader.ReadUInt32());
        int width = checked((int)reader.ReadUInt32());
        _ = reader.ReadUInt32(); // pitch or linear size
        _ = reader.ReadUInt32(); // depth
        int mipMapCount = checked((int)reader.ReadUInt32());

        for (int i = 0; i < 11; i++)
        {
            _ = reader.ReadUInt32();
        }

        uint pixelFormatSize = reader.ReadUInt32();
        if (pixelFormatSize != 32)
        {
            throw new InvalidDataException($"Unexpected DDS pixel format size '{pixelFormatSize}'.");
        }

        uint pixelFormatFlags = reader.ReadUInt32();
        string fourCC = new(reader.ReadChars(4));
        uint rgbBitCount = reader.ReadUInt32();
        uint rMask = reader.ReadUInt32();
        uint gMask = reader.ReadUInt32();
        uint bMask = reader.ReadUInt32();
        uint aMask = reader.ReadUInt32();

        _ = reader.ReadUInt32(); // caps
        uint caps2 = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();

        if ((caps2 & 0x0000FE00u) != 0x0000FE00u)
        {
            throw new NotSupportedException("Only DDS cubemaps with all six faces are supported.");
        }

        return DdsHeader.Create(width, height, mipMapCount, pixelFormatFlags, fourCC, rgbBitCount, rMask, gMask, bMask, aMask);
    }

    private static byte[] ConvertUncompressedFace(byte[] sourceData, DdsHeader header)
    {
        int bytesPerPixel = checked((int)(header.RgbBitCount / 8));
        int pixelCount = header.Width * header.Height;
        var rgba = new byte[pixelCount * 4];

        for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
        {
            int sourceOffset = pixelIndex * bytesPerPixel;
            uint packedPixel = 0;
            for (int byteIndex = 0; byteIndex < bytesPerPixel; byteIndex++)
            {
                packedPixel |= (uint)sourceData[sourceOffset + byteIndex] << (8 * byteIndex);
            }

            int targetOffset = pixelIndex * 4;
            rgba[targetOffset] = ExtractChannel(packedPixel, header.RMask);
            rgba[targetOffset + 1] = ExtractChannel(packedPixel, header.GMask);
            rgba[targetOffset + 2] = ExtractChannel(packedPixel, header.BMask);
            rgba[targetOffset + 3] = header.AMask == 0 ? (byte)255 : ExtractChannel(packedPixel, header.AMask);
        }

        return rgba;
    }

    private static byte ExtractChannel(uint packedPixel, uint mask)
    {
        if (mask == 0)
        {
            return 0;
        }

        int shift = BitOperations.TrailingZeroCount(mask);
        uint value = (packedPixel & mask) >> shift;
        uint maxValue = mask >> shift;
        return maxValue == 0
            ? (byte)0
            : (byte)((value * 255u + (maxValue / 2u)) / maxValue);
    }

    private readonly record struct DdsHeader(
        int Width,
        int Height,
        int MipMapCount,
        bool IsCubeMap,
        bool IsCompressed,
        SurfaceFormat SurfaceFormat,
        uint RgbBitCount,
        uint RMask,
        uint GMask,
        uint BMask,
        uint AMask)
    {
        public static DdsHeader Create(
            int width,
            int height,
            int mipMapCount,
            uint pixelFormatFlags,
            string fourCC,
            uint rgbBitCount,
            uint rMask,
            uint gMask,
            uint bMask,
            uint aMask)
        {
            const uint ddsFourCc = 0x00000004u;
            const uint ddsRgb = 0x00000040u;
            const uint ddsRgba = 0x00000041u;

            if ((pixelFormatFlags & ddsFourCc) != 0)
            {
                return fourCC.TrimEnd('\0', ' ') switch
                {
                    "DXT1" => new DdsHeader(width, height, mipMapCount, true, true, SurfaceFormat.Dxt1, rgbBitCount, rMask, gMask, bMask, aMask),
                    "DXT3" => new DdsHeader(width, height, mipMapCount, true, true, SurfaceFormat.Dxt3, rgbBitCount, rMask, gMask, bMask, aMask),
                    "DXT5" => new DdsHeader(width, height, mipMapCount, true, true, SurfaceFormat.Dxt5, rgbBitCount, rMask, gMask, bMask, aMask),
                    _ => throw new NotSupportedException($"DDS cubemap format '{fourCC}' is not supported."),
                };
            }

            if ((pixelFormatFlags & ddsRgba) == ddsRgba && rgbBitCount == 32)
            {
                return new DdsHeader(width, height, mipMapCount, true, false, SurfaceFormat.Color, rgbBitCount, rMask, gMask, bMask, aMask);
            }

            if ((pixelFormatFlags & ddsRgb) != 0 && rgbBitCount == 24)
            {
                return new DdsHeader(width, height, mipMapCount, true, false, SurfaceFormat.Color, rgbBitCount, rMask, gMask, bMask, aMask);
            }

            throw new NotSupportedException($"DDS cubemap pixel format flags '0x{pixelFormatFlags:X}' with {rgbBitCount} bits are not supported.");
        }

        public int GetFaceByteSize()
        {
            if (IsCompressed)
            {
                int blockSize = SurfaceFormat == SurfaceFormat.Dxt1 ? 8 : 16;
                int blockWidth = Math.Max(1, (Width + 3) / 4);
                int blockHeight = Math.Max(1, (Height + 3) / 4);
                return blockWidth * blockHeight * blockSize;
            }

            return checked(Width * Height * (int)(RgbBitCount / 8));
        }
    }
}