using CasaEngine.Framework.Configuration.Project;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Shows a view rendered into a target of its virtual size (ADR-0087). The whole back buffer is cleared to black, then
/// the target is drawn into <see cref="PresentedRect"/>: at an integer factor with point sampling
/// (<see cref="VirtualResolutionMode.BufferedIntegerFit"/>), or at a free factor with linear sampling
/// (<see cref="VirtualResolutionMode.BufferedFit"/>).
/// <para>
/// The presenter owns the surface it is given and the sprite batch it draws with: <see cref="Dispose"/> releases both.
/// The default view creates one and releases it when the view is removed. Presenting allocates nothing: the rectangle
/// changes only in <see cref="Resize"/>, and the sprite batch is created on the first frame.
/// </para>
/// </summary>
public sealed class VirtualResolutionPresenter : IViewPresenter, IDisposable
{
    private readonly IRenderSurface _ownedSurface;
    private SpriteBatch _spriteBatch;

    /// <param name="ownedSurface">The target surface the view renders into; released by <see cref="Dispose"/>. May be null in tests.</param>
    /// <param name="mode">A buffered mode.</param>
    /// <param name="virtualWidth">Width of the target, in virtual pixels.</param>
    /// <param name="virtualHeight">Height of the target, in virtual pixels.</param>
    /// <param name="windowWidth">Current window width.</param>
    /// <param name="windowHeight">Current window height.</param>
    public VirtualResolutionPresenter(
        IRenderSurface ownedSurface, VirtualResolutionMode mode, int virtualWidth, int virtualHeight, int windowWidth, int windowHeight)
    {
        if (!mode.IsBuffered())
        {
            throw new ArgumentException($"'{mode}' is not a buffered virtual resolution mode.", nameof(mode));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(virtualWidth, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(virtualHeight, 0);

        _ownedSurface = ownedSurface;
        Mode = mode;
        VirtualWidth = virtualWidth;
        VirtualHeight = virtualHeight;
        Resize(windowWidth, windowHeight);
    }

    public VirtualResolutionMode Mode { get; }

    public int VirtualWidth { get; }

    public int VirtualHeight { get; }

    /// <summary>Where the target is drawn, in window pixels, not cropped by the window.</summary>
    public Rectangle PresentedRect { get; private set; }

    public bool IsDisposed { get; private set; }

    /// <summary>Recomputes <see cref="PresentedRect"/> for a new window size.</summary>
    public void Resize(int windowWidth, int windowHeight)
        => PresentedRect = VirtualResolutionLayout.ComputePresentation(windowWidth, windowHeight, VirtualWidth, VirtualHeight, Mode);

    /// <summary>
    /// The virtual position of a window position: <c>(window - origin) * virtual size / presented size</c>, exact. The
    /// single conversion the view manager, the input router and the UI host use for a buffered view (ADR-0087).
    /// </summary>
    public Vector2 WindowToVirtual(Vector2 windowPosition)
    {
        var rect = PresentedRect;
        return new Vector2(
            (windowPosition.X - rect.X) * VirtualWidth / rect.Width,
            (windowPosition.Y - rect.Y) * VirtualHeight / rect.Height);
    }

    /// <summary>The virtual pixel under a window pixel, rounded down (a point left of or above the image is negative).</summary>
    public Point WindowToVirtualPixel(Point windowPoint)
    {
        var position = WindowToVirtual(new Vector2(windowPoint.X, windowPoint.Y));
        return new Point((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y));
    }

    /// <inheritdoc/>
    public void Present(GraphicsDevice graphicsDevice, RenderView view)
    {
        var texture = view.Surface?.RenderTarget;
        if (IsDisposed || texture == null)
        {
            return;
        }

        _spriteBatch ??= new SpriteBatch(graphicsDevice);

        // Every state changed here is put back: the pipeline expects the state it left.
        var previousViewport = graphicsDevice.Viewport;
        var previousBlend = graphicsDevice.BlendState;
        var previousDepthStencil = graphicsDevice.DepthStencilState;
        var previousRasterizer = graphicsDevice.RasterizerState;
        var previousSampler = graphicsDevice.SamplerStates[0];

        // GraphicsDevice.Clear is not guaranteed to ignore the viewport: clear the whole back buffer explicitly.
        var presentation = graphicsDevice.PresentationParameters;
        graphicsDevice.Viewport = new Viewport(0, 0, presentation.BackBufferWidth, presentation.BackBufferHeight);
        graphicsDevice.Clear(Color.Black);

        var sampler = Mode == VirtualResolutionMode.BufferedIntegerFit ? SamplerState.PointClamp : SamplerState.LinearClamp;
        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, sampler, DepthStencilState.None, RasterizerState.CullNone);
        _spriteBatch.Draw(texture, PresentedRect, Color.White);
        _spriteBatch.End();

        graphicsDevice.Viewport = previousViewport;
        graphicsDevice.BlendState = previousBlend;
        graphicsDevice.DepthStencilState = previousDepthStencil;
        graphicsDevice.RasterizerState = previousRasterizer;
        graphicsDevice.SamplerStates[0] = previousSampler;
    }

    /// <summary>Releases the owned surface and the sprite batch. Safe to call twice.</summary>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        _spriteBatch?.Dispose();
        _spriteBatch = null;
        (_ownedSurface as IDisposable)?.Dispose();
    }
}
