using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StbImageSharp;
using XnaTextureCube = Microsoft.Xna.Framework.Graphics.TextureCube;

namespace CasaEngine.Framework.Rendering.Environment;

internal static class PanoramaEnvironmentGenerator
{
    public const int DefaultCubemapSize = 256;
    private const int MinCubemapSize = 16;
    private const int MaxCubemapSize = 1024;

    private static readonly CubeMapFace[] Faces =
    [
        CubeMapFace.PositiveX,
        CubeMapFace.NegativeX,
        CubeMapFace.PositiveY,
        CubeMapFace.NegativeY,
        CubeMapFace.PositiveZ,
        CubeMapFace.NegativeZ,
    ];

    public static XnaTextureCube? GetOrCreateCubemap(RenderView view, Guid panoramaAssetId, int requestedCubemapSize)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (panoramaAssetId == Guid.Empty)
        {
            return null;
        }

        int cubemapSize = NormalizeCubemapSize(requestedCubemapSize);
        var assetContentManager = view.World.Game.AssetContentManager;
        Guid generatedAssetId = CreateGeneratedCubemapAssetId(panoramaAssetId, cubemapSize);
        var cachedCubemap = assetContentManager.GetAsset<XnaTextureCube>(generatedAssetId);
        if (cachedCubemap is { IsDisposed: false })
        {
            return cachedCubemap;
        }

        string? panoramaPath = ResolvePanoramaPath(assetContentManager, panoramaAssetId);
        if (string.IsNullOrWhiteSpace(panoramaPath) || !File.Exists(panoramaPath))
        {
            return null;
        }

