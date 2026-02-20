using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Handles the final presentation of a rendered <see cref="RenderView"/>
/// after the render pipeline has completed its pass.
///
/// A presenter is optional — if <see cref="RenderView.Presenter"/> is null,
/// the pipeline simply leaves the output in the surface (backbuffer viewport or RT).
///
/// Built-in implementations:
/// <list type="bullet">
///   <item><see cref="BackBufferPresenter"/> — blits an RT to a backbuffer rectangle with scale/fit modes.</item>
///   <item><see cref="TexturePresenter"/> — exposes the RT as a <see cref="Texture2D"/> for UI consumption.</item>
/// </list>
/// </summary>
public interface IViewPresenter
{
    /// <summary>
    /// Called after <paramref name="view"/> has been fully rendered.
    /// The device is in its post-pipeline state (render target restored to initial).
    /// </summary>
    void Present(GraphicsDevice graphicsDevice, RenderView view);
}
