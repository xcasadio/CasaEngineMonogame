using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.PostProcess;

/// <summary>
/// Screen-space ambient occlusion of a view (ADR-0116), run after the scene and before the bloom. From the scene depth of the
/// pre-pass (<see cref="SceneDepthPrepass"/>): an occlusion pass into one target, a separable depth-aware blur through a second
/// target, then a multiplication of the HDR scene target by <c>lerp(1, occlusion, intensity)</c>. Every pass is a
/// <see cref="SpriteBatch"/> draw of a texture with <c>Ssao.fx</c>.
/// <para/>
/// The two intermediate targets are given by the caller (the pipeline takes them from <see cref="RenderTargetPool.Shared"/>, stamps
/// their keys and counts them). The render target, the viewport and the blend, depth, rasterizer and sampler states set here are
/// put back before the method returns; the multiplication never touches the depth buffer of the scene target or its alpha.
/// </summary>
internal sealed class SsaoRenderer
{
    internal const string OcclusionTechniqueName = "Ssao_Occlusion";
    internal const string BlurHorizontalTechniqueName = "Ssao_BlurHorizontal";
    internal const string BlurVerticalTechniqueName = "Ssao_BlurVertical";
    internal const string CompositeTechniqueName = "Ssao_Composite";

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly BlendState _multiplyBlend;

    internal SsaoRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;

        // destination colour = destination colour * source colour; the alpha of the scene target is left alone.
        _multiplyBlend = new BlendState
        {
            ColorBlendFunction = BlendFunction.Add,
            ColorSourceBlend = Blend.Zero,
            ColorDestinationBlend = Blend.SourceColor,
            AlphaBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One,
        };
    }

    /// <summary>Number of SSAO compositions since the start. A pass that ran is proved by this count going up.</summary>
    internal int PassCount { get; private set; }

    /// <summary>
    /// Multiplies <paramref name="hdrTarget"/> by the ambient occlusion computed from <paramref name="depth"/>. The two targets
    /// have the size of the view (and of <paramref name="hdrTarget"/>), a one-channel floating-point format and no depth buffer.
    /// </summary>
    /// <param name="projectionScaleX">1 / M11 of the perspective projection of the view.</param>
    /// <param name="projectionScaleY">1 / M22 of the perspective projection of the view.</param>
    internal void Render(
        RenderTarget2D hdrTarget,
        Texture2D depth,
        RenderTarget2D occlusion,
        RenderTarget2D partner,
        ShaderWrapper shader,
        float projectionScaleX,
        float projectionScaleY,
        float radius,
        float bias,
        float intensity)
    {
        int width = hdrTarget.Width;
        int height = hdrTarget.Height;

        var previousViewport = _graphicsDevice.Viewport;
        var previousBlend = _graphicsDevice.BlendState;
        var previousDepth = _graphicsDevice.DepthStencilState;
        var previousRasterizer = _graphicsDevice.RasterizerState;
        var previousSampler = _graphicsDevice.SamplerStates[0];
        var previousSampler1 = _graphicsDevice.SamplerStates[1];

        try
        {
            shader.SetParameter(ShaderParameterNames.SsaoTexelSize, new Vector2(1.0f / width, 1.0f / height));
            shader.SetParameter(ShaderParameterNames.SsaoProjectionScale, new Vector2(projectionScaleX, projectionScaleY));
            shader.SetParameter(ShaderParameterNames.SsaoRadius, radius);
            shader.SetParameter(ShaderParameterNames.SsaoBias, bias);
            shader.SetParameter(ShaderParameterNames.SsaoIntensity, intensity);
            shader.SetParameter(ShaderParameterNames.SsaoSkyThreshold, SceneDepthPrepass.SkyThreshold);
            shader.SetParameter(ShaderParameterNames.SsaoDepthTexture, depth);

            // Occlusion: scene depth -> occlusion.
            shader.SelectTechnique(OcclusionTechniqueName);
            DrawPass(shader, depth, occlusion, BlendState.Opaque);

            // Separable blur that stops at depth edges: horizontal into the partner, vertical back into the occlusion target.
            shader.SelectTechnique(BlurHorizontalTechniqueName);
            DrawPass(shader, occlusion, partner, BlendState.Opaque);
            shader.SelectTechnique(BlurVerticalTechniqueName);
            DrawPass(shader, partner, occlusion, BlendState.Opaque);

            // Composite: multiply the scene target.
            shader.SelectTechnique(CompositeTechniqueName);
            DrawPass(shader, occlusion, hdrTarget, _multiplyBlend);

            PassCount++;
        }
        finally
        {
            _graphicsDevice.SetRenderTarget(hdrTarget);
            _graphicsDevice.Viewport = previousViewport;
            _graphicsDevice.BlendState = previousBlend;
            _graphicsDevice.DepthStencilState = previousDepth;
            _graphicsDevice.RasterizerState = previousRasterizer;
            _graphicsDevice.SamplerStates[0] = previousSampler;
            _graphicsDevice.SamplerStates[1] = previousSampler1;
            _graphicsDevice.Textures[1] = null;
        }
    }

    /// <summary>Draws <paramref name="source"/> over the whole of <paramref name="destination"/> with the selected technique.</summary>
    private void DrawPass(ShaderWrapper shader, Texture2D source, RenderTarget2D destination, BlendState blend)
    {
        _graphicsDevice.SetRenderTarget(destination);
        _graphicsDevice.Viewport = new Viewport(0, 0, destination.Width, destination.Height);
        _spriteBatch.Begin(SpriteSortMode.Immediate, blend,
            SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, shader.Effect);
        _spriteBatch.Draw(source, new Rectangle(0, 0, destination.Width, destination.Height), Color.White);
        _spriteBatch.End();
    }
}
