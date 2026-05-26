using CasaEngine.Framework.Rendering;

namespace CasaEngine.Framework.Application.Components;

/// <summary>
/// Implemented by renderer components that support per-view flushing via <see cref="RenderPipeline"/>.
/// Each view provides a <see cref="RenderFrame"/> with the camera data for that view,
/// removing the need for renderers to read any global camera state.
/// </summary>
public interface IViewFlushableRenderer
{
    /// <summary>
    /// Renders all queued primitives using the provided camera frame, then clears the queue.
    /// When supplied, <paramref name="stats"/> aggregates counters for the current view.
    /// </summary>
    void Flush(in RenderFrame frame, RenderStats stats = null);
}
