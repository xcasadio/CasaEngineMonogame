using CasaEngine.Framework.Rendering.Shaders;

namespace CasaEngine.Framework.Rendering.Environment;

public static class EnvironmentShaderBinder
{
    public static void Bind(ShaderWrapper shader, in ResolvedEnvironmentSettings environment, RenderStats? stats = null)
    {
        ArgumentNullException.ThrowIfNull(shader);

        shader.SetParameter(ShaderParameterNames.EnvironmentAmbientColor, environment.EffectiveAmbientColor);
        shader.SetParameter(ShaderParameterNames.EnvironmentSpecularIntensity, environment.SpecularIntensity);
        shader.SetParameter(ShaderParameterNames.HasEnvironmentCubeTexture, environment.SpecularEnvironmentCubemap is not null ? 1.0f : 0.0f);
        shader.SetParameter(ShaderParameterNames.HasLocalReflectionProbeTexture, environment.PrimaryReflectionProbeCubemap is not null ? 1.0f : 0.0f);
        shader.SetParameter(ShaderParameterNames.HasSecondaryLocalReflectionProbeTexture, environment.SecondaryReflectionProbeCubemap is not null ? 1.0f : 0.0f);
        shader.SetParameter(ShaderParameterNames.LocalReflectionProbeWeight, environment.PrimaryReflectionProbeWeight);
        shader.SetParameter(ShaderParameterNames.SecondaryLocalReflectionProbeWeight, environment.SecondaryReflectionProbeWeight);
        shader.SetParameter(ShaderParameterNames.LocalReflectionProbeInfluence, environment.LocalReflectionProbeInfluence);
        shader.SetTextureCubeParameter(ShaderParameterNames.EnvironmentCubeTexture, environment.SpecularEnvironmentCubemap, stats);
        shader.SetTextureCubeParameter(ShaderParameterNames.LocalReflectionProbeCubeTexture, environment.PrimaryReflectionProbeCubemap, stats);
        shader.SetTextureCubeParameter(ShaderParameterNames.SecondaryLocalReflectionProbeCubeTexture, environment.SecondaryReflectionProbeCubemap, stats);
    }
}