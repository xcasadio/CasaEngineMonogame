using Microsoft.Xna.Framework;
using XnaTextureCube = Microsoft.Xna.Framework.Graphics.TextureCube;

namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Resolves the effective environment data for a view by combining view overrides,
/// world defaults, and legacy fallbacks.
/// </summary>
public static class EnvironmentResolver
{
    public static readonly Vector3 LegacyAmbientColor = new(0.05f, 0.05f, 0.05f);

    public static ResolvedEnvironmentSettings Resolve(RenderView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (view.EnvironmentCache.TryGet(view, out var cachedEnvironment))
        {
            return cachedEnvironment;
        }

        var source = view.EnvironmentOverride ?? view.World.EnvironmentSettings;
        var environmentAsset = EnvironmentAssetLookup.TryLoadEnvironmentAsset(view, source.EnvironmentAssetId);
        EnvironmentType requestedEnvironmentType = ResolveRequestedEnvironmentType(source, environmentAsset);
        ProceduralSkySettings proceduralSky = ResolveProceduralSkySettings(source, environmentAsset);
        PhysicalAtmosphereSettings physicalAtmosphere = ResolvePhysicalAtmosphereSettings(source, environmentAsset);
        Guid panoramaAssetId = source.PanoramaAssetId != Guid.Empty
            ? source.PanoramaAssetId
            : environmentAsset?.PanoramaAssetId ?? Guid.Empty;
        int panoramaCubemapSize = source.PanoramaAssetId != Guid.Empty
            ? PanoramaEnvironmentGenerator.NormalizeCubemapSize(source.PanoramaCubemapSize)
            : PanoramaEnvironmentGenerator.NormalizeCubemapSize(environmentAsset?.PanoramaCubemapSize ?? source.PanoramaCubemapSize);
        XnaTextureCube panoramaCubemap = panoramaAssetId != Guid.Empty
            ? PanoramaEnvironmentGenerator.GetOrCreateCubemap(view, panoramaAssetId, panoramaCubemapSize)
            : null;
        XnaTextureCube proceduralCubemap = requestedEnvironmentType == EnvironmentType.Procedural
            ? ProceduralSkyEnvironmentGenerator.GetOrCreateCubemap(view, proceduralSky)
            : null;
        XnaTextureCube physicalAtmosphereCubemap = requestedEnvironmentType == EnvironmentType.PhysicalAtmosphere
            ? PhysicalAtmosphereEnvironmentGenerator.GetOrCreateCubemap(view, physicalAtmosphere)
            : null;
        XnaTextureCube generatedEnvironmentCubemap = requestedEnvironmentType switch
        {
            EnvironmentType.PhysicalAtmosphere => physicalAtmosphereCubemap,
            EnvironmentType.Procedural => proceduralCubemap,
            EnvironmentType.PanoramaHdr => panoramaCubemap,
            _ when panoramaCubemap is not null => panoramaCubemap,
            _ => null,
        };
        Guid backgroundCubemapAssetId = source.BackgroundCubemapAssetId != Guid.Empty
            ? source.BackgroundCubemapAssetId
            : environmentAsset?.BackgroundCubemapAssetId ?? Guid.Empty;
        Guid specularCubemapAssetId = source.SpecularEnvironmentCubemapAssetId != Guid.Empty
            ? source.SpecularEnvironmentCubemapAssetId
            : environmentAsset?.SpecularCubemapAssetId ?? Guid.Empty;
        XnaTextureCube backgroundCubemap = source.BackgroundCubemap ?? generatedEnvironmentCubemap ?? EnvironmentAssetLookup.TryLoadTextureCube(view, backgroundCubemapAssetId);
        XnaTextureCube specularEnvironmentCubemap = source.SpecularEnvironmentCubemap ?? generatedEnvironmentCubemap ?? EnvironmentAssetLookup.TryLoadTextureCube(view, specularCubemapAssetId);
        if (specularEnvironmentCubemap is null)
        {
            specularEnvironmentCubemap = backgroundCubemap;
            specularCubemapAssetId = specularCubemapAssetId != Guid.Empty ? specularCubemapAssetId : backgroundCubemapAssetId;
        }

        ResolvedReflectionProbeBlend reflectionProbeBlend = ReflectionProbeResolver.Resolve(view);

        Vector3 ambientColor = ResolveAmbientColor(source, environmentAsset);
        float ambientIntensity = (environmentAsset?.AmbientIntensity ?? 1.0f) * source.AmbientIntensity;
        float specularIntensity = (environmentAsset?.SpecularIntensity ?? 1.0f) * source.SpecularIntensity;
        bool hasEnvironmentCubemap = backgroundCubemap is not null;
        bool hasExplicitLighting = source.EnvironmentAssetId != Guid.Empty
            || panoramaAssetId != Guid.Empty
            || source.SpecularEnvironmentCubemapAssetId != Guid.Empty
            || source.SpecularEnvironmentCubemap is not null
            || hasEnvironmentCubemap
            || ambientColor != LegacyAmbientColor
            || ambientIntensity != 1.0f
            || specularIntensity != 1.0f;
        bool usesLegacyClearColor = source.BackgroundMode == EnvironmentBackgroundMode.LegacyClearColor;
        bool usesLegacyLighting = !hasExplicitLighting && source.Type == EnvironmentType.None;

        if (!usesLegacyClearColor
            && source.BackgroundMode == EnvironmentBackgroundMode.Environment
            && !hasEnvironmentCubemap)
        {
            usesLegacyClearColor = true;
        }

        var backgroundColor = usesLegacyClearColor
            ? view.ClearColor
            : source.BackgroundColor;

        var resolvedEnvironment = new ResolvedEnvironmentSettings
        {
            Type = ResolveEnvironmentType(source, environmentAsset, panoramaAssetId != Guid.Empty, hasEnvironmentCubemap),
            BackgroundMode = usesLegacyClearColor ? EnvironmentBackgroundMode.LegacyClearColor : source.BackgroundMode,
            BackgroundColor = backgroundColor,
            EnvironmentAssetId = source.EnvironmentAssetId,
            PanoramaAssetId = panoramaAssetId,
            PanoramaCubemapSize = panoramaCubemapSize,
            BackgroundCubemapAssetId = backgroundCubemapAssetId,
            SpecularEnvironmentCubemapAssetId = specularCubemapAssetId,
            BackgroundCubemap = backgroundCubemap,
            SpecularEnvironmentCubemap = specularEnvironmentCubemap,
            PrimaryReflectionProbeId = reflectionProbeBlend.PrimaryProbeId,
            SecondaryReflectionProbeId = reflectionProbeBlend.SecondaryProbeId,
            PrimaryReflectionProbeCubemap = reflectionProbeBlend.PrimaryCubemap,
            SecondaryReflectionProbeCubemap = reflectionProbeBlend.SecondaryCubemap,
            PrimaryReflectionProbeWeight = reflectionProbeBlend.PrimaryWeight,
            SecondaryReflectionProbeWeight = reflectionProbeBlend.SecondaryWeight,
            LocalReflectionProbeInfluence = reflectionProbeBlend.Influence,
            AmbientColor = usesLegacyLighting ? LegacyAmbientColor : ambientColor,
            AmbientIntensity = usesLegacyLighting ? 1.0f : ambientIntensity,
            SpecularIntensity = usesLegacyLighting ? 1.0f : specularIntensity,
            UsesLegacyClearColor = usesLegacyClearColor,
            UsesLegacyLighting = usesLegacyLighting,
        };

        view.EnvironmentCache.Store(view, in resolvedEnvironment);
        source.MarkClean();
        return resolvedEnvironment;
    }

