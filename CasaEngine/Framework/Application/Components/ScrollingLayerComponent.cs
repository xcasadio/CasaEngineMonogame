using CasaEngine.Core.Logging;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngineTexture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Application.Components;

/// <summary>
/// Drives <see cref="ScrollingLayers.ScrollingLayerService"/> from the game loop: resolves the
/// service's texture ids into <see cref="Texture2D"/>s (the one GPU-facing job the service itself
/// cannot do - it holds no <c>Texture2D</c>), advances the pending frame and submits the covering
/// quads through <see cref="SpriteRendererComponent"/>. Same house pattern as
/// <see cref="ScreenEffectComponent"/> (docs/engine/scrolling-layers.md).
///
/// Submission happens from this component's own <see cref="Update"/>, not <c>Draw</c> - same
/// reasoning as <see cref="ScreenEffectComponent"/>: the game DLL pushes this frame's scroll/ticks to
/// <see cref="Service"/> from <c>GameManager.UpdateWorld</c>, which every <c>GameComponent.Update</c>
/// (this one included) runs after, so the state read here is already final for the frame.
/// </summary>
public class ScrollingLayerComponent : GameComponent
{
    private readonly CasaEngineGame _game;
    private Texture2D[][] _layerFrames = System.Array.Empty<Texture2D[]>();
    private int _resolvedLayersVersion = -1;
    private Texture2D _whiteTexture;
    private bool _isDisposed;

    public ScrollingLayerComponent(Game game) : base(game)
    {
        ArgumentNullException.ThrowIfNull(game);

        _game = game as CasaEngineGame;
        Service = new ScrollingLayerService();

        UpdateOrder = (int)ComponentUpdateOrder.ScrollingLayers;
        game.Components.Add(this);
    }

    /// <summary>Layer/tint definitions, per-tick state and the frame push contract.</summary>
    public ScrollingLayerService Service { get; }

    public override void Update(GameTime gameTime)
    {
        if (_resolvedLayersVersion != Service.LayersVersion)
        {
            ResolveTextures(LoadTexture);
            _resolvedLayersVersion = Service.LayersVersion;
        }

        if (Service.HasPendingFrame)
        {
            Service.Advance();
        }

        if (Service.FramesPushed > 0 && _game?.SpriteRendererComponent != null)
        {
            var scissorRectangle = ResolveScissorRectangle();
            Submit(_game.SpriteRendererComponent, Service.CameraTarget, scissorRectangle);
        }

        base.Update(gameTime);
    }

    /// <summary>
    /// Resolves every current layer's frame textures through <paramref name="loader"/> (production:
    /// <c>AssetContentManager.Load&lt;Texture&gt;</c> + <c>.Load(acm)</c> + <c>.Resource</c>), applying
    /// D-E9-9's fallback exactly as the DLL's own <c>BackdropRenderer.LoadLayerFrames</c> did: a null
    /// id is never passed to <paramref name="loader"/> (resolves to a null texture directly); frame 0
    /// resolving to null skips the whole layer; frame <c>f &gt;= 1</c> resolving to null truncates the
    /// layer to <c>[frame0]</c> only, never a partial/sparse array. Called by <see cref="Update"/>
    /// whenever <see cref="ScrollingLayerService.LayersVersion"/> changes - never per frame - and
    /// explicitly resets every layer's per-tick runtime state at that same occasion
    /// (<see cref="ScrollingLayerService.ResetLayerRuntimeState"/>, D-E9b-7): a bare
    /// <see cref="ScrollingLayerService.SetTint"/> bumps <see cref="ScrollingLayerService.LayersVersion"/>
    /// without rebuilding the layer array, so without this explicit reset its counters would keep
    /// accumulating instead of restarting - the production sequence <c>Clear</c> → <c>SetLayers</c> →
    /// <c>SetTint</c> is unaffected since <c>SetLayers</c> already starts every layer zeroed.
    /// </summary>
    public void ResolveTextures(System.Func<System.Guid, Texture2D> loader)
    {
        var layerCount = Service.LayerCount;
        var layerFrames = new Texture2D[layerCount][];

        for (var i = 0; i < layerCount; i++)
        {
            var definition = Service.GetLayerDefinition(i);
            var frameIds = definition.FrameTextureAssetIds ?? System.Array.Empty<System.Guid>();
            layerFrames[i] = ResolveLayerFrames(frameIds, loader, i, definition.StableId);
        }

        _layerFrames = layerFrames;
        Service.ResetLayerRuntimeState();
    }

