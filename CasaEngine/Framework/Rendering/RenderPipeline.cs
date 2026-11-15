using System.Diagnostics;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.FrozenFrames;
using CasaEngine.Framework.Rendering.PostProcess;
using CasaEngine.Framework.Rendering.Shaders;
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
public sealed class RenderPipeline : IDisposable
{
    private readonly GraphicsDevice                       _graphicsDevice;
    private readonly IReadOnlyList<IViewFlushableRenderer> _renderers;
    private readonly SpriteBatch                          _spriteBatch;
    private readonly Texture2D                            _pixel;
    private readonly DefaultViewPipeline                  _defaultPipeline = DefaultViewPipeline.Instance;
    // Scene render target used when shadows are active on a back-buffer view.
    // Prevents D3D11 back-buffer discards caused by shadow-pass RT switches.
    private RenderTarget2D _sceneRt;

    // Tonemapping shader of the linear HDR colour pipeline (ADR-0101). Loaded by the game once its content root is set;
    // null until then (the editor, tests), which only matters for a view in LinearHdr.
    private ShaderWrapper _tonemapShader;

    // Bloom shader (ADR-0104), loaded with the tonemap shader; null until then, which only matters for a view with bloom.
    private ShaderWrapper _bloomShader;

    // Generator of the IBL cubes (ADR-0106), created when the game gives the pipeline the IblConvolution effect; null until then
    // (the editor, tests), in which case nothing is generated and the PBR ambient stays flat.
    private IblCubeGenerator _iblCubeGenerator;

    // Cache of the generated IBL textures and of the BRDF table (ADR-0106). Its requests run at the start of Render, before any view.
    private readonly IblCache _iblCache;

    private const string ReinhardTechniqueName = "Tonemap_Reinhard";
    private const string AcesApproxTechniqueName = "Tonemap_AcesApprox";

    // Format of the HDR scene target of the linear HDR colour pipeline; falls back once, logged, when the device refuses it.
    private SurfaceFormat _hdrFormat = SurfaceFormat.HalfVector4;
    private bool _tonemapMissingLogged;
    private int _renderIndex;
    private readonly HashSet<Type> _unadaptedPipelineWarnings = new();

    /// <summary>
    /// Number of HDR scene targets created since the start (a target is created once per view size and format, then
    /// reused from <see cref="RenderTargetPool"/>).
    /// </summary>
    internal int HdrTargetCreationCount { get; private set; }

    /// <summary>Number of HDR scene targets disposed because their size was no longer in use (see <see cref="HdrTargetEviction"/>).</summary>
    internal int HdrTargetDisposalCount { get; private set; }

    /// <summary>Number of bloom targets created since the start (see <see cref="BloomRenderer"/>).</summary>
    internal int BloomTargetCreationCount => _bloomRenderer.TargetCreationCount;

    /// <summary>Number of bloom targets disposed because their size was no longer in use (see <see cref="HdrTargetEviction"/>).</summary>
    internal int BloomTargetDisposalCount { get; private set; }

    // Render index of the last use of each tracked target key (width, height, format, depth format): the HDR scene targets
    // (with depth) and the bloom targets (without). Filled when a key is first seen, so no allocation at constant size; swept
    // once per Render.
    private readonly Dictionary<(int, int, SurfaceFormat, DepthFormat), long> _hdrLastUsedRender = new();
    private readonly List<(int, int, SurfaceFormat, DepthFormat)> _hdrStaleKeys = new();
    private readonly List<((int, int, SurfaceFormat, DepthFormat) Key, int Disposed)> _releasedTargets = new();

    private readonly BloomRenderer _bloomRenderer;
    private bool _bloomShaderMissingLogged;

    /// <summary>
    /// The frozen frame's GPU side (E19.TR-4a): keeps the last frame of the target view and copies it on request.
    /// Null (the editor, tests): the pipeline is exactly the one without it.
    /// </summary>
    internal FrameRetention Retention { get; set; }

