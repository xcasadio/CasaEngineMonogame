namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Manages the list of active <see cref="RenderView"/> instances for the multi-view render pipeline.
/// </summary>
public sealed class ViewManager
{
    private readonly List<RenderView> _views = new();

    /// <summary>Active render views (read-only).</summary>
    public IReadOnlyList<RenderView> Views => _views;

    /// <summary>
    /// The primary view used for editor overlays and UI interaction (gizmo, grid, axes,
    /// screen resize, entity focus, drag-drop raycasting).
    /// Automatically set to the first view added. Can be overridden with
    /// <see cref="SetActive"/>.
    /// </summary>
    public RenderView? ActiveView { get; private set; }

    /// <summary>
    /// Adds a view. If no <see cref="ActiveView"/> is set yet, this view becomes the active one.
    /// </summary>
    public void Add(RenderView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _views.Add(view);
        ActiveView ??= view;
    }

    /// <summary>
    /// Designates <paramref name="view"/> as the <see cref="ActiveView"/>.
    /// The view must already have been added.
    /// </summary>
    public void SetActive(RenderView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        ActiveView = view;
    }

    /// <summary>
    /// Removes a view. If the removed view was the <see cref="ActiveView"/>,
    /// the active view is reset to the first remaining view, or null if empty.
    /// </summary>
    public bool Remove(RenderView view)
    {
        var removed = _views.Remove(view);
        if (removed && ActiveView == view)
        {
            ActiveView = _views.Count > 0 ? _views[0] : null;
        }
        return removed;
    }

    /// <summary>Removes all views and resets <see cref="ActiveView"/> to null.</summary>
    public void Clear()
    {
        _views.Clear();
        ActiveView = null;
    }
}
