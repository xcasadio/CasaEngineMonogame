using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.GUI;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Describes a render view: a combination of world, camera and output surface.
///
/// Extended in v2 with:
/// <list type="bullet">
///   <item>A <see cref="ViewId"/> for stable reference via <see cref="ViewManager"/>.</item>
///   <item><see cref="UpdateMode"/> / <see cref="TargetFrameRate"/> / throttle state.</item>
///   <item><see cref="ResolutionScale"/> for quality reduction on secondary views.</item>
///   <item><see cref="IsVisible"/> / <see cref="IsActive"/> lifecycle flags.</item>
///   <item>Optional <see cref="Pipeline"/>, <see cref="Presenter"/>, <see cref="Host"/> hooks.</item>
///   <item><see cref="ShowDebugOverlay"/> toggle.</item>
/// </list>
/// </summary>
public sealed class RenderView
{
    // ---- Identity ----

    /// <summary>Unique identifier assigned by <see cref="ViewManager"/>.</summary>
    public ViewId Id { get; internal set; } = ViewId.Empty;

    // ---- Core rendering inputs ----

    /// <summary>World to render in this view.</summary>
    public World.World World { get; set; }

    /// <summary>Camera used to compute View/Projection matrices.</summary>
    public CameraComponent Camera { get; set; }

    /// <summary>Output surface (backbuffer or RenderTarget).</summary>
    public IRenderSurface Surface { get; set; }

    // ---- Clear options ----

    /// <summary>Clear color. Default: CornflowerBlue.</summary>
    public Color ClearColor { get; set; } = Color.CornflowerBlue;

    /// <summary>If true, clears the color buffer before rendering. Default: true.</summary>
    public bool ClearColorBuffer { get; set; } = true;

    /// <summary>If true, clears the depth/stencil buffer before rendering. Default: true.</summary>
    public bool ClearDepthBuffer { get; set; } = true;

    // ---- Lifecycle ----

    /// <summary>
    /// If false, the view is completely skipped (no render, no update). Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// If false, the view exists logically but is not rendered (e.g. hidden tab).
    /// The last rendered frame is preserved in the RT. Default: true.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// When true, this view receives input events (mouse, keyboard).
    /// Only one view should be active at a time; managed by <see cref="ViewManager"/>.
    /// Default: false (set to true by ViewManager on Add when no active view exists).
    /// </summary>
    public bool IsActive { get; set; }

    // ---- Update mode / throttle ----

    /// <summary>
    /// Controls how often this view is re-rendered. Default: <see cref="ViewUpdateMode.RealTime"/>.
    /// </summary>
    public ViewUpdateMode UpdateMode { get; set; } = ViewUpdateMode.RealTime;

    /// <summary>
    /// Target frame-rate for <see cref="ViewUpdateMode.Throttled"/> mode. Default: 10 fps.
    /// </summary>
    public float TargetFrameRate { get; set; } = 10f;

    /// <summary>Accumulated time since the last render (internal, used by throttle).</summary>
    internal float ThrottleAccumulator { get; set; }

    /// <summary>
    /// Dirty flag used for <see cref="ViewUpdateMode.OnDemand"/> mode.
    /// Set by <see cref="Invalidate"/>; reset to false after rendering.
    /// The setter is internal — use <see cref="Invalidate"/> to set it.
    /// </summary>
    public bool IsDirty { get; internal set; } = true;

    /// <summary>
    /// Marks this view as needing a re-render on the next frame.
    /// Only relevant when <see cref="UpdateMode"/> is <see cref="ViewUpdateMode.OnDemand"/>.
    /// </summary>
    public void Invalidate()
    {
        IsDirty = true;
        Invalidated?.Invoke(this);
    }

    // ---- Resolution scale ----

    /// <summary>
    /// Render resolution multiplier (0.25..2.0, default 1.0).
    /// The presenter upscales to the destination size.
    /// </summary>
    public float ResolutionScale
    {
        get => _resolutionScale;
        set => _resolutionScale = Math.Clamp(value, 0.25f, 2.0f);
    }
    private float _resolutionScale = 1.0f;

    /// <summary>Per-view UI scaling service used to derive scale and safe area.</summary>
    public UIScaler UIScaler { get; set; } = new(new Point(1920, 1080));

    /// <summary>Relative safe-area inset used by <see cref="UIScaler"/> for this view.</summary>
    public float UISafeAreaInset
    {
        get => _uiSafeAreaInset;
        set => _uiSafeAreaInset = Math.Clamp(value, 0.0f, 0.45f);
    }
    private float _uiSafeAreaInset = 0.05f;

    // ---- Extended hooks ----

    /// <summary>Optional custom render pipeline. Null = <see cref="DefaultViewPipeline"/>.</summary>
    public IViewRenderPipeline? Pipeline { get; set; }

    /// <summary>
    /// Optional UI composition service executed by the view pipeline after world rendering.
    /// Null falls back to <see cref="DefaultUICompositionService"/>.
    /// </summary>
    public IUICompositionService? UICompositionService { get; set; }

    /// <summary>
    /// Optional presenter called after the render pipeline to display the result.
    /// Null = no post-render presentation step.
    /// </summary>
    public IViewPresenter? Presenter { get; set; }

    /// <summary>Optional host that owns this view's surface and handles resize/close.</summary>
    public IViewHost? Host { get; set; }

    // ---- UI integration ----

    /// <summary>
    /// Per-view MGUI UI root (desktop + screen stack).
    /// Automatically created by <see cref="ViewManager"/> when this view is added
    /// and disposed when this view is removed.
    /// Null before the view is registered or after it has been disposed.
    /// </summary>
    public IUIViewRuntime? UIView { get; set; }

    // ---- Debug ----

    /// <summary>Optional name for debugging purposes.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// When true, a <see cref="DebugOverlay"/> is drawn over this view each frame.
    /// Default: false.
    /// </summary>
    public bool ShowDebugOverlay { get; set; }

    /// <summary>
    /// Aggregated render statistics for the last completed render of this view.
    /// Reset by <see cref="RenderPipeline"/> before each rendered frame.
    /// </summary>
    public RenderStats RenderStats { get; } = new();

    // ---- User / editor metadata ----

    /// <summary>Raised whenever <see cref="Invalidate"/> marks the view dirty.</summary>
    public event Action<RenderView>? Invalidated;

    /// <summary>
    /// Arbitrary user-defined payload attached to this view.
    /// In editor mode this holds an <c>EditorViewContext</c> instance carrying
    /// the per-view camera, gizmo, grid, axis components and input providers.
    /// Null in standalone game builds.
    /// </summary>
    public object? Tag { get; set; }

    // ---- Constructor ----

    public RenderView(World.World world, CameraComponent camera, IRenderSurface surface)
    {
        World   = world;
        Camera  = camera;
        Surface = surface;
    }
}
