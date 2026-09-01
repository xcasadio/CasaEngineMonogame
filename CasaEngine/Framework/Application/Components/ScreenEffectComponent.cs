using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Application.Components;

/// <summary>
/// Drives the engine screen fade/tint overlay from the game loop.
/// </summary>
/// <remarks>
/// All the logic lives in <see cref="ScreenEffects.ScreenEffectService"/>, which knows nothing
/// about MonoGame: this component only owns the 1x1 overlay pixel and submits it through
/// <see cref="SpriteRendererComponent"/> - the same house pattern as
/// <see cref="AudioSystemComponent"/>/<see cref="Audio.AudioService"/>.
///
/// Submission happens from this component's own <see cref="Update"/>, not <c>Draw</c>:
/// <c>CasaEngineGame.Update</c> runs <c>GameManager.UpdateWorld</c> (where the game DLL pushes this
/// frame's fade/tint state to <see cref="Service"/>) before every <c>GameComponent.Update</c>, so by
/// the time this component runs, the state for the frame is already final. Submitting queues one
/// quad into <see cref="SpriteRendererComponent"/>'s sorted list, consumed at
/// <see cref="Rendering.Depth.RenderPass2D.ScreenEffects"/> on the next <c>Flush</c>.
/// </remarks>
public class ScreenEffectComponent : GameComponent
{
    private static readonly Rectangle PixelSource = new(0, 0, 1, 1);

    private readonly CasaEngineGame _game;
    private Texture2D _pixelTexture;
    private bool _isDisposed;

    public ScreenEffectComponent(Game game) : base(game)
    {
        ArgumentNullException.ThrowIfNull(game);

        _game = game as CasaEngineGame;
        Service = new ScreenEffectService();

        UpdateOrder = (int)ComponentUpdateOrder.ScreenEffects;
        game.Components.Add(this);
    }

    /// <summary>Overlay state: colour, blend mode, active flag, ramp.</summary>
    public ScreenEffectService Service { get; }

    public override void Update(GameTime gameTime)
    {
        Service.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        if (_game?.SpriteRendererComponent != null)
        {
            var camera = _game.GameManager?.ViewManager?.ActiveView?.Camera as Camera2dComponent;
            var cameraPosition = camera?.Target ?? Vector3.Zero;

            SubmitOverlay(_game.SpriteRendererComponent, cameraPosition, _game.ScreenSizeWidth, _game.ScreenSizeHeight);
        }

        base.Update(gameTime);
    }

    /// <summary>
    /// Submits the full-viewport overlay quad, if <see cref="Service"/> is active. Every input is
    /// caller-supplied - no <see cref="GraphicsDevice"/>, no <c>ScreenSizeWidth</c>/<c>Height</c>, no
    /// <c>ActiveView</c> is read here - so this method is exercisable with no device at all, by
    /// passing an explicit <paramref name="overlayTexture"/>. When <paramref name="overlayTexture"/>
    /// is null, the component's own 1x1 pixel is used, lazily created against the live
    /// <see cref="GraphicsDevice"/> (bypassed - nothing is submitted - if none is available).
    /// </summary>
    /// <remarks>
    /// The placement formula mirrors the DLL's <c>BackdropRenderer.Draw</c> tint block exactly: a
    /// quad scaled to the full viewport, positioned at <c>cameraPosition - halfViewport</c> with the Y
    /// flip this engine's 2D world (+Y up) needs against screen space (+Y down), so it cancels the
    /// active camera's own view transform and always covers the screen regardless of where the
    /// camera is.
    /// </remarks>
    public void SubmitOverlay(SpriteRendererComponent renderer, Vector3 cameraPosition, int viewportWidth, int viewportHeight, Texture2D overlayTexture = null)
    {
        if (!Service.Active || renderer == null || viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        var texture = overlayTexture ?? GetOrCreatePixelTexture();
        if (texture == null)
        {
            return;
        }

        var halfWidth = viewportWidth / 2f;
        var halfHeight = viewportHeight / 2f;
        var worldPosition = new Vector2(cameraPosition.X - halfWidth, cameraPosition.Y + halfHeight);
        var color = new Color(Service.R, Service.G, Service.B);
        var sortKey = new RenderSortKey2D((int)RenderPass2D.ScreenEffects, 0, 0, 0, 0, 0, 0);
        var fullViewport = new Rectangle(0, 0, viewportWidth, viewportHeight);

        renderer.DrawSprite(
            texture,
            PixelSource,
            Point.Zero,
            worldPosition,
            0f,
            new Vector2(viewportWidth, viewportHeight),
            color,
            0f,
            sortKey,
            SpriteEffects.None,
            fullViewport,
            Service.Blend);
    }

    private Texture2D GetOrCreatePixelTexture()
    {
        if (_pixelTexture != null)
        {
            return _pixelTexture;
        }

        GraphicsDevice graphicsDevice;
        try
        {
            // Guards against more than a null _game: a headless/partially-constructed Game (as used
            // by this component's own unit tests) can throw from the GraphicsDevice getter itself
            // rather than returning null - either way, no device means bypass, never an exception.
            graphicsDevice = _game?.GraphicsDevice;
        }
        catch (Exception)
        {
            return null;
        }

        if (graphicsDevice == null)
        {
            return null;
        }

        _pixelTexture = new Texture2D(graphicsDevice, 1, 1, false, SurfaceFormat.Color);
        _pixelTexture.SetData(new[] { Color.White });
        return _pixelTexture;
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing && !_isDisposed)
            {
                _pixelTexture?.Dispose();
                _pixelTexture = null;
                _isDisposed = true;
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
}
