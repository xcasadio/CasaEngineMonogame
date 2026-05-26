using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;

namespace CasaEngine.Framework.UI;

/// <summary>
/// Per-view implementation of <see cref="IRenderHost"/> that clips MGUI bounds
/// and mouse input to a specific <see cref="CasaEngine.Framework.Rendering.RenderView"/>'s
/// viewport rectangle.
///
/// Returns 0-based bounds (width × height, no X/Y offset) so that MGUI lays out
/// UI elements in viewport-local space where (0,0) is the top-left of the view.
/// Mouse coordinates are translated from screen-space to viewport-local space by
/// subtracting the viewport's screen-space origin.
/// </summary>
internal sealed class ViewRenderHost : IRenderHost, IRawInputSource
{
    private readonly CasaEngineGame _game;
    private readonly IRenderSurface      _surface;
    private readonly IWindowInputSource _windowInputSource;

    public ViewRenderHost(CasaEngineGame game, IRenderSurface surface, IWindowInputSource windowInputSource = null)
    {
        _game    = game;
        _surface = surface;
        _windowInputSource = windowInputSource ?? game.RuntimeContext.WindowInputSource ?? new MonoGameWindowInputSource(
            () => game.IsActive,
            () => game.Window);

        // Forward lifecycle events from the host game so the desktop runtime
        // can refresh its input cache and other per-frame state.
        _game.PreviewUpdate += (s, e) => PreviewUpdate?.Invoke(s, e);
        _game.EndUpdate     += (s, e) => EndUpdate?.Invoke(s, e);
    }

    // ---- IRenderViewport ----

    /// <summary>
    /// Returns 0-based viewport bounds (0, 0, width, height) so that MGUI
    /// treats the view as its own full-screen area.
    /// </summary>
    public Rectangle GetBounds()
    {
        var vp = _surface.ViewportRect;
        return new Rectangle(0, 0, vp.Width, vp.Height);
    }

    // ---- IRenderHost ----

    public GraphicsDevice GraphicsDevice => _game.GraphicsDevice;

    /// <summary>
    /// Returns mouse state with the position translated to viewport-local
    /// coordinates by subtracting the viewport's screen-space origin.
    /// </summary>
    public MouseState GetMouseState()
    {
        var state = _windowInputSource.GetSnapshot().MouseState;
        var vp = _surface.ViewportRect;

        if (_game.GameManager.ViewManager.Views.FirstOrDefault(v => ReferenceEquals(v.Surface, _surface)) is { Host: IViewScreenBoundsHost screenBoundsHost })
        {
            vp = screenBoundsHost.ScreenBounds;
        }

        return new MouseState(
            state.X - vp.X,
            state.Y - vp.Y,
            state.ScrollWheelValue,
            state.LeftButton,
            state.MiddleButton,
            state.RightButton,
            state.XButton1,
            state.XButton2,
            state.HorizontalScrollWheelValue);
    }

    public KeyboardState GetKeyboardState() => _windowInputSource.GetSnapshot().KeyboardState;

    public object GetService(Type serviceType) => _game.Services.GetService(serviceType);

    // ---- IObservableUpdate ----

    public event EventHandler<TimeSpan>  PreviewUpdate;
    public event EventHandler<EventArgs> EndUpdate;
}
