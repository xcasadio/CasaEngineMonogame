
using CasaEngine.Framework.UI;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Manages the list of active <see cref="RenderView"/> instances for the multi-view render pipeline.
///
/// v2 additions:
/// <list type="bullet">
///   <item>Stable <see cref="ViewId"/> keys — use <see cref="CreateView"/> / <see cref="TryGetView"/>.</item>
///   <item>Events: <see cref="ViewAdded"/>, <see cref="ViewRemoved"/>, <see cref="ViewResized"/>, <see cref="ViewInvalidated"/>.</item>
///   <item>Per-view input: <see cref="ScreenToView"/>, <see cref="ViewToWorldRay"/>, <see cref="CaptureInput"/>.</item>
/// </list>
/// </summary>
public sealed class ViewManager
{
    private readonly List<RenderView>              _views  = new();
    private readonly Dictionary<ViewId, RenderView> _byId   = new();

    // ---- Events ----

    /// <summary>Fired when a view is added to the manager.</summary>
    public event Action<RenderView> ViewAdded;

    /// <summary>Fired when a view is removed from the manager.</summary>
    public event Action<RenderView> ViewRemoved;

    /// <summary>
    /// Fired when a view's host notifies a resize.
    /// Parameters: (view, newWidth, newHeight).
    /// </summary>
    public event Action<RenderView, int, int> ViewResized;

    /// <summary>Fired when <see cref="RenderView.Invalidate"/> causes a re-render.</summary>
    public event Action<RenderView> ViewInvalidated;

    // ---- Views ----

    /// <summary>Active render views (read-only, ordered by insertion).</summary>
    public IReadOnlyList<RenderView> Views => _views;

    /// <summary>
    /// When set, <see cref="ApplyBackBufferLayout"/> automatically distributes all
    /// BackBuffer views across the screen according to this split mode.
    /// Set to null to disable automatic layout (manual or custom split only).
    /// </summary>
    public SplitMode? AutoLayoutMode { get; set; }

    /// <summary>
    /// Distributes all BackBuffer views across a screen of the given size using
    /// <see cref="AutoLayoutMode"/>. Updates each view's <see cref="BackBufferSurface.ViewportRect"/>
    /// and notifies the camera via <c>OnScreenResized</c>.
    /// Does nothing if <see cref="AutoLayoutMode"/> is null or there are no BackBuffer views.
    /// </summary>
    public void ApplyBackBufferLayout(int screenWidth, int screenHeight)
    {
        SynchronizeHostStates();

        if (AutoLayoutMode == null)
        {
            return;
        }

        var bbViews = new List<RenderView>();
        foreach (var v in _views)
            if (v.Surface is BackBufferSurface)
            {
                bbViews.Add(v);
            }

        if (bbViews.Count == 0)
        {
            return;
        }

        var rects = SplitScreenLayout.Compute(screenWidth, screenHeight, bbViews.Count, AutoLayoutMode.Value);
        for (int i = 0; i < bbViews.Count; i++)
        {
            ((BackBufferSurface)bbViews[i].Surface).ViewportRect = rects[i];
            bbViews[i].Camera?.OnScreenResized(rects[i].Width, rects[i].Height);
        }
    }

    /// <summary>
    /// The primary view used for editor overlays and UI interaction (gizmo, grid, axes,
    /// screen resize, entity focus, drag-drop raycasting).
    /// Automatically set to the first view added. Can be overridden with <see cref="SetActive"/>.
    /// </summary>
    public RenderView ActiveView { get; private set; }

    // ---- Input capture ----

    /// <summary>
    /// The view that is currently capturing all input events (e.g. during a gizmo drag).
    /// Null when no capture is active.
    /// </summary>
    public RenderView InputCaptureView { get; private set; }

    // ---- Factory / registry ----

    /// <summary>
    /// Creates and registers a new <see cref="RenderView"/> from the provided definition,
    /// assigning it a stable <see cref="ViewId"/>.
    /// </summary>
    /// <returns>The stable ViewId for the newly created view.</returns>
    public ViewId CreateView(ViewDefinition def)
    {
        var id   = ViewId.Next();
        var view = new RenderView(def.World, def.Camera, def.Surface)
        {
            Id              = id,
            Name            = def.Name,
            EnvironmentOverride = def.EnvironmentOverride,
            ClearColor      = def.ClearColor,
            ClearColorBuffer = def.ClearColorBuffer,
            ClearDepthBuffer = def.ClearDepthBuffer,
            UpdateMode      = def.UpdateMode,
            TargetFrameRate = def.TargetFrameRate,
            ResolutionScale = def.ResolutionScale,
            Pipeline        = def.Pipeline,
            Presenter       = def.Presenter,
        };

        RegisterView(view);
        return id;
    }

