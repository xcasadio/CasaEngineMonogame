using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XnaTextureCube = Microsoft.Xna.Framework.Graphics.TextureCube;

namespace CasaEngine.Framework.Rendering.Environment;

internal static class ProceduralSkyEnvironmentGenerator
{
    public const int DefaultCubemapSize = 128;
    private const int MinCubemapSize = 16;
    private const int MaxCubemapSize = 512;

    private static readonly CubeMapFace[] Faces =
    [
        CubeMapFace.PositiveX,
        CubeMapFace.NegativeX,
        CubeMapFace.PositiveY,
        CubeMapFace.NegativeY,
        CubeMapFace.PositiveZ,
        CubeMapFace.NegativeZ,
    ];

    public static XnaTextureCube GetOrCreateCubemap(RenderView view, ProceduralSkySettings settings)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(settings);

        settings.CubemapSize = NormalizeCubemapSize(settings.CubemapSize);
        Guid generatedAssetId = CreateGeneratedCubemapAssetId(settings);
        var assetContentManager = view.World.Game.AssetContentManager;
        var cachedCubemap = assetContentManager.GetAsset<XnaTextureCube>(generatedAssetId);
        if (cachedCubemap is { IsDisposed: false })
        {
            return cachedCubemap;
        }

        var cubemap = new XnaTextureCube(assetContentManager.GraphicsDevice, settings.CubemapSize, mipMap: false, SurfaceFormat.Vector4);
        for (int faceIndex = 0; faceIndex < Faces.Length; faceIndex++)
        {
            var facePixels = new Vector4[settings.CubemapSize * settings.CubemapSize];
            int pixelIndex = 0;

            for (int y = 0; y < settings.CubemapSize; y++)
            {
                float faceV = 1.0f - (2.0f * (y + 0.5f) / settings.CubemapSize);
                for (int x = 0; x < settings.CubemapSize; x++)
                {
                    float faceU = (2.0f * (x + 0.5f) / settings.CubemapSize) - 1.0f;
                    Vector3 direction = PanoramaEnvironmentGenerator.GetDirectionForFace(Faces[faceIndex], faceU, faceV);
                    facePixels[pixelIndex++] = EvaluateColor(direction, settings);
                }
            }

            cubemap.SetData(Faces[faceIndex], facePixels);
        }

        string assetName = BuildGeneratedAssetName(settings);
        cubemap.Name = assetName;
        assetContentManager.AddAsset(generatedAssetId, assetName, cubemap);
        return cubemap;
    }

    internal static int NormalizeCubemapSize(int requestedCubemapSize)
    {
        if (requestedCubemapSize <= 0)
        {
            return DefaultCubemapSize;
        }

        return Math.Clamp(requestedCubemapSize, MinCubemapSize, MaxCubemapSize);
    }

    internal static Vector4 EvaluateColor(Vector3 direction, ProceduralSkySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (direction == Vector3.Zero)
        {
            return new Vector4(settings.HorizonColor.ToVector3(), 1.0f);
        }

        direction.Normalize();

        if (direction.Y >= 0.0f)
        {
            float skyFactor = MathF.Pow(Math.Clamp(direction.Y, 0.0f, 1.0f), MathF.Max(settings.SkyExponent, 0.001f));
            Vector3 skyColor = Vector3.Lerp(settings.HorizonColor.ToVector3(), settings.ZenithColor.ToVector3(), skyFactor);
            return new Vector4(skyColor, 1.0f);
        }

        float groundFactor = MathF.Pow(Math.Clamp(-direction.Y, 0.0f, 1.0f), MathF.Max(settings.GroundExponent, 0.001f));
        Vector3 groundColor = Vector3.Lerp(settings.HorizonColor.ToVector3(), settings.GroundColor.ToVector3(), groundFactor);
        return new Vector4(groundColor, 1.0f);
    }

    private static Guid CreateGeneratedCubemapAssetId(ProceduralSkySettings settings)
    {
        int hashA = HashCode.Combine(
            (int)settings.ZenithColor.PackedValue,
            (int)settings.HorizonColor.PackedValue,
            (int)settings.GroundColor.PackedValue,
            settings.CubemapSize);
        int hashB = HashCode.Combine(settings.SkyExponent, settings.GroundExponent, unchecked((int)0x50726F63));
        byte[] bytes = new byte[16];
        Array.Copy(BitConverter.GetBytes(hashA), 0, bytes, 0, 4);
        Array.Copy(BitConverter.GetBytes(hashB), 0, bytes, 4, 4);
        Array.Copy(BitConverter.GetBytes(settings.CubemapSize), 0, bytes, 8, 4);
        bytes[12] = 0x50;
        bytes[13] = 0x53;
        bytes[14] = 0x4B;
        bytes[15] = 0x59;
        return new Guid(bytes);
    }

    private static string BuildGeneratedAssetName(ProceduralSkySettings settings)
        => $"procedural_sky_{settings.CubemapSize}_{(uint)settings.ZenithColor.PackedValue:x8}_{(uint)settings.HorizonColor.PackedValue:x8}_{(uint)settings.GroundColor.PackedValue:x8}";
}