using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.FrozenFrames;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Application.Components;

/// <summary>
/// Holds the <see cref="FrozenFrameService"/> and draws the frozen image as a post-UI overlay
/// (E19.TR-4a, D-TR4A-5, D-TR4A-6).
/// </summary>
/// <remarks>
/// Registers on the active view while <see cref="FrozenFrameService.ShouldRegisterBase"/> holds, at
/// <see cref="PostUIOverlayOrder.FrozenFrame"/> (below the screen effect veil), and draws only while
/// <see cref="FrozenFrameService.IsBaseDrawable"/>. It also keeps the GPU side (<see cref="Retention"/>)
/// pointed at the active view and in step with <see cref="FrozenFrameService.RetainLastFrame"/>.
/// The game of a project turns nothing on by itself: the option is false by default.
/// </remarks>
public class FrozenFrameComponent : GameComponent, IPostUIOverlay
{
    private readonly CasaEngineGame _game;
    private RenderView _registeredOverlayView;
    private bool _isDisposed;

    public FrozenFrameComponent(Game game) : base(game)
    {
        ArgumentNullException.ThrowIfNull(game);

        _game = game as CasaEngineGame;
        Service = new FrozenFrameService();
        Retention = new FrameRetention(Service);

        UpdateOrder = (int)ComponentUpdateOrder.FrozenFrames;
        game.Components.Add(this);
    }

    /// <summary>The state the game DLL pushes to.</summary>
    public FrozenFrameService Service { get; }

    internal FrameRetention Retention { get; }

    public override void Update(GameTime gameTime)
    {
        var activeView = _game?.GameManager?.ViewManager?.ActiveView;

        Retention.TargetView = activeView;
        Retention.Enabled = Service.RetainLastFrame;
        Retention.ReleaseCapturedWhenUnused();

        UpdateRegistration(activeView);

        base.Update(gameTime);
    }

    private void UpdateRegistration(RenderView activeView)
    {
        var shouldBeRegistered = Service.ShouldRegisterBase;

        if (_registeredOverlayView != null && (!shouldBeRegistered || _registeredOverlayView != activeView))
        {
            _registeredOverlayView.UnregisterPostUIOverlay(this);
            _registeredOverlayView = null;
        }

        if (shouldBeRegistered && _registeredOverlayView == null && activeView != null)
        {
            activeView.RegisterPostUIOverlay(this, PostUIOverlayOrder.FrozenFrame);
            _registeredOverlayView = activeView;
        }
    }

    /// <summary>
    /// Draws the captured image over the whole view: opaque, point sampling, no depth, through the
    /// <c>SpriteBatch</c> of the game (D-TR4A-5). An image captured at another size is stretched to the view.
    /// </summary>
    public void Draw(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame)
    {
        if (!TryGetBaseDestination(Service.IsBaseDrawable, view.Surface.ViewportRect, out var destination))
        {
            return;
        }

        var texture = Retention.CapturedTexture;
        var spriteBatch = _game?.SpriteBatch;
        if (texture == null || spriteBatch == null)
        {
            return;
        }

        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
            SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        spriteBatch.Draw(texture, destination, Color.White);
        spriteBatch.End();
    }

    /// <summary>
    /// Pure seam: the destination rectangle of the base image over a view, or false when nothing is drawn
    /// (<paramref name="isBaseDrawable"/> false, or an empty view). The viewport is already the one of the view:
    /// the destination starts at the origin, as the copy of <c>RenderPipeline</c> does.
    /// </summary>
    internal static bool TryGetBaseDestination(bool isBaseDrawable, Rectangle viewportRect, out Rectangle destination)
    {
        if (!isBaseDrawable || viewportRect.Width <= 0 || viewportRect.Height <= 0)
        {
            destination = Rectangle.Empty;
            return false;
        }

        destination = new Rectangle(0, 0, viewportRect.Width, viewportRect.Height);
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing && !_isDisposed)
            {
                _registeredOverlayView?.UnregisterPostUIOverlay(this);
                _registeredOverlayView = null;
                Retention.Dispose();
                _isDisposed = true;
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
}
