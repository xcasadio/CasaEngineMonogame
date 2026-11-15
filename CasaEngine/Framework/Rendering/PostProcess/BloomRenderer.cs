using CasaEngine.Core.Logging;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.PostProcess;

/// <summary>
/// Bloom of the HDR scene target of a view, applied before the tonemapping (ADR-0099). From the scene target: a bright pass
/// to half size, two downsamples (quarter and eighth size), a separable blur of each level through a partner target of the
/// same size, then the three blurred levels are added back to the scene target. Every pass is a <see cref="SpriteBatch"/>
/// draw of a texture with <c>Bloom.fx</c>.
/// <para/>
/// The targets have the format of the scene target and no depth buffer, are taken from <see cref="RenderTargetPool.Shared"/>
/// and given back at the end of the pass; their keys are stamped in the tracking map of the pipeline, which evicts them by the
/// rule of <see cref="HdrTargetEviction"/>. The render target, the viewport and the blend, depth, rasterizer and sampler
/// states set by the pass are put back before it returns.
/// </summary>
internal sealed class BloomRenderer
{
    internal const string BrightPassTechniqueName = "Bloom_BrightPass";
    internal const string DownsampleTechniqueName = "Bloom_Downsample";
    internal const string BlurHorizontalTechniqueName = "Bloom_BlurHorizontal";
    internal const string BlurVerticalTechniqueName = "Bloom_BlurVertical";
    internal const string CompositeTechniqueName = "Bloom_Composite";

    /// <summary>Number of reduced levels (half, quarter and eighth size).</summary>
    internal const int LevelCount = 3;

    // The levels carry the same energy (each one is the bright part, blurred at its own scale), so the composite averages them.
    private const float LevelWeight = 1.0f / LevelCount;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly BlendState _additiveBlend;

    private readonly RenderTarget2D[] _levels = new RenderTarget2D[LevelCount];
    private readonly RenderTarget2D[] _partners = new RenderTarget2D[LevelCount];

    private bool _colorFormatWarningLogged;

    internal BloomRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;

