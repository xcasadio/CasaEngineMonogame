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
        shader.SetTextureCubeParameter(ShaderParameterNames.EnvironmentCubeTexture, environment.SpecularEnvironmentCubemap, stats);
    }
}