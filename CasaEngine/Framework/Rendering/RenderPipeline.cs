using CasaEngine.Framework.Game.Components;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Multi-view render pipeline. Iterates a list of <see cref="RenderView"/> instances
/// and for each enabled, visible, and due-to-render view:
/// <list type="number">
///   <item>Captures GPU state via <see cref="GraphicsStateGuard"/>.</item>
///   <item>Applies the surface (SetRenderTarget + Viewport).</item>
///   <item>Clears the surface.</item>
///   <item>Delegates to the view's <see cref="IViewRenderPipeline"/> (or <see cref="DefaultViewPipeline"/>).</item>
///   <item>Optionally calls the view's <see cref="IViewPresenter"/>.</item>
///   <item>Optionally draws <see cref="DebugOverlay"/> if requested.</item>
///   <item>Restores GPU state automatically when the guard is disposed.</item>
/// </list>
///
/// <b>UpdateMode support:</b>
/// <list type="bullet">
///   <item><see cref="ViewUpdateMode.RealTime"/>   — rendered every frame.</item>
///   <item><see cref="ViewUpdateMode.OnDemand"/>   — rendered only after <see cref="RenderView.Invalidate"/>.</item>
///   <item><see cref="ViewUpdateMode.Throttled"/>  — rendered at <see cref="RenderView.TargetFrameRate"/> fps.</item>
/// </list>
/// </summary>
public sealed class RenderPipeline
{
    private readonly GraphicsDevice                       _graphicsDevice;
    private readonly IReadOnlyList<IViewFlushableRenderer> _renderers;
    private readonly SpriteBatch                          _spriteBatch;
    private readonly Texture2D                            _pixel;
    private readonly DefaultViewPipeline                  _defaultPipeline = DefaultViewPipeline.Instance;

    /// <summary>
    /// Optional debug overlay drawn on views that have <see cref="RenderView.ShowDebugOverlay"/> = true.
    /// Assign from the game's Initialize method (requires a FontSystem).
    /// </summary>
    public DebugOverlay? DebugOverlay { get; set; }

#if DEBUG
    /// <summary>
    /// When true (debug builds only), logs a warning if a renderer leaves the
    /// GraphicsDevice in a non-default blend/depth/rasterizer state after a flush.
    /// </summary>
    public bool ValidateStatePerView { get; set; }
#endif