    /// <summary>D-E9-9's fallback rule, extracted as its own internal static method (no
    /// <see cref="Texture2D"/> loading machinery beyond <paramref name="loader"/>) so it is testable
    /// with a synthetic delegate - mirrors the DLL's own (now-retired) <c>BackdropRenderer.LoadLayerFrames</c>,
    /// including its warning on both failure branches (docs/engine/scrolling-layers.md). Unlike the DLL's
    /// version this service is asset-agnostic (no world name); <paramref name="layerIndex"/>/
    /// <paramref name="stableId"/> identify the layer instead - both default so existing callers that
    /// only care about the fallback rule itself need not pass them.</summary>
    internal static Texture2D[] ResolveLayerFrames(
        System.Guid[] frameIds, System.Func<System.Guid, Texture2D> loader, int layerIndex = -1, int stableId = 0)
    {
        var frames = new System.Collections.Generic.List<Texture2D>(frameIds.Length);

        for (var frameIndex = 0; frameIndex < frameIds.Length; frameIndex++)
        {
            var frameId = frameIds[frameIndex];
            var texture = frameId == System.Guid.Empty ? null : loader(frameId);

            if (texture == null)
            {
                if (frameIndex == 0)
                {
                    Logs.WriteWarning(
                        $"ScrollingLayerComponent: layer index {layerIndex} (StableId {stableId}) frame 0 "
                        + $"texture '{frameId}' failed to load; layer skipped (no world name available - "
                        + "the service is asset-agnostic).");
                    return System.Array.Empty<Texture2D>();
                }

                Logs.WriteWarning(
                    $"ScrollingLayerComponent: layer index {layerIndex} (StableId {stableId}) frame "
                    + $"{frameIndex} texture '{frameId}' failed to load; layer falls back to frame 0 only.");
                return new[] { frames[0] };
            }

            frames.Add(texture);
        }

        return frames.ToArray();
    }

    private Texture2D LoadTexture(System.Guid id)
    {
        var wrapperTexture = _game?.AssetContentManager.Load<CasaEngineTexture>(id);
        wrapperTexture?.Load(_game.AssetContentManager);
        return wrapperTexture?.Resource;
    }

