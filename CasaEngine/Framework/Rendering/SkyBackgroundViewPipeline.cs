using CasaEngine.Framework.Application.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Wraps an existing view pipeline with a simple sky background pass driven by <see cref="SkySettings"/>.
/// </summary>
public sealed class SkyBackgroundViewPipeline : IViewRenderPipeline, IScenePhaseViewPipeline
{
    private readonly IViewRenderPipeline _inner;

    private GraphicsDevice _graphicsDevice;
    private SpriteBatch _spriteBatch;
    private Texture2D _gradientTexture;
    private Texture2D _linearGradientTexture;
    private Texture2D _sunTexture;
    private SkySettings _resourceSkySettings;

    public SkyBackgroundViewPipeline(SkySettings sky, IViewRenderPipeline inner = null)
    {
        Sky = sky ?? throw new ArgumentNullException(nameof(sky));
        _inner = inner ?? DefaultViewPipeline.Instance;
    }

    public SkySettings Sky { get; set; }

    public void RenderView(
        GraphicsDevice graphicsDevice,
        RenderView view,
        in RenderFrame frame,
        IReadOnlyList<IViewFlushableRenderer> renderers)
    {
        EnsureResources(graphicsDevice);
        DrawBackground(in frame);
        _inner.RenderView(graphicsDevice, view, in frame, renderers);
    }

    /// <summary>
    /// True when the wrapped pipeline can render its scene alone (<see cref="IScenePhaseViewPipeline"/>); otherwise this
    /// pipeline cannot split its scene from the interface and is treated as one that does not implement the interface.
    /// </summary>
    public bool SupportsScenePhase => _inner is IScenePhaseViewPipeline { SupportsScenePhase: true };

    /// <summary>The sky background, then the scene of the wrapped pipeline without its interface.</summary>
    public void RenderScene(
        GraphicsDevice graphicsDevice,
        RenderView view,
        in RenderFrame frame,
        IReadOnlyList<IViewFlushableRenderer> renderers)
    {
        if (_inner is not IScenePhaseViewPipeline { SupportsScenePhase: true } innerScene)
        {
            throw new InvalidOperationException("The wrapped view pipeline cannot render its scene without its interface.");
        }

        EnsureResources(graphicsDevice);
        DrawBackground(in frame);
        innerScene.RenderScene(graphicsDevice, view, in frame, renderers);
    }

    private void EnsureResources(GraphicsDevice graphicsDevice)
    {
        if (ReferenceEquals(_graphicsDevice, graphicsDevice)
            && _spriteBatch != null
            && _gradientTexture != null
            && _sunTexture != null
            && ReferenceEquals(_resourceSkySettings, Sky))
        {
            return;
        }

        _spriteBatch?.Dispose();
        _gradientTexture?.Dispose();
        _linearGradientTexture?.Dispose();
        _linearGradientTexture = null;
        _sunTexture?.Dispose();

        _graphicsDevice = graphicsDevice;
        _resourceSkySettings = Sky;
        _spriteBatch = new SpriteBatch(graphicsDevice);
        _gradientTexture = CreateGradientTexture(graphicsDevice, Sky, 256);
        _sunTexture = CreateSunTexture(graphicsDevice, 128);
    }

    private void DrawBackground(in RenderFrame frame)
    {
        if (_spriteBatch == null || _gradientTexture == null || _sunTexture == null)
        {
            return;
        }

        int width = Math.Max(1, frame.ViewportRect.Width);
        int height = Math.Max(1, frame.ViewportRect.Height);
        var viewportRect = new Rectangle(0, 0, width, height);

        // In the linear colour pipeline (ADR-0097) the gradient is a display image: it is drawn from a copy of its colours decoded
        // to linear light, kept in a floating-point texture (decoding into 8 bits would crush the dark end of the gradient).
        bool linearColor = frame.Environment.LinearColorActive;
        Texture2D gradientTexture = linearColor ? EnsureLinearGradientTexture() : _gradientTexture;

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        _spriteBatch.Draw(gradientTexture, viewportRect, Color.White);
        _spriteBatch.End();

        DrawSun(in frame, viewportRect, linearColor);
    }

    private Texture2D EnsureLinearGradientTexture()
    {
        return _linearGradientTexture ??= CreateLinearGradientTexture(_graphicsDevice, Sky, 256);
    }