    /// <summary>
    /// Adds a pre-constructed view. If it has no ViewId yet, one is assigned.
    /// If no <see cref="ActiveView"/> is set yet, this view becomes the active one.
    /// </summary>
    public void Add(RenderView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (view.Id.IsEmpty)
        {
            view.Id = ViewId.Next();
        }

        RegisterView(view);
    }

    /// <summary>
    /// Tries to retrieve the view registered under <paramref name="id"/>.
    /// Returns false if the id is not registered.
    /// </summary>
    public bool TryGetView(ViewId id, out RenderView view)
    {
        if (_byId.TryGetValue(id, out var v))
        {
            view = v;
            return true;
        }

        view = null!;
        return false;
    }

    public IUIViewRuntime GetUIView(ViewId id)
    {
        return TryGetView(id, out var view) ? view.UIView : null;
    }

    public IUIViewRuntime GetActiveUIView()
    {
        SynchronizeHostStates();
        return ActiveView?.UIView;
    }

    /// <summary>
    /// Designates <paramref name="view"/> as the <see cref="ActiveView"/>.
    /// The view must already have been added.
    /// </summary>
    public void SetActive(RenderView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (ActiveView != null)
        {
            ActiveView.IsActive = false;
        }

        ActiveView = view;
        view.IsActive = true;
    }

    /// <summary>
    /// Removes a view. If the removed view was the <see cref="ActiveView"/>,
    /// the active view is reset to the first remaining view, or null if empty.
    /// </summary>
    public bool Remove(RenderView view)
    {
        var removed = _views.Remove(view);
        if (removed)
        {
            _byId.Remove(view.Id);
            UnhookHost(view);
            view.Invalidated -= OnViewInvalidated;

            if (InputCaptureView == view)
            {
                InputCaptureView = null;
            }

            if (ActiveView == view)
            {
                ActiveView = _views.Count > 0 ? _views[0] : null;
                if (ActiveView != null)
                {
                    ActiveView.IsActive = true;
                }
            }

            ViewRemoved?.Invoke(view);
        }

        return removed;
    }

    /// <summary>Removes all views and resets <see cref="ActiveView"/> to null.</summary>
    public void Clear()
    {
        foreach (var view in _views)
        {
            UnhookHost(view);
            view.Invalidated -= OnViewInvalidated;
            ViewRemoved?.Invoke(view);
        }

        _views.Clear();
        _byId.Clear();
        ActiveView        = null;
        InputCaptureView  = null;
    }

    // ---- Input mapping ----

    /// <summary>
    /// Finds the topmost view whose viewport contains <paramref name="screenPoint"/> and
    /// returns the view plus the point expressed in the view's local space (0,0 = top-left).
    ///
    /// Uses <see cref="InputCaptureView"/> first if a capture is active.
    ///
    /// Returns (null, default) if no view contains the point.
    /// </summary>
    public (RenderView view, Vector2 localPoint) ScreenToView(Point screenPoint)
    {
        SynchronizeHostStates();

        // If a view has captured input, always route to it.
        if (InputCaptureView != null)
        {
            var vp = GetScreenBounds(InputCaptureView);
            var local = new Vector2(screenPoint.X - vp.X, screenPoint.Y - vp.Y);
            return (InputCaptureView, local);
        }

        // Iterate in reverse insertion order so the last-added view is tested first.
        for (int i = _views.Count - 1; i >= 0; i--)
        {
            var view = _views[i];
            if (!view.Enabled || !view.IsVisible)
            {
                continue;
            }

            var vp = GetScreenBounds(view);
            if (vp.Contains(screenPoint))
            {
                var local = new Vector2(screenPoint.X - vp.X, screenPoint.Y - vp.Y);
                return (view, local);
            }
        }

        return (null, default);
    }

    /// <summary>
    /// Computes a world-space ray from <paramref name="localPoint"/> (view-local pixels,
    /// (0,0) = top-left of the view) using the view's camera and viewport.
    /// </summary>
    public Ray ViewToWorldRay(RenderView view, Vector2 localPoint)
    {
        ArgumentNullException.ThrowIfNull(view);
        return RayHelper.CalculateRayFromScreenCoordinate(
            localPoint,
            view.Camera.ProjectionMatrix,
            view.Camera.ViewMatrix,
            view.Camera.Viewport);
    }