        // Adds the colour of the source to the scene and leaves its alpha alone (the tonemapping ignores it).
        _additiveBlend = new BlendState
        {
            ColorBlendFunction = BlendFunction.Add,
            ColorSourceBlend = Blend.One,
            ColorDestinationBlend = Blend.One,
            AlphaBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One,
        };
    }

    /// <summary>Number of bloom targets created since the start (one per size and format, then reused from the pool).</summary>
    internal int TargetCreationCount { get; private set; }

    /// <summary>
    /// Adds the bloom to <paramref name="hdrTarget"/>. Returns false, drawing nothing, when the format of the scene target has
    /// no headroom above 1 (<see cref="SurfaceFormat.Color"/>, warned once) or when the pass would add nothing.
    /// </summary>
    /// <param name="lastUsedRender">The tracking map of the pipeline; the key of every target taken is stamped with <paramref name="renderIndex"/>.</param>
    internal bool Render(
        RenderTarget2D hdrTarget,
        ShaderWrapper shader,
        float threshold,
        float intensity,
        float radius,
        Dictionary<(int, int, SurfaceFormat, DepthFormat), long> lastUsedRender,
        long renderIndex)
    {
        if (hdrTarget.Format == SurfaceFormat.Color)
        {
            if (!_colorFormatWarningLogged)
            {
                _colorFormatWarningLogged = true;
                Logs.WriteWarning("[Bloom] The HDR scene target fell back to Color: no value above 1 survives, so bloom is skipped.");
            }

            return false;
        }

        if (intensity <= 0.0f)
        {
            return false;
        }

        int width = hdrTarget.Width;
        int height = hdrTarget.Height;
        var format = hdrTarget.Format;

        var previousViewport = _graphicsDevice.Viewport;
        var previousBlend = _graphicsDevice.BlendState;
        var previousDepth = _graphicsDevice.DepthStencilState;
        var previousRasterizer = _graphicsDevice.RasterizerState;
        var previousSampler = _graphicsDevice.SamplerStates[0];

        int acquired = 0;
        try
        {
            int levelWidth = width;
            int levelHeight = height;
            for (int level = 0; level < LevelCount; level++)
            {
                levelWidth = Math.Max(1, levelWidth / 2);
                levelHeight = Math.Max(1, levelHeight / 2);
                _levels[level] = AcquireTarget(levelWidth, levelHeight, format, lastUsedRender, renderIndex);
                acquired++;
                _partners[level] = AcquireTarget(levelWidth, levelHeight, format, lastUsedRender, renderIndex);
                acquired++;
            }

            shader.SetParameter(ShaderParameterNames.BloomThreshold, threshold);

            // Bright pass: scene -> half size.
            shader.SelectTechnique(BrightPassTechniqueName);
            DrawPass(shader, hdrTarget, _levels[0], BlendState.Opaque, new Vector2(1.0f / width, 1.0f / height));

            // Reductions: half -> quarter -> eighth.
            shader.SelectTechnique(DownsampleTechniqueName);
            for (int level = 1; level < LevelCount; level++)
            {
                var source = _levels[level - 1];
                DrawPass(shader, source, _levels[level], BlendState.Opaque, new Vector2(1.0f / source.Width, 1.0f / source.Height));
            }

            // Separable blur of each level: horizontal into the partner, vertical back into the level.
            for (int level = 0; level < LevelCount; level++)
            {
                var target = _levels[level];
                var tapSpacing = new Vector2(radius / target.Width, radius / target.Height);

                shader.SelectTechnique(BlurHorizontalTechniqueName);
                DrawPass(shader, target, _partners[level], BlendState.Opaque, tapSpacing);

                shader.SelectTechnique(BlurVerticalTechniqueName);
                DrawPass(shader, _partners[level], target, BlendState.Opaque, tapSpacing);
            }

            // Composite: the three levels, scaled, added to the scene target.
            shader.SelectTechnique(CompositeTechniqueName);
            shader.SetParameter(ShaderParameterNames.BloomIntensity, intensity * LevelWeight);
            _graphicsDevice.SetRenderTarget(hdrTarget);
            _graphicsDevice.Viewport = new Viewport(0, 0, width, height);
            _spriteBatch.Begin(SpriteSortMode.Immediate, _additiveBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, shader.Effect);
            var sceneRectangle = new Rectangle(0, 0, width, height);
            for (int level = 0; level < LevelCount; level++)
            {
                _spriteBatch.Draw(_levels[level], sceneRectangle, Color.White);
            }

            _spriteBatch.End();
        }
        finally
        {
            for (int index = 0; index < acquired; index++)
            {
                var target = (index & 1) == 0 ? _levels[index >> 1] : _partners[index >> 1];
                RenderTargetPool.Shared.Release(target);
            }

            for (int level = 0; level < LevelCount; level++)
            {
                _levels[level] = null;
                _partners[level] = null;
            }

            _graphicsDevice.SetRenderTarget(hdrTarget);
            _graphicsDevice.Viewport = previousViewport;
            _graphicsDevice.BlendState = previousBlend;
            _graphicsDevice.DepthStencilState = previousDepth;
            _graphicsDevice.RasterizerState = previousRasterizer;
            _graphicsDevice.SamplerStates[0] = previousSampler;
        }

        return true;
    }

    /// <summary>Draws <paramref name="source"/> over the whole of <paramref name="destination"/> with the selected technique.</summary>
    private void DrawPass(ShaderWrapper shader, Texture2D source, RenderTarget2D destination, BlendState blend, Vector2 texelSize)
    {
        shader.SetParameter(ShaderParameterNames.BloomTexelSize, texelSize);

        _graphicsDevice.SetRenderTarget(destination);
        _graphicsDevice.Viewport = new Viewport(0, 0, destination.Width, destination.Height);
        _spriteBatch.Begin(SpriteSortMode.Immediate, blend,
            SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, shader.Effect);
        _spriteBatch.Draw(source, new Rectangle(0, 0, destination.Width, destination.Height), Color.White);
        _spriteBatch.End();
    }

    /// <summary>
    /// Takes a bloom target (the format of the scene target, no depth) from the shared pool and stamps its key. A creation is
    /// counted and logged at Debug level.
    /// </summary>
    private RenderTarget2D AcquireTarget(
        int width,
        int height,
        SurfaceFormat format,
        Dictionary<(int, int, SurfaceFormat, DepthFormat), long> lastUsedRender,
        long renderIndex)
    {
        var pool = RenderTargetPool.Shared;
        int countBefore = pool.TotalCount;

        var target = pool.Acquire(width, height, format, DepthFormat.None);
        lastUsedRender[(width, height, format, DepthFormat.None)] = renderIndex;

        if (pool.TotalCount != countBefore)
        {
            TargetCreationCount++;
            Logs.WriteDebug($"[Bloom] target created: {width}x{height} {format}, render {renderIndex}, total {TargetCreationCount}");
        }

        return target;
    }
}
