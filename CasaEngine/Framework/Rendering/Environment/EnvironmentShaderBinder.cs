using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Environment;

public static class EnvironmentShaderBinder
{
    public static void Bind(ShaderWrapper shader, in ResolvedEnvironmentSettings environment, RenderStats stats = null, IblCache ibl = null)
    {
        ArgumentNullException.ThrowIfNull(shader);

        shader.SetParameter(ShaderParameterNames.ColorPipelineIsLinear, environment.LinearColorActive ? 1.0f : 0.0f);
        shader.SetParameter(ShaderParameterNames.EnvironmentCubeIsLinear, environment.SpecularEnvironmentCubemapIsLinear ? 1.0f : 0.0f);
        shader.SetParameter(ShaderParameterNames.EnvironmentAmbientColor, environment.EffectiveAmbientColor);
        shader.SetParameter(ShaderParameterNames.EnvironmentSpecularIntensity, environment.SpecularIntensity);
        shader.SetParameter(ShaderParameterNames.HasEnvironmentCubeTexture, environment.SpecularEnvironmentCubemap is not null ? 1.0f : 0.0f);
        shader.SetParameter(ShaderParameterNames.UseFlatAmbientSource, environment.UsesFlatAmbient ? 1.0f : 0.0f);
        shader.SetParameter(ShaderParameterNames.HasLocalReflectionProbeTexture, environment.PrimaryReflectionProbeCubemap is not null ? 1.0f : 0.0f);
        shader.SetParameter(ShaderParameterNames.HasSecondaryLocalReflectionProbeTexture, environment.SecondaryReflectionProbeCubemap is not null ? 1.0f : 0.0f);
        shader.SetParameter(ShaderParameterNames.LocalReflectionProbeWeight, environment.PrimaryReflectionProbeWeight);
        shader.SetParameter(ShaderParameterNames.SecondaryLocalReflectionProbeWeight, environment.SecondaryReflectionProbeWeight);
        shader.SetParameter(ShaderParameterNames.LocalReflectionProbeInfluence, environment.LocalReflectionProbeInfluence);
        shader.SetTextureCubeParameter(ShaderParameterNames.EnvironmentCubeTexture, environment.SpecularEnvironmentCubemap, stats);
        shader.SetTextureCubeParameter(ShaderParameterNames.LocalReflectionProbeCubeTexture, environment.PrimaryReflectionProbeCubemap, stats);
        shader.SetTextureCubeParameter(ShaderParameterNames.SecondaryLocalReflectionProbeCubeTexture, environment.SecondaryReflectionProbeCubemap, stats);

        BindImageBasedLighting(shader, in environment, stats, ibl);
    }

    /// <summary>
    /// Binds the IBL textures of the environment (ADR-0107) on a shader that declares them, and only on such a shader: one that
    /// does not (<c>LitForward</c>) neither receives them nor registers a generation request. With the textures present
    /// <c>HasIbl</c> is 1; absent, the request is registered with <paramref name="ibl"/> (executed at the start of the next
    /// <see cref="RenderPipeline.Render"/>), <c>HasIbl</c> is 0 and the slots are cleared, so the shader keeps its flat ambient.
    /// Without a cache (null) nothing is ever requested.
    /// </summary>
    public static void BindImageBasedLighting(ShaderWrapper shader, in ResolvedEnvironmentSettings environment, RenderStats stats, IblCache ibl)
    {
        ArgumentNullException.ThrowIfNull(shader);

        if (!shader.HasParameter(ShaderParameterNames.HasIbl))
        {
            return;
        }

        bool hasIbl = TryResolveImageBasedLighting(in environment, ibl, out TextureCube irradiance, out Texture2D atlas, out Texture2D brdfTable);

        shader.SetParameter(ShaderParameterNames.HasIbl, hasIbl ? 1.0f : 0.0f);
        shader.SetTextureCubeParameter(ShaderParameterNames.IrradianceCubeTexture, irradiance, stats);
        shader.SetTextureParameter(ShaderParameterNames.PrefilteredSpecularAtlasTexture, atlas, stats);
        shader.SetTextureParameter(ShaderParameterNames.BrdfLookupTexture, brdfTable, stats);

        // The local reflection probes (T7.1): the prefiltered atlas of each selected probe cube, requested from the same cache. Until
        // an atlas exists its flag stays 0 and the shader samples the unprefiltered cube of slot t4 / t5. Probe cubes are display
        // colours (Lighting.fxh decodes them), so they are generated as non-linear sources.
        bool hasPrefilteredPrimary = TryResolvePrefilteredProbe(environment.PrimaryReflectionProbeCubemap, ibl, out Texture2D primaryAtlas);
        bool hasPrefilteredSecondary = TryResolvePrefilteredProbe(environment.SecondaryReflectionProbeCubemap, ibl, out Texture2D secondaryAtlas);
        shader.SetParameter(ShaderParameterNames.HasPrefilteredLocalReflectionProbe, hasPrefilteredPrimary ? 1.0f : 0.0f);
        shader.SetParameter(ShaderParameterNames.HasPrefilteredSecondaryLocalReflectionProbe, hasPrefilteredSecondary ? 1.0f : 0.0f);
        shader.SetTextureParameter(ShaderParameterNames.PrefilteredLocalReflectionProbeAtlasTexture, primaryAtlas, stats);
        shader.SetTextureParameter(ShaderParameterNames.PrefilteredSecondaryLocalReflectionProbeAtlasTexture, secondaryAtlas, stats);
    }

    /// <summary>
    /// What a shader that declares the IBL would receive: the generated textures of the environment cube, or false (and a request
    /// registered with <paramref name="ibl"/>) while they do not exist yet. False without a cube or without a cache.
    /// </summary>
    internal static bool TryResolveImageBasedLighting(
        in ResolvedEnvironmentSettings environment, IblCache ibl, out TextureCube irradiance, out Texture2D atlas, out Texture2D brdfTable)
    {
        irradiance = null;
        atlas = null;
        brdfTable = null;
        return ibl is not null
            && environment.SpecularEnvironmentCubemap is not null
            && ibl.TryGet(environment.SpecularEnvironmentCubemap, environment.SpecularEnvironmentCubemapIsLinear, out irradiance, out atlas, out brdfTable);
    }

    /// <summary>
    /// The prefiltered atlas of a probe cube, or false (and a request registered with <paramref name="ibl"/>) while it does not
    /// exist yet. False without a probe cube or without a cache.
    /// </summary>
    internal static bool TryResolvePrefilteredProbe(TextureCube probeCube, IblCache ibl, out Texture2D atlas)
    {
        atlas = null;
        return ibl is not null
            && probeCube is not null
            && ibl.TryGetPrefilteredAtlas(probeCube, false, out atlas);
    }
}