    // ---- Input capture ----

    /// <summary>
    /// Starts routing all screen input to <paramref name="view"/>,
    /// regardless of the mouse cursor position.
    /// Call <see cref="ReleaseInput"/> when the drag/interaction ends.
    /// </summary>
    public void CaptureInput(RenderView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        SynchronizeHostStates();
        if (!IsPresented(view))
        {
            return;
        }

        InputCaptureView = view;
    }

    /// <summary>
    /// Ends the current input capture started by <see cref="CaptureInput"/>.
    /// </summary>
    public void ReleaseInput() => InputCaptureView = null;

    // ---- Internal helpers ----

    private void RegisterView(RenderView view)
    {
        _views.Add(view);
        _byId[view.Id] = view;

        RefreshHostState(view);

        // Wire host events
        if (view.Host != null)
        {
            view.Host.Resized += OnHostResized;
            view.Host.Closed  += OnHostClosed;
        }

        // The first view to be added (or the first after a Clear) becomes the active view.
        if (ActiveView == null)
        {
            ActiveView       = view;
            view.IsActive    = true;
        }

        // OnDemand views start as dirty so they render at least once.
        if (view.UpdateMode == ViewUpdateMode.OnDemand)
        {
            view.IsDirty = true;
        }

        view.Invalidated -= OnViewInvalidated;
        view.Invalidated += OnViewInvalidated;

        ViewAdded?.Invoke(view);
    }

    /// <summary>
    /// Re-hooks the <see cref="IViewHost"/> events for an already-registered view.
    /// Call this after setting <see cref="RenderView.Host"/> when the host was not yet
    /// available at the time the view was registered.
    /// </summary>
    public void HookViewHost(RenderView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.Host == null)
        {
            return;
        }

        RefreshHostState(view);

        // Unhook first to avoid double-subscription if called more than once.
        view.Host.Resized -= OnHostResized;
        view.Host.Closed  -= OnHostClosed;
        view.Host.Resized += OnHostResized;
        view.Host.Closed  += OnHostClosed;
    }

    /// <summary>
    /// Unsubscribes the ViewManager from <see cref="IViewHost"/> events on
    /// <paramref name="view"/> without removing the view from the registry.
    /// Use this before clearing <see cref="RenderView.Host"/> in <c>Detach()</c>.
    /// </summary>
    public void UnhookViewHost(RenderView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        UnhookHost(view);
    }

    private void UnhookHost(RenderView view)
    {
        if (view.Host != null)
        {
            view.Host.Resized -= OnHostResized;
            view.Host.Closed  -= OnHostClosed;
        }
    }

    private void OnHostResized(IViewHost host, int w, int h)
    {
        if (_byId.TryGetValue(host.ViewId, out var view))
        {
            RefreshHostState(view);
            ViewResized?.Invoke(view, w, h);
        }
    }

    private void OnViewInvalidated(RenderView view)
    {
        ViewInvalidated?.Invoke(view);
    }

    private void OnHostClosed(IViewHost host)
    {
        if (_byId.TryGetValue(host.ViewId, out var view))
        {
            Remove(view);
        }
    }

    public void SynchronizeHostStates()
    {
        foreach (var view in _views)
        {
            RefreshHostState(view);
        }

        if (InputCaptureView != null && !IsPresented(InputCaptureView))
        {
            InputCaptureView = null;
        }

        if (ActiveView != null && !IsPresented(ActiveView))
        {
            ActiveView.IsActive = false;
            ActiveView = _views.FirstOrDefault(IsPresented);
            if (ActiveView != null)
            {
                ActiveView.IsActive = true;
            }
        }
    }

    private static bool IsPresented(RenderView view)
    {
        return view.Enabled && view.IsVisible;
    }

    private static Rectangle GetScreenBounds(RenderView view)
    {
        if (view.Host is IViewScreenBoundsHost screenBoundsHost)
        {
            return screenBoundsHost.ScreenBounds;
        }

        return view.Surface.ViewportRect;
    }

    private static void RefreshHostState(RenderView view)
    {
        if (view.Host == null)
        {
            return;
        }

        var wasVisible = view.IsVisible;
        view.IsVisible = view.Host.IsVisible;

        if (!wasVisible && view.IsVisible)
        {
            view.Invalidate();
        }
    }
}
