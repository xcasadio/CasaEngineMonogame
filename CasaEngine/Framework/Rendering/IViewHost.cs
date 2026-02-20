namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Represents the UI container that hosts a <see cref="RenderView"/>
/// (e.g. a docked editor panel, a split-screen region, or a mini-map area).
///
/// Responsibilities:
/// <list type="bullet">
///   <item>Owns the view's <see cref="IRenderSurface"/> — disposes it when closed.</item>
///   <item>Notifies the engine when its size changes so the surface can be resized.</item>
///   <item>Reports visibility so the pipeline can skip hidden views.</item>
/// </list>
/// </summary>
public interface IViewHost : IDisposable
{
    /// <summary>Identifier of the hosted <see cref="RenderView"/>.</summary>
    ViewId ViewId { get; }

    /// <summary>Current width of the host area in pixels.</summary>
    int Width { get; }

    /// <summary>Current height of the host area in pixels.</summary>
    int Height { get; }

    /// <summary>Whether the host (and therefore its view) is currently visible.</summary>
    bool IsVisible { get; }

    /// <summary>
    /// Fired when the host area is resized.
    /// Parameters: (host, newWidth, newHeight).
    /// </summary>
    event Action<IViewHost, int, int>? Resized;

    /// <summary>Fired when the host is closed or disposed.</summary>
    event Action<IViewHost>? Closed;

    /// <summary>
    /// Notifies the host that the containing area has been resized and the
    /// render surface should be updated accordingly.
    /// </summary>
    void NotifyResized(int newWidth, int newHeight);
}
