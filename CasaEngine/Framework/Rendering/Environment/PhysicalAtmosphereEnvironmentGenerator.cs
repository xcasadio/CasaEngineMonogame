using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XnaTextureCube = Microsoft.Xna.Framework.Graphics.TextureCube;

namespace CasaEngine.Framework.Rendering.Environment;

internal static class PhysicalAtmosphereEnvironmentGenerator
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

    public static XnaTextureCube GetOrCreateCubemap(RenderView view, PhysicalAtmosphereSettings settings)
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

    internal static Vector3 NormalizeSunDirection(Vector3 sunDirection)
    {
        if (sunDirection == Vector3.Zero)
        {
            return Vector3.Normalize(new Vector3(0.0f, 1.0f, -0.001f));
        }

        sunDirection.Normalize();
        return sunDirection;
    }

    internal static Vector4 EvaluateColor(Vector3 direction, PhysicalAtmosphereSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (direction == Vector3.Zero)
        {
            direction = Vector3.Up;
        }
        else
        {
            direction.Normalize();
        }

        Vector3 sunDirection = NormalizeSunDirection(settings.SunDirection);
        Vector3 spaceColor = settings.SpaceColor.ToVector3();
        Vector3 rayleighColor = settings.RayleighColor.ToVector3();
        Vector3 mieColor = settings.MieColor.ToVector3();
        Vector3 sunsetColor = settings.SunsetColor.ToVector3();
        Vector3 groundColor = settings.GroundColor.ToVector3();
        Vector3 sunColor = Color.White.ToVector3();

        float viewUpAmount = Math.Clamp(direction.Y, 0.0f, 1.0f);
        float viewDownAmount = Math.Clamp(-direction.Y, 0.0f, 1.0f);
        float opticalDepth = MathF.Pow(1.0f - viewUpAmount, MathF.Max(settings.AtmosphereDensity, 0.01f));
        float airScattering = Math.Clamp(settings.RayleighDensity * (0.15f + (0.85f * opticalDepth)), 0.0f, 1.0f);
        float spaceVisibility = Math.Clamp(MathF.Pow(viewUpAmount, MathF.Max(settings.SpaceFalloff, 0.01f)), 0.0f, 1.0f);
        float sunHeight = Math.Clamp((sunDirection.Y * 0.5f) + 0.5f, 0.0f, 1.0f);
        float sunsetAmount = (1.0f - sunHeight) * opticalDepth;
        float mu = Math.Clamp(Vector3.Dot(direction, sunDirection), -1.0f, 1.0f);
        float rayleighPhase = 0.75f * (1.0f + (mu * mu));

        float mieAnisotropy = Math.Clamp(settings.MieAnisotropy, -0.99f, 0.99f);
        float mieDenominator = MathF.Max(1.0f + (mieAnisotropy * mieAnisotropy) - (2.0f * mieAnisotropy * mu), 0.001f);
        float miePhase = (1.0f - (mieAnisotropy * mieAnisotropy)) / MathF.Pow(mieDenominator, 1.5f);

        Vector3 atmosphereBase = Vector3.Lerp(spaceColor, rayleighColor, airScattering);
        atmosphereBase = Vector3.Lerp(atmosphereBase, sunsetColor, sunsetAmount * 0.75f);
        atmosphereBase = Vector3.Lerp(atmosphereBase, spaceColor, spaceVisibility * 0.45f);

        Vector3 skyColor = atmosphereBase * (0.72f + (0.28f * rayleighPhase));
        skyColor += rayleighColor * (settings.RayleighDensity * (0.08f + (0.22f * airScattering)));
        skyColor += mieColor * (settings.MieDensity * opticalDepth * 0.05f * miePhase);

        float sunDiscCos = MathF.Cos(Math.Clamp(settings.SunDiscSize, 0.001f, 0.25f));
        float sunDiscMask = SmoothStep(sunDiscCos, 1.0f, mu);
        float sunGlowMask = MathF.Pow(Math.Clamp(mu, 0.0f, 1.0f), 12.0f);
        skyColor += sunColor * (settings.SunIntensity * sunDiscMask);
        skyColor += mieColor * (settings.SunIntensity * 0.12f * sunGlowMask);

        if (viewDownAmount > 0.0f)
        {
            Vector3 horizonColor = Vector3.Lerp(atmosphereBase, sunsetColor, sunsetAmount * 0.35f);
            float horizonRetain = MathF.Pow(1.0f - viewDownAmount, MathF.Max(settings.GroundFalloff, 0.01f));
            skyColor = Vector3.Lerp(groundColor, horizonColor, horizonRetain);
        }

        return new Vector4(skyColor, 1.0f);
    }

    private static Guid CreateGeneratedCubemapAssetId(PhysicalAtmosphereSettings settings)
    {
        Vector3 sunDirection = NormalizeSunDirection(settings.SunDirection);
        int hashA = HashCode.Combine(
            (int)settings.SpaceColor.PackedValue,
            (int)settings.RayleighColor.PackedValue,
            (int)settings.MieColor.PackedValue,
            (int)settings.SunsetColor.PackedValue);
        int hashB = HashCode.Combine(
            (int)settings.GroundColor.PackedValue,
            settings.AtmosphereDensity,
            settings.RayleighDensity,
            settings.MieDensity);
        int hashC = HashCode.Combine(
            settings.MieAnisotropy,
            settings.SunIntensity,
            settings.SunDiscSize,
            settings.GroundFalloff);
        int hashD = HashCode.Combine(
            settings.SpaceFalloff,
            settings.CubemapSize,
            sunDirection.X,
            HashCode.Combine(sunDirection.Y, sunDirection.Z));

        byte[] bytes = new byte[16];
        Array.Copy(BitConverter.GetBytes(hashA), 0, bytes, 0, 4);
        Array.Copy(BitConverter.GetBytes(hashB), 0, bytes, 4, 4);
        Array.Copy(BitConverter.GetBytes(hashC), 0, bytes, 8, 4);
        Array.Copy(BitConverter.GetBytes(hashD), 0, bytes, 12, 4);
        return new Guid(bytes);
    }

    private static string BuildGeneratedAssetName(PhysicalAtmosphereSettings settings)
        => $"physical_atmosphere_{settings.CubemapSize}_{(uint)settings.SpaceColor.PackedValue:x8}_{(uint)settings.RayleighColor.PackedValue:x8}_{(uint)settings.MieColor.PackedValue:x8}";

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (edge0 == edge1)
        {
            return value < edge0 ? 0.0f : 1.0f;
        }

        float t = Math.Clamp((value - edge0) / (edge1 - edge0), 0.0f, 1.0f);
        return t * t * (3.0f - (2.0f * t));
    }
}