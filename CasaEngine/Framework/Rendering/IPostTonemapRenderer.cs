namespace CasaEngine.Framework.Rendering;

/// <summary>
/// A renderer whose output is display-ready (screen sprites and text of <c>Renderer2DComponent</c>): in the linear HDR
/// colour pipeline it must not be drawn into the HDR scene target, where it would be tonemapped and re-encoded (ADR-0103).
/// <see cref="DefaultViewPipeline.RenderScene"/> skips it and <see cref="RenderPipeline"/> flushes it through
/// <see cref="FlushPostTonemap"/> once the scene is tonemapped, before the interface is composed. In the legacy pipeline
/// nothing changes: <see cref="IViewFlushableRenderer.Flush"/> is called in the usual order with the usual states.
/// </summary>
public interface IPostTonemapRenderer : IViewFlushableRenderer
{
    /// <summary>
    /// Flushes the queued primitives on the tonemapped destination of the view. The depth buffer of the scene lives in the
    /// HDR target, not in the destination: the implementation draws with <c>DepthStencilState.None</c>
    /// (<see cref="RenderPipeline"/> sets it before the call, and the implementation must not replace it with a state that
    /// tests depth). It clears its queues like <see cref="IViewFlushableRenderer.Flush"/>.
    /// </summary>
    void FlushPostTonemap(in RenderFrame frame, RenderStats stats = null);
}
