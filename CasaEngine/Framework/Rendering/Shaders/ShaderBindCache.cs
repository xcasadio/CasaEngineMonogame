using CasaEngine.Framework.Rendering.Environment;

namespace CasaEngine.Framework.Rendering.Shaders;

/// <summary>
/// Tracks the last shader that had its per-frame globals (view, projection, eye position, lights)
/// pushed onto the GPU. When the same shader is used for consecutive draw calls those globals
/// are not re-uploaded, reducing CPU–GPU bandwidth.
/// Reset at the start of each frame via <see cref="ResetFrame"/>.
/// </summary>
public sealed class ShaderBindCache
{
    private ShaderWrapper? _lastShader;
    private readonly ForwardLightBinder _forwardLightBinder = new();

    /// <summary>
    /// Sets the per-frame global parameters on <paramref name="shader"/> if it differs
    /// from the last bound shader.
    /// Returns <c>true</c> when the shader changed and globals were re-uploaded.
    /// </summary>
    public bool BindGlobals(ShaderWrapper shader, in RenderContext context)
    {
        if (ReferenceEquals(shader, _lastShader))
        {
            return false;
        }

        _lastShader = shader;

        // Per-frame global parameters
        shader.SetParameter(ShaderParameterNames.EyePosition, context.Frame.CameraPosition);

        _forwardLightBinder.Bind(shader, context.Lighting, context.Stats);
        EnvironmentShaderBinder.Bind(shader, in context.Environment, context.Stats);

        if (context.Stats is not null)
        {
            context.Stats.EffectBinds++;
        }

        return true;
    }

    /// <summary>Forces the next call to <see cref="BindGlobals"/> to re-upload, even for the same shader.</summary>
    public void ResetFrame() => _lastShader = null;
}