    internal static Vector3 ResolveAmbientColor(WorldEnvironmentSettings source, EnvironmentAsset environmentAsset)
    {
        ArgumentNullException.ThrowIfNull(source);

        Vector3 sourceAmbientColor = source.AmbientColor;
        if (environmentAsset is null)
        {
            return sourceAmbientColor;
        }

        if (sourceAmbientColor == LegacyAmbientColor)
        {
            return environmentAsset.AmbientColor;
        }

        return environmentAsset.AmbientColor * CreateWorldAmbientTint(sourceAmbientColor);
    }

    private static EnvironmentType ResolveRequestedEnvironmentType(WorldEnvironmentSettings source, EnvironmentAsset environmentAsset)
    {
        if (source.Type != EnvironmentType.None)
        {
            return source.Type;
        }

        return environmentAsset?.Type ?? EnvironmentType.None;
    }

    private static EnvironmentType ResolveEnvironmentType(WorldEnvironmentSettings source, EnvironmentAsset environmentAsset, bool hasPanoramaSource, bool hasEnvironmentCubemap)
    {
        if (source.Type != EnvironmentType.None)
        {
            return source.Type;
        }

        if (environmentAsset != null)
        {
            return environmentAsset.Type;
        }

        if (hasPanoramaSource)
        {
            return EnvironmentType.PanoramaHdr;
        }

        return hasEnvironmentCubemap ? EnvironmentType.Cubemap : EnvironmentType.None;
    }

    private static Vector3 CreateWorldAmbientTint(Vector3 sourceAmbientColor)
    {
        return new Vector3(
            CreateWorldAmbientTint(sourceAmbientColor.X, LegacyAmbientColor.X),
            CreateWorldAmbientTint(sourceAmbientColor.Y, LegacyAmbientColor.Y),
            CreateWorldAmbientTint(sourceAmbientColor.Z, LegacyAmbientColor.Z));
    }

    private static float CreateWorldAmbientTint(float sourceChannel, float legacyChannel)
    {
        if (legacyChannel <= 0.0f)
        {
            return sourceChannel;
        }

        return sourceChannel / legacyChannel;
    }

    private static ProceduralSkySettings ResolveProceduralSkySettings(WorldEnvironmentSettings source, EnvironmentAsset environmentAsset)
    {
        if (source.Type == EnvironmentType.Procedural)
        {
            return source.ProceduralSky.Clone();
        }

        if (environmentAsset?.Type == EnvironmentType.Procedural)
        {
            return environmentAsset.ProceduralSky.Clone();
        }

        return source.ProceduralSky.Clone();
    }

    private static PhysicalAtmosphereSettings ResolvePhysicalAtmosphereSettings(WorldEnvironmentSettings source, EnvironmentAsset environmentAsset)
    {
        if (source.Type == EnvironmentType.PhysicalAtmosphere)
        {
            return source.PhysicalAtmosphere.Clone();
        }

        if (environmentAsset?.Type == EnvironmentType.PhysicalAtmosphere)
        {
            return environmentAsset.PhysicalAtmosphere.Clone();
        }

        return source.PhysicalAtmosphere.Clone();
    }
}