    private void DrawSun(in RenderFrame frame, Rectangle viewportRect, bool linearColor)
    {
        if (_spriteBatch == null || _sunTexture == null)
        {
            return;
        }

        Vector3 visibleSunDirection = -Sky.GetNormalizedSunDirection();
        Vector3 sunWorldPosition = frame.CameraPosition + visibleSunDirection * 2048.0f;
        Vector3 sunViewPosition = Vector3.Transform(sunWorldPosition, frame.View);
        if (sunViewPosition.Z >= -0.01f)
        {
            return;
        }

        var viewport = new Viewport(frame.ViewportRect);
        Vector3 projected = viewport.Project(sunWorldPosition, frame.Projection, frame.View, Matrix.Identity);
        if (float.IsNaN(projected.X) || float.IsNaN(projected.Y) || projected.Z < 0.0f || projected.Z > 1.0f)
        {
            return;
        }

        float localX = projected.X - frame.ViewportRect.X;
        float localY = projected.Y - frame.ViewportRect.Y;
        float glowSize = Math.Clamp(MathF.Min(viewportRect.Width, viewportRect.Height) * 0.18f, 72.0f, 220.0f);
        float coreSize = glowSize * 0.42f;

        var glowRect = BuildCenteredRectangle(localX, localY, glowSize);
        var coreRect = BuildCenteredRectangle(localX, localY, coreSize);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        // The sun colour is decoded before its 0.65 factor; the white core and the alpha-only falloff texture stay as they are.
        Vector3 sunColor = linearColor ? ColorSpace.Decode(Sky.SunColor.ToVector3()) : Sky.SunColor.ToVector3();
        _spriteBatch.Draw(_sunTexture, glowRect, new Color(new Vector4(sunColor * 0.65f, 1.0f)));
        _spriteBatch.Draw(_sunTexture, coreRect, new Color(new Vector4(Vector3.One, 1.0f)));
        _spriteBatch.End();
    }

    private static Rectangle BuildCenteredRectangle(float centerX, float centerY, float size)
    {
        int intSize = Math.Max(1, (int)MathF.Round(size));
        int left = (int)MathF.Round(centerX - intSize * 0.5f);
        int top = (int)MathF.Round(centerY - intSize * 0.5f);
        return new Rectangle(left, top, intSize, intSize);
    }

    private static Color EvaluateGradientColor(SkySettings sky, int y, int height)
    {
        float v = height == 1 ? 0.5f : y / (float)(height - 1);
        float vertical = 1.0f - v * 2.0f;
        Vector3 direction = Vector3.Normalize(new Vector3(0.0f, vertical, 1.0f));
        return ProceduralSkyCubeFactory.EvaluateColor(sky, direction, includeSun: false);
    }

    private static Texture2D CreateGradientTexture(GraphicsDevice graphicsDevice, SkySettings sky, int height)
    {
        var texture = new Texture2D(graphicsDevice, 1, height);
        var data = new Color[height];

        for (int y = 0; y < height; y++)
        {
            data[y] = EvaluateGradientColor(sky, y, height);
        }

        texture.SetData(data);
        return texture;
    }

    private static Texture2D CreateLinearGradientTexture(GraphicsDevice graphicsDevice, SkySettings sky, int height)
    {
        var texture = new Texture2D(graphicsDevice, 1, height, false, SurfaceFormat.HalfVector4);
        var data = new HalfVector4[height];

        for (int y = 0; y < height; y++)
        {
            data[y] = new HalfVector4(ColorSpace.Decode(EvaluateGradientColor(sky, y, height)));
        }

        texture.SetData(data);
        return texture;
    }

    private static Texture2D CreateSunTexture(GraphicsDevice graphicsDevice, int size)
    {
        var texture = new Texture2D(graphicsDevice, size, size);
        var data = new Color[size * size];
        float radius = size * 0.5f;
        var center = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / radius;
                float falloff = Math.Clamp(1.0f - distance, 0.0f, 1.0f);
                float intensity = falloff * falloff * falloff;
                data[y * size + x] = new Color((byte)255, (byte)255, (byte)255, (byte)(intensity * 255.0f));
            }
        }

        texture.SetData(data);
        return texture;
    }
}