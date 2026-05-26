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

    public bool UsesLegacyClearColor { get; init; }

    public bool UsesLegacyLighting { get; init; }

    public bool HasPanoramaSource => PanoramaAssetId != Guid.Empty;

    public bool HasEnvironmentCubemap => BackgroundCubemap is not null;

    public bool HasLocalReflectionProbe => PrimaryReflectionProbeCubemap is not null && LocalReflectionProbeInfluence > 0.0f;

    public Vector3 EffectiveAmbientColor => AmbientColor * AmbientIntensity;

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
            UsesLegacyClearColor = true,
            UsesLegacyLighting = true,
        };
    }
}