    /// <param name="graphicsDevice">The graphics device to draw on.</param>
    /// <param name="renderers">
    /// Ordered list of renderer components to flush per view
    /// (e.g. StaticMesh → SkinnedMesh → Sprite → Line3d).
    /// </param>
    /// <param name="spriteBatch">
    /// Used to fill backbuffer viewport rectangles with the clear color.
    /// MonoGame's <see cref="GraphicsDevice.Clear"/> ignores the current viewport
    /// and always clears the full render target; using SpriteBatch is the correct
    /// way to scope a color fill to a sub-rectangle of the backbuffer.
    /// </param>
    public RenderPipeline(
        GraphicsDevice                        graphicsDevice,
        IReadOnlyList<IViewFlushableRenderer> renderers,
        SpriteBatch                           spriteBatch)
    {
        _graphicsDevice = graphicsDevice;
        _renderers      = renderers;
        _spriteBatch    = spriteBatch;

        // 1×1 white pixel used to fill viewport areas with the clear color.
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    /// <summary>
    /// Renders all views that are enabled, visible, and due to render according
    /// to their <see cref="ViewUpdateMode"/>.
    ///
    /// RenderTarget views are processed before BackBuffer views so that transitions
    /// between RT and backbuffer surfaces are handled correctly.
    ///
    /// IMPORTANT — WPF editor compatibility:
    /// The D3D11Host sets a _cachedRenderTarget before calling Draw so that the
    /// scene is rendered into an off-screen texture shown in the WPF control.
    /// We must capture that initial render target and restore it whenever we need
    /// the "backbuffer" surface — not SetRenderTarget(null), which would lose the
    /// WPF texture. BackBufferSurface.Apply() therefore only touches the Viewport.
    /// </summary>
    /// <param name="views">The current list of views from <see cref="ViewManager"/>.</param>
    /// <param name="deltaSeconds">Elapsed time (seconds) since last frame, used for throttling.</param>
    public void Render(IReadOnlyList<RenderView> views, float deltaSeconds = 0f)
    {
        foreach (var world in views.Select(static view => view.World).Distinct())
        {
            world.DrawWorldUIToTextures();
        }

        // Capture the render target that is active when Render() is entered.
        // Standalone : null  (real backbuffer)
        // WPF editor : _cachedRenderTarget  (off-screen WPF texture)
        var initialTargets      = _graphicsDevice.GetRenderTargets();
        var initialRenderTarget = initialTargets.Length > 0
            ? initialTargets[0].RenderTarget as RenderTarget2D
            : null;

        // RenderTarget views first, then BackBuffer views.
        var orderedViews = views
            .Where(v => v.Enabled && IsViewPresented(v) && !v.Surface.IsBackBuffer)
            .Concat(views.Where(v => v.Enabled && IsViewPresented(v) && v.Surface.IsBackBuffer));

        foreach (var view in orderedViews)
        {
            // ---- UpdateMode check ----
            if (!ShouldRenderThisFrame(view, deltaSeconds))
            {
                continue;
            }

            ApplyResolutionScale(view);

            // ---- Capture full GPU state (restore on scope exit) ----
            using var guard = new GraphicsStateGuard(_graphicsDevice);

            // 1. Restore the initial target for BB views; RT views set their own.
            if (view.Surface.IsBackBuffer)
            {
                _graphicsDevice.SetRenderTarget(initialRenderTarget);
            }

            // 2. Apply surface (SetRenderTarget for RT, or Viewport-only for BB)
            view.Surface.Apply(_graphicsDevice);

            // 3. Clear
            //
            // IMPORTANT: GraphicsDevice.Clear() always clears the FULL render target,
            // ignoring the current viewport. For split-screen BackBufferSurface views
            // this would wipe every previously rendered view.
            //
            // Solution for BackBuffer color clear: draw a viewport-sized quad via
            // SpriteBatch (which respects the current viewport) instead of calling
            // Clear(Target). GraphicsDevice.Clear() is still used for depth/stencil
            // because depth precision artifacts from a leftover depth buffer are
            // acceptable and no quad-draw trick exists for depth.
            //
            // For RenderTarget surfaces a full Clear(Target) is safe (they are
            // independent textures, not shared with other views).
            if (view.ClearColorBuffer && view.Surface.IsBackBuffer)
            {
                var vp = view.Surface.ViewportRect;
                _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
                    null, DepthStencilState.None, RasterizerState.CullNone);
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, vp.Width, vp.Height), view.ClearColor);
                _spriteBatch.End();

                var depthClear = BuildDepthClearOptions(view);
                if (depthClear != 0)
                {
                    _graphicsDevice.Clear(depthClear, view.ClearColor, 1.0f, 0);
                }
            }
            else
            {
                var clearOptions = BuildDepthClearOptions(view);
                if (view.ClearColorBuffer) clearOptions |= ClearOptions.Target;
                if (clearOptions != 0)
                {
                    _graphicsDevice.Clear(clearOptions, view.ClearColor, 1.0f, 0);
                }
            }

            // 4. Build the camera frame for this view
            var frame = RenderFrameFactory.From(view.Camera, view.Surface.ViewportRect);

            // 5. Delegate to the per-view pipeline (or the shared default)
            var pipeline = view.Pipeline ?? _defaultPipeline;
            pipeline.RenderView(_graphicsDevice, view, in frame, _renderers);

            // 6. After an RT view, restore the initial render target so the next
            //    view (BB or another RT) starts from the expected surface.
            if (!view.Surface.IsBackBuffer)
            {
                _graphicsDevice.SetRenderTarget(initialRenderTarget);
            }

