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
        ProceduralSkySettings proceduralSky = ResolveProceduralSkySettings(source, environmentAsset);
        bool usesProceduralSky = source.Type == EnvironmentType.Procedural
            || (source.Type == EnvironmentType.None && environmentAsset?.Type == EnvironmentType.Procedural);
        XnaTextureCube? proceduralCubemap = usesProceduralSky
            ? ProceduralSkyEnvironmentGenerator.GetOrCreateCubemap(view, proceduralSky)
            : null;
        Guid panoramaAssetId = source.PanoramaAssetId != Guid.Empty
            ? source.PanoramaAssetId
            : environmentAsset?.PanoramaAssetId ?? Guid.Empty;
        int panoramaCubemapSize = source.PanoramaAssetId != Guid.Empty
            ? PanoramaEnvironmentGenerator.NormalizeCubemapSize(source.PanoramaCubemapSize)
            : PanoramaEnvironmentGenerator.NormalizeCubemapSize(environmentAsset?.PanoramaCubemapSize ?? source.PanoramaCubemapSize);
        XnaTextureCube? panoramaCubemap = panoramaAssetId != Guid.Empty
            ? PanoramaEnvironmentGenerator.GetOrCreateCubemap(view, panoramaAssetId, panoramaCubemapSize)
            : null;
        Guid backgroundCubemapAssetId = source.BackgroundCubemapAssetId != Guid.Empty
            ? source.BackgroundCubemapAssetId
            : environmentAsset?.BackgroundCubemapAssetId ?? Guid.Empty;
        Guid specularCubemapAssetId = source.SpecularEnvironmentCubemapAssetId != Guid.Empty
            ? source.SpecularEnvironmentCubemapAssetId
            : environmentAsset?.SpecularCubemapAssetId ?? Guid.Empty;
        XnaTextureCube? backgroundCubemap = source.BackgroundCubemap ?? panoramaCubemap ?? proceduralCubemap ?? EnvironmentAssetLookup.TryLoadTextureCube(view, backgroundCubemapAssetId);
        XnaTextureCube? specularEnvironmentCubemap = source.SpecularEnvironmentCubemap ?? panoramaCubemap ?? proceduralCubemap ?? EnvironmentAssetLookup.TryLoadTextureCube(view, specularCubemapAssetId);
        if (specularEnvironmentCubemap is null)
        {
            specularEnvironmentCubemap = backgroundCubemap;
            specularCubemapAssetId = specularCubemapAssetId != Guid.Empty ? specularCubemapAssetId : backgroundCubemapAssetId;
        }

        ResolvedReflectionProbeBlend reflectionProbeBlend = ReflectionProbeResolver.Resolve(view);

        Vector3 ambientColor = environmentAsset?.AmbientColor ?? source.AmbientColor;
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

    private static EnvironmentType ResolveEnvironmentType(WorldEnvironmentSettings source, EnvironmentAsset? environmentAsset, bool hasPanoramaSource, bool hasEnvironmentCubemap)
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

    private static ProceduralSkySettings ResolveProceduralSkySettings(WorldEnvironmentSettings source, EnvironmentAsset? environmentAsset)
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
}