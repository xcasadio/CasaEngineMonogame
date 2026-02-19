using CasaEngine.Framework.Rendering;

namespace CasaEngine.Framework.Game.Components;

/// <summary>
/// Implemented by renderer components that support per-view flushing.
/// Instead of reading GameManager.ActiveCamera, they receive a <see cref="RenderFrame"/>
/// containing the camera data for the current view.
/// </summary>
public interface IViewFlushableRenderer
{
    /// <summary>
    /// Renders all queued primitives using the provided camera frame, then clears the queue.
    /// </summary>
    void Flush(in RenderFrame frame);
}