        try
        {
            var panorama = PanoramaImageData.Load(panoramaPath);
            var cubemap = CreateCubemap(assetContentManager.GraphicsDevice, panorama, cubemapSize);
            string generatedAssetName = BuildGeneratedAssetName(panoramaPath, cubemapSize);
            cubemap.Name = generatedAssetName;
            assetContentManager.AddAsset(generatedAssetId, generatedAssetName, cubemap);
            return cubemap;
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"[PanoramaEnvironmentGenerator] Cannot create cubemap from panorama '{panoramaPath}'", exception));
            return null;
        }
    }

    internal static int NormalizeCubemapSize(int requestedCubemapSize)
    {
        if (requestedCubemapSize <= 0)
        {
            return DefaultCubemapSize;
        }

        return Math.Clamp(requestedCubemapSize, MinCubemapSize, MaxCubemapSize);
    }

    internal static Guid CreateGeneratedCubemapAssetId(Guid panoramaAssetId, int cubemapSize)
    {
        byte[] bytes = panoramaAssetId.ToByteArray();
        int prefix = BitConverter.ToInt32(bytes, 0) ^ cubemapSize ^ unchecked((int)0x5A17C0DE);
        byte[] prefixBytes = BitConverter.GetBytes(prefix);
        Array.Copy(prefixBytes, bytes, prefixBytes.Length);
        bytes[15] ^= 0x22;
        return new Guid(bytes);
    }

    internal static Vector3 GetDirectionForFace(CubeMapFace face, float u, float v)
    {
        Vector3 direction = face switch
        {
            CubeMapFace.PositiveX => new Vector3(1.0f, v, -u),
            CubeMapFace.NegativeX => new Vector3(-1.0f, v, u),
            CubeMapFace.PositiveY => new Vector3(u, 1.0f, -v),
            CubeMapFace.NegativeY => new Vector3(u, -1.0f, v),
            CubeMapFace.PositiveZ => new Vector3(u, v, 1.0f),
            CubeMapFace.NegativeZ => new Vector3(-u, v, -1.0f),
            _ => Vector3.Forward,
        };

        direction.Normalize();
        return direction;
    }

    internal static Vector2 GetPanoramaUv(Vector3 direction)
    {
        if (direction == Vector3.Zero)
        {
            return new Vector2(0.5f, 0.5f);
        }

        direction.Normalize();
        float longitude = MathF.Atan2(direction.X, -direction.Z);
        float latitude = MathF.Asin(Math.Clamp(direction.Y, -1.0f, 1.0f));
        float u = 0.5f + (longitude / (2.0f * MathF.PI));
        float v = 0.5f - (latitude / MathF.PI);
        return new Vector2(u, v);
    }

    private static XnaTextureCube CreateCubemap(GraphicsDevice graphicsDevice, PanoramaImageData panorama, int cubemapSize)
    {
        var cubemap = new XnaTextureCube(graphicsDevice, cubemapSize, mipMap: false, SurfaceFormat.Vector4);

        for (int faceIndex = 0; faceIndex < Faces.Length; faceIndex++)
        {
            var facePixels = new Vector4[cubemapSize * cubemapSize];
            int pixelIndex = 0;

            for (int y = 0; y < cubemapSize; y++)
            {
                float faceV = 1.0f - (2.0f * (y + 0.5f) / cubemapSize);
                for (int x = 0; x < cubemapSize; x++)
                {
                    float faceU = (2.0f * (x + 0.5f) / cubemapSize) - 1.0f;
                    Vector3 direction = GetDirectionForFace(Faces[faceIndex], faceU, faceV);
                    Vector2 uv = GetPanoramaUv(direction);
                    facePixels[pixelIndex++] = SampleBilinear(panorama, uv);
                }
            }

            cubemap.SetData(Faces[faceIndex], facePixels);
        }

        return cubemap;
    }

    private static Vector4 SampleBilinear(PanoramaImageData panorama, Vector2 uv)
    {
        float wrappedU = uv.X - MathF.Floor(uv.X);
        float clampedV = Math.Clamp(uv.Y, 0.0f, 1.0f);

        float sampleX = (wrappedU * panorama.Width) - 0.5f;
        float sampleY = (clampedV * panorama.Height) - 0.5f;
        int x0 = (int)MathF.Floor(sampleX);
        int y0 = (int)MathF.Floor(sampleY);
        int x1 = x0 + 1;
        int y1 = y0 + 1;

        float tx = sampleX - MathF.Floor(sampleX);
        float ty = sampleY - MathF.Floor(sampleY);

        Vector4 topLeft = panorama.GetPixel(WrapX(x0, panorama.Width), ClampY(y0, panorama.Height));
        Vector4 topRight = panorama.GetPixel(WrapX(x1, panorama.Width), ClampY(y0, panorama.Height));
        Vector4 bottomLeft = panorama.GetPixel(WrapX(x0, panorama.Width), ClampY(y1, panorama.Height));
        Vector4 bottomRight = panorama.GetPixel(WrapX(x1, panorama.Width), ClampY(y1, panorama.Height));

        Vector4 top = Vector4.Lerp(topLeft, topRight, tx);
        Vector4 bottom = Vector4.Lerp(bottomLeft, bottomRight, tx);
        return Vector4.Lerp(top, bottom, ty);
    }

    private static int WrapX(int x, int width)
    {
        int wrapped = x % width;
        return wrapped < 0 ? wrapped + width : wrapped;
    }

    private static int ClampY(int y, int height)
        => Math.Clamp(y, 0, height - 1);

    private static string? ResolvePanoramaPath(AssetContentManager assetContentManager, Guid panoramaAssetId)
    {
        AssetInfo? assetInfo = assetContentManager.RuntimeContext?.ResolveAssetInfo(panoramaAssetId) ?? AssetCatalog.Get(panoramaAssetId);
        if (assetInfo is null)
        {
            return null;
        }

        return ResolveAssetPath(assetContentManager.RuntimeContext, assetInfo.FileName);
    }

    private static string ResolveAssetPath(EngineRuntimeContext? runtimeContext, string relativeFileName)
    {
        if (runtimeContext != null)
        {
            return runtimeContext.GetAssetPath(relativeFileName);
        }

        return Path.Combine(EngineEnvironment.ResolveProjectPath(EngineEnvironment.ProjectPath), relativeFileName);
    }

    private static string BuildGeneratedAssetName(string panoramaPath, int cubemapSize)
        => $"{Path.GetFileNameWithoutExtension(panoramaPath)}_panorama_{cubemapSize}";

    private readonly struct PanoramaImageData
    {
        private readonly Vector4[] _pixels;

        public int Width { get; }

        public int Height { get; }

        public PanoramaImageData(int width, int height, Vector4[] pixels)
        {
            Width = width;
            Height = height;
            _pixels = pixels;
        }

        public Vector4 GetPixel(int x, int y)
            => _pixels[(y * Width) + x];

        public static PanoramaImageData Load(string fileName)
        {
            using var stream = File.OpenRead(fileName);
            string extension = Path.GetExtension(fileName);

            if (string.Equals(extension, ".hdr", StringComparison.OrdinalIgnoreCase))
            {
                var image = ImageResultFloat.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
                return FromFloatData(image.Width, image.Height, image.Data);
            }

            var ldrImage = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            return FromByteData(ldrImage.Width, ldrImage.Height, ldrImage.Data);
        }

        private static PanoramaImageData FromFloatData(int width, int height, float[] data)
        {
            var pixels = new Vector4[width * height];
            for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
            {
                int dataIndex = pixelIndex * 4;
                pixels[pixelIndex] = new Vector4(
                    data[dataIndex],
                    data[dataIndex + 1],
                    data[dataIndex + 2],
                    data[dataIndex + 3]);
            }

            return new PanoramaImageData(width, height, pixels);
        }

        private static PanoramaImageData FromByteData(int width, int height, byte[] data)
        {
            var pixels = new Vector4[width * height];
            const float ByteToFloat = 1.0f / 255.0f;

            for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
            {
                int dataIndex = pixelIndex * 4;
                pixels[pixelIndex] = new Vector4(
                    data[dataIndex] * ByteToFloat,
                    data[dataIndex + 1] * ByteToFloat,
                    data[dataIndex + 2] * ByteToFloat,
                    data[dataIndex + 3] * ByteToFloat);
            }

            return new PanoramaImageData(width, height, pixels);
        }
    }
}