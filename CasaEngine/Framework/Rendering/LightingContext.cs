using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Holds lighting data for a render pass: directional lights and ambient color.
/// Populated and attached to <see cref="RenderContext"/> before rendering.
/// Fully implemented in Phase 5.
/// </summary>
public class LightingContext
{
    public const int MaxDirectionalLights = 8;

    private static readonly Vector3 ZeroVector3 = Vector3.Zero;

    public DirectionalLight[] DirectionalLights { get; } = new DirectionalLight[MaxDirectionalLights];
    public int ActiveDirectionalLightCount { get; set; }
    public Vector3 AmbientColor { get; set; } = EnvironmentResolver.LegacyAmbientColor;

    internal static int ClampActiveDirectionalLightCount(int activeDirectionalLightCount)
        => Math.Clamp(activeDirectionalLightCount, 0, MaxDirectionalLights);

    public void CopyFrom(LightingContext other)
    {
        ArgumentNullException.ThrowIfNull(other);

        ActiveDirectionalLightCount = ClampActiveDirectionalLightCount(other.ActiveDirectionalLightCount);
        AmbientColor = other.AmbientColor;

        for (int i = 0; i < MaxDirectionalLights; i++)
        {
            DirectionalLights[i] = other.DirectionalLights[i];
        }
    }

    /// <summary>Binds all active directional lights to the given shader wrapper.</summary>
    public void Bind(ShaderWrapper shader)
    {
        int activeDirectionalLightCount = ClampActiveDirectionalLightCount(ActiveDirectionalLightCount);
        shader.SetParameter(ShaderParameterNames.ActiveDirectionalLightCount, (float)activeDirectionalLightCount);

        for (int i = 0; i < MaxDirectionalLights; i++)
        {
            if (i < activeDirectionalLightCount)
            {
                var d = DirectionalLights[i];
                shader.SetParameter(ShaderParameterNames.DirectionalLightDirectionParameters[i], d.Direction);
                shader.SetParameter(ShaderParameterNames.DirectionalLightDiffuseParameters[i], d.DiffuseColor * d.Intensity);
                shader.SetParameter(ShaderParameterNames.DirectionalLightSpecularParameters[i], d.SpecularColor);
            }
            else
            {
                // Zero out inactive slots so the shader does not accumulate stale light data.
                shader.SetParameter(ShaderParameterNames.DirectionalLightDirectionParameters[i], ZeroVector3);
                shader.SetParameter(ShaderParameterNames.DirectionalLightDiffuseParameters[i], ZeroVector3);
                shader.SetParameter(ShaderParameterNames.DirectionalLightSpecularParameters[i], ZeroVector3);
            }
        }
        shader.SetParameter(ShaderParameterNames.AmbientColor, AmbientColor);
    }
}
