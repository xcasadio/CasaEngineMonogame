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
            // The cached value depends neither on the position of the camera nor on the probes of the world, but the probe blend
            // does: it is recomputed on every call, without allocation, whenever probes are involved. A world that never had a
            // probe keeps the cached value as it is.
            return view.World.ReflectionProbes.Count == 0 && !HasReflectionProbeData(in cachedEnvironment)
                ? cachedEnvironment
                : WithReflectionProbes(view, in cachedEnvironment);
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

        // Colour origin of the environment cubes (ADR-0105, open point O2): only a cube built from a .hdr panorama holds linear
        // light; procedural sky, physical atmosphere (authored Color values), DDS or authored cubes are display-encoded.
        bool panoramaIsLinear = panoramaCubemap is not null && PanoramaEnvironmentGenerator.IsLinearPanorama(view, panoramaAssetId);
        bool backgroundCubemapIsLinear = panoramaIsLinear && ReferenceEquals(backgroundCubemap, panoramaCubemap);
        bool specularEnvironmentCubemapIsLinear = panoramaIsLinear && ReferenceEquals(specularEnvironmentCubemap, panoramaCubemap);

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
            AmbientSource = source.AmbientSource,
            ColorPipelineMode = source.ColorPipeline.Mode,
            Tonemapper = source.ColorPipeline.Tonemapper,
            Exposure = source.ColorPipeline.Exposure,
            BloomEnabled = source.ColorPipeline.BloomEnabled,
            BloomThreshold = source.ColorPipeline.BloomThreshold,
            BloomIntensity = source.ColorPipeline.BloomIntensity,
            BloomRadius = source.ColorPipeline.BloomRadius,
            UsesLegacyClearColor = usesLegacyClearColor,
            UsesLegacyLighting = usesLegacyLighting,
        };

        view.EnvironmentCache.Store(view, in resolvedEnvironment);
        source.MarkClean();
        return resolvedEnvironment;
    }

    private static bool HasReflectionProbeData(in ResolvedEnvironmentSettings environment)
    {
        return environment.PrimaryReflectionProbeCubemap is not null || environment.SecondaryReflectionProbeCubemap is not null;
    }

    /// <summary>The resolved value with the reflection probe blend of the view recomputed (see <see cref="Resolve"/>).</summary>
    private static ResolvedEnvironmentSettings WithReflectionProbes(RenderView view, in ResolvedEnvironmentSettings environment)
    {
        ResolvedReflectionProbeBlend blend = ReflectionProbeResolver.Resolve(view);
        return environment with
        {
            PrimaryReflectionProbeId = blend.PrimaryProbeId,
            SecondaryReflectionProbeId = blend.SecondaryProbeId,
            PrimaryReflectionProbeCubemap = blend.PrimaryCubemap,
            SecondaryReflectionProbeCubemap = blend.SecondaryCubemap,
            PrimaryReflectionProbeWeight = blend.PrimaryWeight,
            SecondaryReflectionProbeWeight = blend.SecondaryWeight,
            LocalReflectionProbeInfluence = blend.Influence,
        };
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