    /// <summary>
    /// Submits the tint quad (if any) then every layer's covering quads (D-E9b-5/D-E9b-6), through the
    /// scissor-explicit keyed <c>DrawSprite</c> overload - never the device's current scissor rectangle,
    /// so this method never touches <see cref="GraphicsDevice"/> and is exercisable headless. No
    /// allocation: the covering-quad bounds are computed by <see cref="ScrollingLayerService.CoveringOriginStart"/>/
    /// <see cref="ScrollingLayerService.CoveringOriginCount"/> and walked with a plain nested loop.
    /// Submits nothing if <see cref="ScrollingLayerService.FramesPushed"/> is still 0 (no
    /// <see cref="ScrollingLayerService.SetFrame"/> received yet since the last
    /// <see cref="ScrollingLayerService.Clear"/>/<see cref="ScrollingLayerService.SetLayers"/> - editor
    /// preview with <c>UpdateGameplayScripts = false</c>, or before the DLL's first frame).
    /// </summary>
    public void Submit(SpriteRendererComponent renderer, Vector3 cameraTarget, Rectangle scissorRectangle)
    {
        if (renderer == null || Service.FramesPushed == 0)
        {
            return;
        }

        var configuration = Service.Configuration;
        var halfWidth = configuration.ViewWidth / 2f;
        var halfHeight = configuration.ViewHeight / 2f;

        var tint = Service.Tint;
        if (tint.HasValue)
        {
            var whiteTexture = GetOrCreateWhiteTexture();
            if (whiteTexture != null)
            {
                var tintWorldPosition = new Vector2(cameraTarget.X - halfWidth, cameraTarget.Y + halfHeight);

                renderer.DrawSprite(
                    whiteTexture,
                    whiteTexture.Bounds,
                    Point.Zero,
                    tintWorldPosition,
                    0f,
                    new Vector2(configuration.ViewWidth, configuration.ViewHeight),
                    tint.Value.Color,
                    0f,
                    tint.Value.SortKey,
                    SpriteEffects.None,
                    scissorRectangle,
                    SpriteBlendMode.AlphaBlend);
            }
        }

        for (var i = 0; i < Service.LayerCount; i++)
        {
            if (!Service.TryGetLayerState(i, out var state))
            {
                continue;
            }

            if (i >= _layerFrames.Length || _layerFrames[i].Length == 0)
            {
                continue;
            }

            var frames = _layerFrames[i];
            var frameIndex = state.AnimFrameCounter < frames.Length ? state.AnimFrameCounter : frames.Length - 1;
            var frame = frames[frameIndex];
            if (frame == null)
            {
                continue;
            }

            var definition = Service.GetLayerDefinition(i);
            var sortKey = new RenderSortKey2D((int)definition.Pass, definition.SortingLayer, definition.OrderInLayer, 0, 0, 0, definition.StableId);

            var startX = ScrollingLayerService.CoveringOriginStart(state.LayerOffsetX, configuration.CanvasWidth);
            var countX = ScrollingLayerService.CoveringOriginCount(configuration.ViewWidth, startX, configuration.CanvasWidth);
            var startY = ScrollingLayerService.CoveringOriginStart(state.LayerOffsetY, configuration.CanvasHeight);
            var countY = ScrollingLayerService.CoveringOriginCount(configuration.ViewHeight, startY, configuration.CanvasHeight);

            for (var row = 0; row < countY; row++)
            {
                var originY = startY + row * configuration.CanvasHeight;

                for (var col = 0; col < countX; col++)
                {
                    var originX = startX + col * configuration.CanvasWidth;

                    var worldPosition = new Vector2(
                        cameraTarget.X + (originX - halfWidth),
                        cameraTarget.Y + (halfHeight - originY));

                    renderer.DrawSprite(
                        frame,
                        frame.Bounds,
                        Point.Zero,
                        worldPosition,
                        0f,
                        Vector2.One,
                        definition.Tint,
                        0f,
                        sortKey,
                        SpriteEffects.None,
                        scissorRectangle,
                        definition.Blend);
                }
            }
        }
    }

    private Rectangle ResolveScissorRectangle()
    {
        GraphicsDevice graphicsDevice;
        try
        {
            // Same guard as ScreenEffectComponent.GetOrCreatePixelTexture: a headless/partially
            // constructed Game can throw from the GraphicsDevice getter itself rather than returning
            // null - either way, no device means fall back to the pixel-size rectangle below.
            graphicsDevice = _game?.GraphicsDevice;
        }
        catch (System.Exception)
        {
            graphicsDevice = null;
        }

        if (graphicsDevice != null)
        {
            return graphicsDevice.ScissorRectangle;
        }

        var width = _game?.ScreenSizeWidth ?? 0;
        var height = _game?.ScreenSizeHeight ?? 0;
        return new Rectangle(0, 0, width, height);
    }

    private Texture2D GetOrCreateWhiteTexture()
    {
        if (_whiteTexture != null)
        {
            return _whiteTexture;
        }

        GraphicsDevice graphicsDevice;
        try
        {
            graphicsDevice = _game?.GraphicsDevice;
        }
        catch (System.Exception)
        {
            return null;
        }

        if (graphicsDevice == null)
        {
            return null;
        }

        _whiteTexture = new Texture2D(graphicsDevice, 1, 1, false, SurfaceFormat.Color);
        _whiteTexture.SetData(new[] { Color.White });
        return _whiteTexture;
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing && !_isDisposed)
            {
                _whiteTexture?.Dispose();
                _whiteTexture = null;
                _isDisposed = true;
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
}
