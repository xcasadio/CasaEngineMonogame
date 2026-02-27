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
    public const int MaxDirectionalLights = 3;

    public DirectionalLight[] DirectionalLights { get; } = new DirectionalLight[MaxDirectionalLights];
    public int ActiveDirectionalLightCount { get; set; }
    public Vector3 AmbientColor { get; set; } = new Vector3(0.2f, 0.2f, 0.2f);

    /// <summary>Binds all active directional lights to the given shader wrapper.</summary>
    public void Bind(ShaderWrapper shader)
    {
        for (int i = 0; i < MaxDirectionalLights; i++)
        {
            if (i < ActiveDirectionalLightCount)
            {
                var d = DirectionalLights[i];
                shader.SetParameter($"DirLight{i}Direction",    d.Direction);
                shader.SetParameter($"DirLight{i}DiffuseColor", d.DiffuseColor * d.Intensity);
                shader.SetParameter($"DirLight{i}SpecularColor", d.SpecularColor);
            }
            else
            {
                // Zero out inactive slots so the shader does not accumulate stale light data.
                shader.SetParameter($"DirLight{i}DiffuseColor",  Vector3.Zero);
                shader.SetParameter($"DirLight{i}SpecularColor", Vector3.Zero);
            }
        }
        shader.SetParameter(ShaderParameterNames.AmbientColor, AmbientColor);
    }
}
