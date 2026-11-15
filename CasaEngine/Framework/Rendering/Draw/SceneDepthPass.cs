using CasaEngine.Framework.Materials.Runtime;
using CasaEngine.Framework.Rendering.PostProcess;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Draw;

/// <summary>
/// Writes the view depth of the opaque and alpha-tested static geometry into the scene depth pre-pass target (ADR-0115), when the
/// pre-pass of the view is active (<see cref="SceneDepthPrepass.IsActive"/>); does nothing otherwise.
/// <para>
/// The list is walked like <see cref="OpaquePass"/> walks it, so that the pre-pass draws exactly what the colour pass draws as
/// opaque: items of a queue below <see cref="RenderQueue.Transparent"/>, with vertex and index buffers, instanced items skipped.
/// It deliberately does not use the shadow filter (<c>EffectiveCastShadows</c>): an object that casts no shadow still occludes.
/// Each item is counted in <see cref="RenderStats.SceneDepthStaticDraws"/>, which therefore equals <see cref="RenderStats.OpaqueItems"/>
/// of the same flush.
/// </para>
/// <para>
/// Alpha cut-out: an item whose material has a <see cref="MaterialBase.ShadowMaskTexture"/> (the alpha-tested queue) is drawn with the
/// textured technique and the cutoff of the material, on the alpha of that texture alone, as the shadow pass does. The colour pass
/// cuts on the product of the alpha factor and the texture alpha, so the two differ when the factor is under 1 (a written limit).
/// </para>
/// </summary>
public sealed class SceneDepthPass : RenderPass
{
    public SceneDepthPass() : base(RenderPassType.SceneDepthPass)
    {
    }

    /// <summary>The <c>SceneDepth.fx</c> effect; null until the renderer has loaded it, in which case the pass does nothing.</summary>
    public ShaderWrapper SceneDepthShader { get; set; }

    public override void Execute(
        RenderContext context,
        IReadOnlyList<RenderItem> items,
        RenderStateCache stateCache,
        ShaderBindCache shaderCache,
        RenderShaderSelector shaderSelector)
    {
        var prepass = context.SceneDepth;
        if (SceneDepthShader is null || prepass is null || !prepass.IsActive)
        {
            return;
        }

        var device = context.Device;
        if (!prepass.BeginDraw(device))
        {
            return;
        }

        try
        {
            device.SetVertexBuffer(null);
            device.Indices = null;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                // Sorted by key: from the first transparent or overlay item on, everything is transparent or overlay.
                if (item.Queue >= RenderQueue.Transparent)
                {
                    break;
                }

                if ((item.Features & ShaderFeature.Instanced) != 0
                    || item.Mesh.VertexBuffer is null
                    || item.Mesh.IndexBuffer is null)
                {
                    continue;
                }

                DrawItemDepth(in item, in context);
            }
        }
        finally
        {
            prepass.EndDraw(device);
        }
    }

    private void DrawItemDepth(in RenderItem item, in RenderContext context)
    {
        var device = context.Device;
        var shader = SceneDepthShader!;

        device.SetVertexBuffer(item.Mesh.VertexBuffer);
        device.Indices = item.Mesh.IndexBuffer;
        device.RasterizerState = item.CompiledMaterial?.RasterizerState
            ?? item.Material.RasterizerState
            ?? RasterizerState.CullCounterClockwise;

        Texture2D maskTexture = item.Material.ShadowMaskTexture;
        if (maskTexture is not null)
        {
            device.SamplerStates[0] = item.CompiledMaterial?.SamplerState
                ?? item.Material.SamplerState
                ?? SamplerState.AnisotropicClamp;
            shader.SelectTechnique(SceneDepthPrepass.TexturedTechniqueName);
            shader.SetTextureParameter(ShaderParameterNames.BasColorTexture, maskTexture, context.Stats);
            shader.SetParameter(ShaderParameterNames.AlphaCutoff, item.Material.AlphaCutoff);
        }
        else
        {
            shader.SelectTechnique(SceneDepthPrepass.SolidTechniqueName);
            shader.SetParameter(ShaderParameterNames.AlphaCutoff, 0.0f);
        }

        shader.SetParameter(ShaderParameterNames.WorldViewProj, item.World * context.Frame.ViewProjection);

        if (item.SubMesh is { } subMesh)
        {
            for (int passIndex = 0; passIndex < shader.PassCount; passIndex++)
            {
                shader.ApplyPass(passIndex);
                device.DrawIndexedPrimitives(item.Mesh.PrimitiveType, subMesh.VertexOffset, subMesh.IndexStart, subMesh.PrimitiveCount);
            }
        }
        else
        {
            int primitiveCount = item.Mesh.IndexBuffer.IndexCount / 3;
            for (int passIndex = 0; passIndex < shader.PassCount; passIndex++)
            {
                shader.ApplyPass(passIndex);
                device.DrawIndexedPrimitives(item.Mesh.PrimitiveType, 0, 0, primitiveCount);
            }
        }

        if (context.Stats is not null)
        {
            context.Stats.SceneDepthStaticDraws++;
            context.Stats.DrawCalls++;
        }
    }
}
