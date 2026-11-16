using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.PostProcess;

/// <summary>
/// The FXAA pass (ADR-0114): draws a tonemapped, display-encoded colour texture 1:1 into the current render target with
/// <c>Fxaa.fx</c>, smoothing the luminance edges. It is a single <see cref="SpriteBatch"/> draw, so the destination keeps its
/// binding and viewport; the blend, depth, rasterizer and sampler states the draw sets are put back before it returns.
/// </summary>
internal sealed class FxaaRenderer
{
    internal const string TechniqueName = "Fxaa";

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;

    internal FxaaRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;
    }

    /// <summary>Number of FXAA draws since the start. A pass that ran is proved by this count going up.</summary>
    internal int DrawCount { get; private set; }

    /// <summary>
    /// Draws <paramref name="source"/> over the rectangle (0, 0, <paramref name="width"/>, <paramref name="height"/>) of the
    /// current viewport. <paramref name="source"/> must be of that size: one texel per destination pixel.
    /// </summary>
    internal void Draw(ShaderWrapper shader, Texture2D source, int width, int height)
    {
        var previousBlend = _graphicsDevice.BlendState;
        var previousDepth = _graphicsDevice.DepthStencilState;
        var previousRasterizer = _graphicsDevice.RasterizerState;
        var previousSampler = _graphicsDevice.SamplerStates[0];

        try
        {
            shader.SelectTechnique(TechniqueName);
            shader.SetParameter(ShaderParameterNames.FxaaTexelSize, new Vector2(1.0f / source.Width, 1.0f / source.Height));

            _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, shader.Effect);
            _spriteBatch.Draw(source, new Rectangle(0, 0, width, height), Color.White);
            _spriteBatch.End();

            DrawCount++;
        }
        finally
        {
            _graphicsDevice.BlendState = previousBlend;
            _graphicsDevice.DepthStencilState = previousDepth;
            _graphicsDevice.RasterizerState = previousRasterizer;
            _graphicsDevice.SamplerStates[0] = previousSampler;
        }
    }
}