            // 7. Optional debug overlay (drawn into the view's surface)
            if (view.ShowDebugOverlay && DebugOverlay != null)
            {
                // Re-apply the surface so the overlay lands in the right target
                if (view.Surface.IsBackBuffer)
                {
                    _graphicsDevice.SetRenderTarget(initialRenderTarget);
                }
                view.Surface.Apply(_graphicsDevice);
                DebugOverlay.Draw(view, view.Surface.ViewportRect, deltaSeconds);

                if (!view.Surface.IsBackBuffer)
                {
                    _graphicsDevice.SetRenderTarget(initialRenderTarget);
                }
            }

            // 8. Optional presenter (blit RT → backbuffer, expose texture to UI, etc.)
            view.Presenter?.Present(_graphicsDevice, view);

            // Reset OnDemand dirty flag after successful render
            if (view.UpdateMode == ViewUpdateMode.OnDemand)
            {
                view.IsDirty = false;
            }

#if DEBUG
            if (ValidateStatePerView)
            {
                ValidateDeviceState(view);
            }
#endif
            // GraphicsStateGuard.Dispose() restores all state here (using block end).
        }

        // After all views: restore initial target + full-screen viewport.
        _graphicsDevice.SetRenderTarget(initialRenderTarget);
        var pp = _graphicsDevice.PresentationParameters;
        _graphicsDevice.Viewport = new Viewport(0, 0, pp.BackBufferWidth, pp.BackBufferHeight);
    }

    // ---- Throttle / update mode ----

    private static bool ShouldRenderThisFrame(RenderView view, float deltaSeconds)
    {
        switch (view.UpdateMode)
        {
            case ViewUpdateMode.RealTime:
                return true;

            case ViewUpdateMode.OnDemand:
                return view.IsDirty;

            case ViewUpdateMode.Throttled:
                view.ThrottleAccumulator += deltaSeconds;
                var interval = view.TargetFrameRate > 0f ? 1f / view.TargetFrameRate : 0f;
                if (view.ThrottleAccumulator >= interval)
                {
                    view.ThrottleAccumulator = 0f;
                    return true;
                }
                return false;

            default:
                return true;
        }
    }

    private static ClearOptions BuildDepthClearOptions(RenderView view)
    {
        if (!view.ClearDepthBuffer)
        {
            return 0;
        }

        return ClearOptions.DepthBuffer | ClearOptions.Stencil;
    }

    private static void ApplyResolutionScale(RenderView view)
    {
        if (view.Surface is not RenderTargetSurface renderTargetSurface)
        {
            return;
        }

        int baseWidth;
        int baseHeight;

        if (view.Host != null)
        {
            baseWidth = Math.Max(1, view.Host.Width);
            baseHeight = Math.Max(1, view.Host.Height);
        }
        else
        {
            var viewport = renderTargetSurface.ViewportRect;
            baseWidth = Math.Max(1, viewport.Width);
            baseHeight = Math.Max(1, viewport.Height);
        }

        var scaledWidth = Math.Max(1, (int)Math.Round(baseWidth * view.ResolutionScale));
        var scaledHeight = Math.Max(1, (int)Math.Round(baseHeight * view.ResolutionScale));
        renderTargetSurface.EnsureSize(scaledWidth, scaledHeight);
    }

    private static bool IsViewPresented(RenderView view)
    {
        return view.Host?.IsVisible ?? view.IsVisible;
    }

#if DEBUG
    private void ValidateDeviceState(RenderView view)
    {
        // Log warnings if the pipeline left the device in a unexpected state.
        // (Blend/Depth/Rasterizer should be back to defaults after the guard restores them,
        //  but this check runs BEFORE the guard disposes, to catch renderer bugs.)
        if (_graphicsDevice.BlendState != BlendState.Opaque &&
            _graphicsDevice.BlendState != BlendState.AlphaBlend &&
            _graphicsDevice.BlendState != BlendState.NonPremultiplied)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[RenderPipeline] View '{view.Name}': BlendState left dirty after flush.");
        }
    }
#endif
}
