namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Manages the list of active <see cref="RenderView"/> instances for the multi-view render pipeline.
/// </summary>
public sealed class ViewManager
{
    private readonly List<RenderView> _views = new();

    /// <summary>Active render views (read-only).</summary>
    public IReadOnlyList<RenderView> Views => _views;

    /// <summary>Adds a view.</summary>
    public void Add(RenderView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _views.Add(view);
    }

    /// <summary>Removes a view.</summary>
    public bool Remove(RenderView view) => _views.Remove(view);

    /// <summary>Removes all views.</summary>
    public void Clear() => _views.Clear();
}
