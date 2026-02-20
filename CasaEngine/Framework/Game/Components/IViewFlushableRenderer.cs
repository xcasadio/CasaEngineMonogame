using CasaEngine.Framework.Rendering;

namespace CasaEngine.Framework.Game.Components;

/// <summary>
/// Implemented by renderer components that support per-view flushing via <see cref="RenderPipeline"/>.
/// Each view provides a <see cref="RenderFrame"/> with the camera data for that view,
/// removing the need for renderers to read any global camera state.
/// </summary>
public interface IViewFlushableRenderer
{
    /// <summary>
    /// Renders all queued primitives using the provided camera frame, then clears the queue.
    /// </summary>
    void Flush(in RenderFrame frame);
}
