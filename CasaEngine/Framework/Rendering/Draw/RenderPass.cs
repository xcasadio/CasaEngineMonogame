using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Draw;

// ---------------------------------------------------------------------------
//  RenderPassType
// ---------------------------------------------------------------------------

/// <summary>Identifies the purpose and ordering of a <see cref="RenderPass"/>.</summary>
public enum RenderPassType
{
    /// <summary>Writes to the depth buffer only (no colour output).</summary>
    DepthPrePass,
    /// <summary>Renders opaque geometry front-to-back.</summary>
    OpaquePass,
    /// <summary>Renders transparent geometry back-to-front.</summary>
    TransparentPass,
    /// <summary>Renders screen-space overlays (UI, gizmos) on top of everything.</summary>
    OverlayPass,
    /// <summary>Renders shadow-casting geometry into a shadow map.</summary>
    ShadowPass,
}

// ---------------------------------------------------------------------------
//  RenderPass
// ---------------------------------------------------------------------------

/// <summary>
/// Abstract base for a single rendering pass in the forward pipeline.
/// Subclasses filter, sort and draw a subset of the frame's <see cref="RenderItem"/>s.
/// </summary>
public abstract class RenderPass
{
    /// <summary>Identifies the role of this pass (used for ordering and filtering).</summary>
    public RenderPassType Type { get; }

    protected RenderPass(RenderPassType type)
    {
        Type = type;
    }

    /// <summary>
    /// Execute this pass: filter <paramref name="items"/> as needed, set GPU state,
    /// bind shaders and issue draw calls.
    /// </summary>
    public abstract void Execute(
        RenderContext context,
        IReadOnlyList<RenderItem> items,
        RenderStateCache stateCache,
        ShaderBindCache shaderCache,
        RenderShaderSelector shaderSelector);

    // -----------------------------------------------------------------------
    //  Helpers shared by concrete passes
    // -----------------------------------------------------------------------

    protected static void DrawItem(
        in RenderItem item,
        in RenderContext context,
        RenderStateCache stateCache,
        ShaderBindCache shaderCache,
        RenderShaderSelector shaderSelector,
        RenderStats? stats)
    {
        var resolvedShader = shaderSelector.Resolve(in item);
        var shader = resolvedShader.Shader;
        var device = context.Device;
        device.SetVertexBuffer(item.Mesh.VertexBuffer);
        device.Indices = item.Mesh.IndexBuffer;

        stateCache.Apply(device, item, stats);
        if (item.Material.RequiresMaterialTechniqueSelection(
            resolvedShader.TechniqueSelectedBySelector,
            in context,
            item.Features))
        {
            item.Material.SelectTechnique(shader, in context, item.Features);
        }
        shaderCache.BindGlobals(shader, in context);
        item.Material.Bind(shader, in context, item.World);
        item.PropertyOverrides?.Apply(shader, context.Stats);

        if (item.SubMesh is { } sub)
        {
            for (int p = 0; p < shader.PassCount; p++)
            {
                shader.ApplyPass(p);
                device.DrawIndexedPrimitives(item.Mesh.PrimitiveType,
                    sub.VertexOffset, sub.IndexStart, sub.PrimitiveCount);
            }
        }
        else
        {
            int primCount = item.Mesh.IndexBuffer!.IndexCount / 3;
            for (int p = 0; p < shader.PassCount; p++)
            {
                shader.ApplyPass(p);
                device.DrawIndexedPrimitives(item.Mesh.PrimitiveType, 0, 0, primCount);
            }
        }

        if (stats is not null)
        {
            stats.DrawCalls++;
        }
    }
}