    /// <summary>
    /// Optional debug overlay drawn on views that have <see cref="RenderView.ShowDebugOverlay"/> = true.
    /// Assign from the game's Initialize method (requires a FontSystem).
    /// </summary>
    public DebugOverlay DebugOverlay { get; set; }

    /// <summary>
    /// Optional callback executed just before a view pipeline renders a view.
    /// Useful for view-scoped debug systems that must enqueue primitives into the
    /// same renderer flush as the owning view.
    /// </summary>
    public Action<RenderView> BeforeViewRender { get; set; }

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
        _bloomRenderer  = new BloomRenderer(graphicsDevice, spriteBatch);
        _iblCache       = new IblCache(graphicsDevice);

        // 1×1 white pixel used to fill viewport areas with the clear color.
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
    }

    /// <summary>
    /// Gives the pipeline the compiled <c>Tonemap</c> effect (<see cref="BuiltInShaderCatalog.TonemapContentName"/>) used
    /// to bring the HDR scene target to the display range. Called once by the game after its content root is set.
    /// </summary>
    public void SetTonemapEffect(Effect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        if (_tonemapShader == null)
        {
            _tonemapShader = new ShaderWrapper(effect);
        }
        else
        {
            _tonemapShader.ReplaceEffect(effect);
        }
    }

    /// <summary>
    /// Gives the pipeline the compiled <c>Bloom</c> effect (<see cref="BuiltInShaderCatalog.BloomContentName"/>) used by the
    /// bloom of a view that asks for it (ADR-0104). Called once by the game after its content root is set.
    /// </summary>
    public void SetBloomEffect(Effect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        if (_bloomShader == null)
        {
            _bloomShader = new ShaderWrapper(effect);
        }
        else
        {
            _bloomShader.ReplaceEffect(effect);
        }
    }

    /// <summary>
    /// Gives the pipeline the compiled <c>IblConvolution</c> effect (<see cref="BuiltInShaderCatalog.IblConvolutionContentName"/>)
    /// and builds the generator of the IBL cubes from it (ADR-0106). Called once by the game after its content root is set; a
    /// hot reload replaces the generator. Without it <see cref="IblCubeGenerator"/> stays null and nothing is generated.
    /// </summary>
    public void SetIblConvolutionEffect(Effect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        _iblCubeGenerator?.Dispose();
        _iblCubeGenerator = new IblCubeGenerator(_graphicsDevice, effect);
    }

    /// <summary>The generator of the IBL cubes, or null until <see cref="SetIblConvolutionEffect"/> has been called.</summary>
    public IblCubeGenerator IblCubeGenerator => _iblCubeGenerator;

    /// <summary>The cache of the IBL textures; the binders request through it and <see cref="Render"/> executes the requests (ADR-0106).</summary>
    public IblCache IblCache => _iblCache;

    /// <summary>Releases the IBL textures, the BRDF table and the generator.</summary>
    public void Dispose()
    {
        _iblCache.Dispose();
        _iblCubeGenerator?.Dispose();
    }

    /// <summary>
    /// Hot reload of the tonemapping, bloom and IBL convolution shaders: takes <paramref name="effect"/> when <paramref name="contentName"/>
    /// names one of them.
    /// </summary>
    public bool TryReloadBuiltInShader(string contentName, Effect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        string normalizedName = BuiltInShaderCatalog.NormalizeContentName(contentName);
        if (string.Equals(normalizedName, BuiltInShaderCatalog.TonemapContentName, StringComparison.OrdinalIgnoreCase))
        {
            SetTonemapEffect(effect);
            return true;
        }

        if (string.Equals(normalizedName, BuiltInShaderCatalog.BloomContentName, StringComparison.OrdinalIgnoreCase))
        {
            SetBloomEffect(effect);
            return true;
        }

        if (string.Equals(normalizedName, BuiltInShaderCatalog.IblConvolutionContentName, StringComparison.OrdinalIgnoreCase))
        {
            SetIblConvolutionEffect(effect);
            return true;
        }

        return false;
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
        _renderIndex++;

        // Image-based lighting requests registered by the binders during the previous frame run here, before any view and
        // with no view target bound (decision P9): the generator restores the device state it touches.
        _iblCache.ViewLoopActive = false;
        _iblCache.Flush(_iblCubeGenerator);

        foreach (var world in views.Select(static view => view.World).Distinct())
        {
            world.DrawWorldUIToTextures();
            world.DrawTileMapSurfacesToTextures();
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

        _iblCache.ViewLoopActive = true;
        foreach (var view in orderedViews)
        {
            if (!ShouldRenderThisFrame(view, deltaSeconds))
            {
                continue;
            }

            ApplyResolutionScale(view);
            PixelPerfectDiagnostics.WarnOnce(view);

            // Shadow settings are needed before surface setup to decide whether to redirect
            // scene rendering through an intermediate RenderTarget2D (sceneRt).
            // On D3D11 standalone builds, switching away from the swap-chain back buffer and
            // back (as the shadow pass does) discards its contents. Rendering to a
            // PreserveContents RenderTarget2D and blitting once at the end avoids this.
            // WPF editor views already use a non-null initialRenderTarget (the D3D11Host
            // texture), so that path is unaffected.
            var shadowSettings = (view.EnvironmentOverride ?? view.World.EnvironmentSettings).Shadows;
            var retention = Retention;
            var viewRectangle = view.Surface.ViewportRect;
            var plan = FrameCapturePlanner.Plan(new FrameCaptureInputs(
                ShadowsEnabled: shadowSettings.Enabled,
                ClearColorBuffer: view.ClearColorBuffer,
                InitialTargetIsBackBuffer: initialRenderTarget == null,
                SurfaceIsBackBuffer: view.Surface.IsBackBuffer,
                ViewRectangleIsEmpty: viewRectangle.Width <= 0 || viewRectangle.Height <= 0,
                RetentionEnabled: retention is { Enabled: true },
                IsTargetView: retention != null && ReferenceEquals(view, retention.TargetView),
                CaptureRequested: retention != null && retention.Service.CaptureRequested,
                RetainedFrameValid: retention != null && retention.IsRetainedFrameValid(viewRectangle.Width, viewRectangle.Height)));
            bool useSceneRt = plan.UseSceneRt;

            // The colour pipeline is read ahead, like the shadow settings, because it decides how the view's target is
            // bound. Legacy (the default) never takes the HDR path: every line below behaves as before it existed.
            var colorPlan = ColorPipelinePlanner.Plan(new ColorPipelineInputs(
                (view.EnvironmentOverride ?? view.World.EnvironmentSettings).ColorPipeline.Mode,
                view.ClearColorBuffer,
                viewRectangle.Width <= 0 || viewRectangle.Height <= 0,
                view.Surface.IsBackBuffer,
                plan));
            bool useHdr = colorPlan.UseHdrSceneTarget && HasTonemapShader();
            RenderTarget2D hdrTarget = null;

            using var guard = new GraphicsStateGuard(_graphicsDevice);

            // E19.TR-4a: the retained frame still holds the previous frame here, before the scene target is
            // bound and cleared; copy it to the captured image, or report the request unavailable (once).
            if (plan.CopyRetainedFrameToCapture)
            {
                retention.CopyRetainedToCaptured(_graphicsDevice, _spriteBatch, viewRectangle.Width, viewRectangle.Height);
            }
            else if (plan.CaptureUnavailable)
            {
                retention.Service.FailCapture();
            }

            if (useHdr)
            {
                // The scene goes into a pooled HDR target; the destination of the view is bound after the scene.
                hdrTarget = AcquireHdrTarget(viewRectangle.Width, viewRectangle.Height);
                _graphicsDevice.SetRenderTarget(hdrTarget);
                _graphicsDevice.Viewport = new Viewport(0, 0, viewRectangle.Width, viewRectangle.Height);
            }
            else if (useSceneRt)
            {
                var vp = view.Surface.ViewportRect;
                var sceneTarget = plan.RetainThisFrame
                    ? retention.EnsureRetained(_graphicsDevice, vp.Width, vp.Height)
                    : EnsureSceneRt(vp.Width, vp.Height);
                _graphicsDevice.SetRenderTarget(sceneTarget);
                _graphicsDevice.Viewport = new Viewport(0, 0, vp.Width, vp.Height);
            }
            else
            {
                // 1. Restore the initial target for BB views; RT views set their own.
                if (view.Surface.IsBackBuffer)
                {
                    _graphicsDevice.SetRenderTarget(initialRenderTarget);
                }

                // 2. Apply surface (SetRenderTarget for RT, or Viewport-only for BB)
                view.Surface.Apply(_graphicsDevice);
            }

            var resolvedEnvironment = EnvironmentResolver.Resolve(view);
            if (useHdr)
            {
                // The resolved value is cached per view and says only what the world asks for. The view really draws through the
                // HDR target in this frame only when the planner said so: that is what lets the binders decode display colours
                // (ADR-0102). Everything built from this copy (lights, frame, binders) sees the same decision.
                resolvedEnvironment = resolvedEnvironment with { LinearColorActive = true };
            }

            WorldLightCollector.Collect(view.World, view.Lighting, in resolvedEnvironment, view.Camera.Position);
            var resolvedClearColor = resolvedEnvironment.BackgroundMode == EnvironmentBackgroundMode.SolidColor
                ? resolvedEnvironment.BackgroundColor
                : view.ClearColor;

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
            long clearStartTimestamp = Stopwatch.GetTimestamp();

            if (useHdr)
            {
                // The clear colour is a display colour: it is decoded to linear light in floating point (an 8-bit Color would
                // quantise dark backgrounds), because the HDR target is tonemapped and re-encoded afterwards.
                _graphicsDevice.Clear(
                    ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil,
                    ColorSpace.Decode(resolvedClearColor), 1.0f, 0);
            }
            else if (useSceneRt)
            {
                // sceneRt is a dedicated surface; Device.Clear is safe (no viewport restriction issue).
                _graphicsDevice.Clear(
                    ClearOptions.Target | ClearOptions.DepthBuffer,
                    resolvedClearColor, 1.0f, 0);
            }
            else if (view.ClearColorBuffer && view.Surface.IsBackBuffer)
            {
                var vp = view.Surface.ViewportRect;
                _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
                    null, DepthStencilState.None, RasterizerState.CullNone);
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, vp.Width, vp.Height), resolvedClearColor);
                _spriteBatch.End();

                var depthClear = BuildDepthClearOptions(view);
                if (depthClear != 0)
                {
                    _graphicsDevice.Clear(depthClear, resolvedClearColor, 1.0f, 0);
                }
            }
            else
            {
                var clearOptions = BuildDepthClearOptions(view);
                if (view.ClearColorBuffer)
                {
                    clearOptions |= ClearOptions.Target;
                }

                if (clearOptions != 0)
                {
                    _graphicsDevice.Clear(clearOptions, resolvedClearColor, 1.0f, 0);
                }
            }

            view.RenderStats.ClearCpuMilliseconds = GetElapsedMilliseconds(clearStartTimestamp);

            // 4. Build the camera frame for this view
            var frame = RenderFrameFactory.From(view.Camera, view.Surface.ViewportRect, in resolvedEnvironment, view.Lighting, shadowSettings) with { Ibl = _iblCache };

            // Reset per-view counters before renderer flushes aggregate into them.
            view.RenderStats.Reset();

            long viewStartTimestamp = Stopwatch.GetTimestamp();

            long beforeViewStartTimestamp = Stopwatch.GetTimestamp();
            BeforeViewRender?.Invoke(view);
            view.RenderStats.BeforeViewCpuMilliseconds = GetElapsedMilliseconds(beforeViewStartTimestamp);

            // 5. Delegate to the per-view pipeline (or the shared default)
            var pipeline = view.Pipeline ?? _defaultPipeline;
            long pipelineStartTimestamp = Stopwatch.GetTimestamp();
            if (useHdr)
            {
                RenderHdrView(view, pipeline, in frame, in colorPlan, in plan, hdrTarget, initialRenderTarget);
            }
            else
            {
                pipeline.RenderView(_graphicsDevice, view, in frame, _renderers);
            }
            view.RenderStats.RenderedThisFrame = true;
            view.RenderStats.TotalCpuMilliseconds += GetElapsedMilliseconds(pipelineStartTimestamp);

            if (useSceneRt)
            {
                // Blit sceneRt to the swap-chain back buffer. This is the single write to the
                // DXGI surface; all shadow-pass RT switches happened between non-back-buffer
                // surfaces so no content was discarded.
                if (useHdr)
                {
                    BindInitialTargetKeepingBackBuffer(initialRenderTarget);
                }
                else
                {
                    _graphicsDevice.SetRenderTarget(initialRenderTarget);
                }
                view.Surface.Apply(_graphicsDevice);
                var vp = view.Surface.ViewportRect;
                _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
                    null, DepthStencilState.None, RasterizerState.CullNone);
                _spriteBatch.Draw(plan.RetainThisFrame ? retention.RetainedTarget : _sceneRt,
                    new Rectangle(0, 0, vp.Width, vp.Height), Color.White);
                _spriteBatch.End();
            }

            // The retained frame is complete only now, after the copy to the back buffer. A target view drawn
            // without retention (option off, ineligible) leaves the retained frame stale.
            if (retention != null && ReferenceEquals(view, retention.TargetView))
            {
                if (plan.RetainThisFrame)
                {
                    retention.MarkRetainedValid();
                }
                else
                {
                    retention.InvalidateRetained();
                }
            }

            // 6. After an RT view, restore the initial render target so the next
            //    view (BB or another RT) starts from the expected surface.
            if (!view.Surface.IsBackBuffer)
            {
                _graphicsDevice.SetRenderTarget(initialRenderTarget);
            }

            // 7. Optional debug overlay (drawn into the view's surface)
            if (view.ShowDebugOverlay && DebugOverlay != null)
            {
                long overlayStartTimestamp = Stopwatch.GetTimestamp();

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

                view.RenderStats.OverlayCpuMilliseconds += GetElapsedMilliseconds(overlayStartTimestamp);
            }

            // 8. Optional presenter (blit RT → backbuffer, expose texture to UI, etc.)
            long presenterStartTimestamp = Stopwatch.GetTimestamp();
            view.Presenter?.Present(_graphicsDevice, view);
            view.RenderStats.PresenterCpuMilliseconds += GetElapsedMilliseconds(presenterStartTimestamp);
            view.RenderStats.TotalCpuMilliseconds = GetElapsedMilliseconds(viewStartTimestamp);

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

        _iblCache.ViewLoopActive = false;
        ReleaseUnusedHdrTargets();

        // After all views: restore initial target + full-screen viewport.
        _graphicsDevice.SetRenderTarget(initialRenderTarget);
        var pp = _graphicsDevice.PresentationParameters;
        _graphicsDevice.Viewport = new Viewport(0, 0, pp.BackBufferWidth, pp.BackBufferHeight);
    }

    // ---- Linear HDR colour pipeline (ADR-0101) ----

    private bool HasTonemapShader()
    {
        if (_tonemapShader != null)
        {
            return true;
        }

        if (!_tonemapMissingLogged)
        {
            _tonemapMissingLogged = true;
            Logs.WriteWarning("[ColorPipeline] The tonemap shader is not loaded: views in LinearHdr are drawn by the legacy path.");
        }

        return false;
    }

    /// <summary>
    /// Disposes the free HDR scene targets of the sizes no view has rendered with for <see cref="HdrTargetEviction.MaxIdleRenders"/>
    /// renders, so GPU memory follows the sizes in use (a window resize leaves no target behind). Once per Render, after the
    /// views have released their targets; only the HDR keys tracked here are touched, never the other users of the shared pool.
    /// </summary>
    private void ReleaseUnusedHdrTargets()
    {
        if (_hdrLastUsedRender.Count == 0)
        {
            return;
        }

        _releasedTargets.Clear();
        HdrTargetEviction.Sweep(_hdrLastUsedRender, _renderIndex, _hdrStaleKeys, _releasedTargets, RenderTargetPool.Shared);

        for (int i = 0; i < _releasedTargets.Count; i++)
        {
            var (key, disposed) = _releasedTargets[i];
            if (key.Item4 == DepthFormat.None)
            {
                BloomTargetDisposalCount += disposed;
                Logs.WriteDebug($"[Bloom] target released: {key.Item1}x{key.Item2} {key.Item3}, render {_renderIndex}, disposed {BloomTargetDisposalCount}, alive {BloomTargetCreationCount - BloomTargetDisposalCount}");
            }
            else
            {
                HdrTargetDisposalCount += disposed;
                Logs.WriteDebug($"[ColorPipeline] HDR scene target released: {key.Item1}x{key.Item2} {key.Item3}, render {_renderIndex}, disposed {HdrTargetDisposalCount}, alive {HdrTargetCreationCount - HdrTargetDisposalCount}");
            }
        }
    }

    /// <summary>
    /// Takes the HDR scene target of a view from <see cref="RenderTargetPool"/>. The key of the pool holds the format, so a
    /// target is created once per view size; creation is logged. A format the device refuses falls back to the next one
    /// (HalfVector4, HdrBlendable, Color), logged once.
    /// </summary>
    private RenderTarget2D AcquireHdrTarget(int width, int height)
    {
        var pool = RenderTargetPool.Shared;
        int countBefore = pool.TotalCount;

        RenderTarget2D target;
        while (true)
        {
            try
            {
                target = pool.Acquire(width, height, _hdrFormat, DepthFormat.Depth24Stencil8);
                break;
            }
            catch (Exception exception) when (_hdrFormat != SurfaceFormat.Color)
            {
                var fallback = _hdrFormat == SurfaceFormat.HalfVector4 ? SurfaceFormat.HdrBlendable : SurfaceFormat.Color;
                Logs.WriteWarning($"[ColorPipeline] HDR scene target format {_hdrFormat} refused ({exception.Message}); falling back to {fallback}.");
                _hdrFormat = fallback;
            }
        }

        _hdrLastUsedRender[(width, height, target.Format, DepthFormat.Depth24Stencil8)] = _renderIndex;

        if (pool.TotalCount != countBefore)
        {
            HdrTargetCreationCount++;
            Logs.WriteDebug($"[ColorPipeline] HDR scene target created: {width}x{height} {target.Format}, render {_renderIndex}, total {HdrTargetCreationCount}");
        }

        return target;
    }

    /// <summary>
    /// Renders the scene of a view into <paramref name="hdrTarget"/> (already bound and cleared), then tonemaps it into the
    /// destination the legacy path would have drawn to, then composes the interface on top of the tonemapped image.
    /// </summary>
    private void RenderHdrView(
        RenderView view,
        IViewRenderPipeline pipeline,
        in RenderFrame frame,
        in ColorPipelinePlan colorPlan,
        in FrameCapturePlan capturePlan,
        RenderTarget2D hdrTarget,
        RenderTarget2D initialRenderTarget)
    {
        try
        {
            bool composeInterfaceAfter = false;
            if (pipeline is IScenePhaseViewPipeline scenePipeline && scenePipeline.SupportsScenePhase)
            {
                scenePipeline.RenderScene(_graphicsDevice, view, in frame, _renderers);
                composeInterfaceAfter = true;
            }
            else
            {
                if (_unadaptedPipelineWarnings.Add(pipeline.GetType()))
                {
                    Logs.WriteWarning(
                        $"[ColorPipeline] View pipeline {pipeline.GetType().Name} does not implement IScenePhaseViewPipeline: its interface is rendered into the HDR target and tonemapped with the scene.");
                }

                pipeline.RenderView(_graphicsDevice, view, in frame, _renderers);
            }

            if (frame.Environment.BloomEnabled)
            {
                ApplyBloom(hdrTarget, in frame);
            }

            var viewRectangle = view.Surface.ViewportRect;
            BindHdrDestination(view, colorPlan.Destination, in capturePlan, initialRenderTarget, viewRectangle);
            DrawTonemap(hdrTarget, viewRectangle.Width, viewRectangle.Height, frame.Environment.Tonemapper, frame.Environment.Exposure);

            if (composeInterfaceAfter)
            {
                FlushPostTonemapRenderers(view, in frame);

                long uiComposeStartTimestamp = Stopwatch.GetTimestamp();

                (view.UICompositionService ?? DefaultUICompositionService.Instance)
                    .Compose(_graphicsDevice, view, in frame);

                view.RenderStats.UiComposeCpuMilliseconds += GetElapsedMilliseconds(uiComposeStartTimestamp);
            }
        }
        finally
        {
            RenderTargetPool.Shared.Release(hdrTarget);
        }
    }

    /// <summary>
    /// Adds the bloom of the view to its HDR scene target (ADR-0104), after the scene and before the tonemapping, so the
    /// interface composed afterwards is never touched. Skipped, with one warning, when the bloom shader is not loaded.
    /// </summary>
    private void ApplyBloom(RenderTarget2D hdrTarget, in RenderFrame frame)
    {
        if (_bloomShader == null)
        {
            if (!_bloomShaderMissingLogged)
            {
                _bloomShaderMissingLogged = true;
                Logs.WriteWarning("[Bloom] The bloom shader is not loaded: bloom is skipped.");
            }

            return;
        }

        _bloomRenderer.Render(
            hdrTarget,
            _bloomShader,
            frame.Environment.BloomThreshold,
            frame.Environment.BloomIntensity,
            frame.Environment.BloomRadius,
            _hdrLastUsedRender,
            _renderIndex);
    }

    /// <summary>
    /// Flushes the renderers that draw display-ready colours (<see cref="IPostTonemapRenderer"/>, the screen sprites and text)
    /// on the tonemapped destination, before the interface (ADR-0103). The depth of the scene lives in the HDR target, so the
    /// depth test is off for these flushes.
    /// </summary>
    private void FlushPostTonemapRenderers(RenderView view, in RenderFrame frame)
    {
        long flushStartTimestamp = Stopwatch.GetTimestamp();

        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        for (int index = 0; index < _renderers.Count; index++)
        {
            if (_renderers[index] is IPostTonemapRenderer postTonemapRenderer)
            {
                postTonemapRenderer.FlushPostTonemap(in frame, view.RenderStats);
            }
        }

        view.RenderStats.RendererFlushCpuMilliseconds += GetElapsedMilliseconds(flushStartTimestamp);
    }

    /// <summary>
    /// Binds what the legacy path would have bound to draw the view (target, surface, viewport) and clears its depth the
    /// way the legacy path does, so the interface composed after the tonemapping meets the state it always met.
    /// </summary>
    private void BindHdrDestination(
        RenderView view,
        ColorPipelineDestination destination,
        in FrameCapturePlan capturePlan,
        RenderTarget2D initialRenderTarget,
        Rectangle viewRectangle)
    {
        switch (destination)
        {
            case ColorPipelineDestination.SceneRt:
            case ColorPipelineDestination.RetainedTarget:
                var sceneTarget = capturePlan.RetainThisFrame
                    ? Retention.EnsureRetained(_graphicsDevice, viewRectangle.Width, viewRectangle.Height)
                    : EnsureSceneRt(viewRectangle.Width, viewRectangle.Height);
                _graphicsDevice.SetRenderTarget(sceneTarget);
                _graphicsDevice.Viewport = new Viewport(0, 0, viewRectangle.Width, viewRectangle.Height);
                _graphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Black, 1.0f, 0);
                break;

            case ColorPipelineDestination.InitialTarget:
                BindInitialTargetKeepingBackBuffer(initialRenderTarget);
                view.Surface.Apply(_graphicsDevice);
                ClearDestinationDepth(view);
                break;

            case ColorPipelineDestination.ViewSurface:
                view.Surface.Apply(_graphicsDevice);
                ClearDestinationDepth(view);
                break;

            default:
                throw new InvalidOperationException($"No destination to tonemap into: {destination}.");
        }
    }

    /// <summary>
    /// Binds the initial target after the scene (or the HDR target) was drawn elsewhere. Binding the back buffer from a
    /// render target clears it when its <see cref="RenderTargetUsage"/> is <c>DiscardContents</c> (the default of the
    /// presentation parameters; the clear happens at the binding, see ADR-0086), which would erase the views already drawn
    /// to the same back buffer (split-screen). The usage is read at each binding, so it is set to
    /// <c>PreserveContents</c> for this binding only and put back at once; the legacy path never calls this.
    /// </summary>
    private void BindInitialTargetKeepingBackBuffer(RenderTarget2D initialRenderTarget)
    {
        var presentationParameters = _graphicsDevice.PresentationParameters;
        var previousUsage = presentationParameters.RenderTargetUsage;
        presentationParameters.RenderTargetUsage = RenderTargetUsage.PreserveContents;
        try
        {
            _graphicsDevice.SetRenderTarget(initialRenderTarget);
        }
        finally
        {
            presentationParameters.RenderTargetUsage = previousUsage;
        }
    }

    private void ClearDestinationDepth(RenderView view)
    {
        var depthClear = BuildDepthClearOptions(view);
        if (depthClear != 0)
        {
            _graphicsDevice.Clear(depthClear, Color.Black, 1.0f, 0);
        }
    }

    /// <summary>
    /// Draws the HDR target 1:1 into the current viewport through the tonemap effect: opaque, no depth, every texel
    /// replaced by its tonemapped and display-encoded colour.
    /// </summary>
    private void DrawTonemap(RenderTarget2D hdrTarget, int width, int height, TonemapOperator tonemapper, float exposure)
    {
        _tonemapShader.SelectTechnique(tonemapper == TonemapOperator.Reinhard ? ReinhardTechniqueName : AcesApproxTechniqueName);
        _tonemapShader.SetParameter(ShaderParameterNames.Exposure, exposure);

        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
            SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, _tonemapShader.Effect);
        _spriteBatch.Draw(hdrTarget, new Rectangle(0, 0, width, height), Color.White);
        _spriteBatch.End();
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

    private static double GetElapsedMilliseconds(long startTimestamp)
    {
        return (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
    }

    /// <summary>
    /// Creates or resizes <see cref="_sceneRt"/> to cover <paramref name="width"/>×<paramref name="height"/>.
    /// Uses <see cref="RenderTargetUsage.PreserveContents"/> so that switching to the shadow-map
    /// atlas and back does not discard the scene content.
    /// </summary>
    private RenderTarget2D EnsureSceneRt(int width, int height)
    {
        if (_sceneRt != null && !_sceneRt.IsDisposed
            && _sceneRt.Width == width && _sceneRt.Height == height)
        {
            return _sceneRt;
        }

        _sceneRt?.Dispose();
        _sceneRt = new RenderTarget2D(
            _graphicsDevice,
            width,
            height,
            false,
            SurfaceFormat.Color,
            DepthFormat.Depth24Stencil8,
            0,
            RenderTargetUsage.PreserveContents);
        return _sceneRt;
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
