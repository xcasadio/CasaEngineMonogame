using Microsoft.Xna.Framework;
using XnaTextureCube = Microsoft.Xna.Framework.Graphics.TextureCube;

namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Immutable per-view environment data after world/view overrides and fallbacks are resolved.
/// </summary>
public readonly struct ResolvedEnvironmentSettings
{
    public EnvironmentType Type { get; init; }

    public EnvironmentBackgroundMode BackgroundMode { get; init; }

    public Color BackgroundColor { get; init; }

    public Guid EnvironmentAssetId { get; init; }

    public Guid PanoramaAssetId { get; init; }

    public int PanoramaCubemapSize { get; init; }

    public Guid BackgroundCubemapAssetId { get; init; }

    public Guid SpecularEnvironmentCubemapAssetId { get; init; }

    public XnaTextureCube BackgroundCubemap { get; init; }

    public XnaTextureCube SpecularEnvironmentCubemap { get; init; }

    public Guid PrimaryReflectionProbeId { get; init; }

    public Guid SecondaryReflectionProbeId { get; init; }

    public XnaTextureCube PrimaryReflectionProbeCubemap { get; init; }

    public XnaTextureCube SecondaryReflectionProbeCubemap { get; init; }

    public float PrimaryReflectionProbeWeight { get; init; }

    public float SecondaryReflectionProbeWeight { get; init; }

    public float LocalReflectionProbeInfluence { get; init; }

    public Vector3 AmbientColor { get; init; }

    public float AmbientIntensity { get; init; }

    public float SpecularIntensity { get; init; }

    public AmbientSource AmbientSource { get; init; }

    public ColorPipelineMode ColorPipelineMode { get; init; }

    public TonemapOperator Tonemapper { get; init; }

    /// <summary>Anti-aliasing requested for the view (ADR-0112). <see cref="AntiAliasingMode.None"/> in <see cref="CreateLegacy"/>.</summary>
    public AntiAliasingMode AntiAliasing { get; init; }

    /// <summary>Scalar exposure applied before the tonemapping operator. 1 in <see cref="CreateLegacy"/>.</summary>
    public float Exposure { get; init; }

    /// <summary>Bloom is requested for the view (ADR-0107). False in <see cref="CreateLegacy"/>.</summary>
    public bool BloomEnabled { get; init; }

    public float BloomThreshold { get; init; }

    public float BloomIntensity { get; init; }

    public float BloomRadius { get; init; }

    /// <summary>The scene depth pre-pass and the SSAO are requested for the view (ADR-0115). False in <see cref="CreateLegacy"/>.</summary>
    public bool SsaoEnabled { get; init; }

    public float SsaoRadius { get; init; }

    public float SsaoIntensity { get; init; }

    public float SsaoBias { get; init; }

    /// <summary>
    /// True when this view is drawn through the HDR scene target in this frame (ADR-0105), so its display colours must be
    /// decoded to linear light. It is not the resolved mode: a view in <see cref="Environment.ColorPipelineMode.LinearHdr"/>
    /// that the planner leaves on the legacy path (ADR-0104) keeps this false. Set by the render pipeline after resolution
    /// (the resolved value is cached per view), false everywhere else.
    /// </summary>
    public bool LinearColorActive { get; init; }

    /// <summary>The background cube holds linear light (a cube built from a <c>.hdr</c> panorama), so it is not decoded.</summary>
    public bool BackgroundCubemapIsLinear { get; init; }

    /// <summary>The specular and diffuse environment cube holds linear light (a cube built from a <c>.hdr</c> panorama), so it is not decoded.</summary>
    public bool SpecularEnvironmentCubemapIsLinear { get; init; }

    public bool UsesLegacyClearColor { get; init; }

    public bool UsesLegacyLighting { get; init; }

    public bool HasPanoramaSource => PanoramaAssetId != Guid.Empty;

    public bool HasEnvironmentCubemap => BackgroundCubemap is not null;

    public bool HasLocalReflectionProbe => PrimaryReflectionProbeCubemap is not null && LocalReflectionProbeInfluence > 0.0f;

    /// <summary>
    /// The ambient colour times its intensity. When <see cref="LinearColorActive"/> the colour is decoded first:
    /// <c>Decode(AmbientColor) * AmbientIntensity</c> (the intensity is a linear multiplier and is never decoded).
    /// </summary>
    public Vector3 EffectiveAmbientColor => LinearColorActive
        ? ColorSpace.Decode(AmbientColor) * AmbientIntensity
        : AmbientColor * AmbientIntensity;

    /// <summary>True when the ambient term must be the flat ambient colour even if an environment cube is bound.</summary>
    public bool UsesFlatAmbient => AmbientSource == AmbientSource.Flat;

    /// <summary>True when the view is rendered in <see cref="Environment.ColorPipelineMode.LinearHdr"/>.</summary>
    public bool IsLinearHdr => ColorPipelineMode == ColorPipelineMode.LinearHdr;

    public static ResolvedEnvironmentSettings CreateLegacy(Color clearColor, Vector3 ambientColor)
    {
        return new ResolvedEnvironmentSettings
        {
            Type = EnvironmentType.None,
            BackgroundMode = EnvironmentBackgroundMode.LegacyClearColor,
            BackgroundColor = clearColor,
            AmbientColor = ambientColor,
            AmbientIntensity = 1.0f,
            SpecularIntensity = 1.0f,
            Exposure = ColorPipelineSettings.DefaultExposure,
            BloomThreshold = ColorPipelineSettings.DefaultBloomThreshold,
            BloomIntensity = ColorPipelineSettings.DefaultBloomIntensity,
            BloomRadius = ColorPipelineSettings.DefaultBloomRadius,
            UsesLegacyClearColor = true,
            UsesLegacyLighting = true,
        };
    }
}