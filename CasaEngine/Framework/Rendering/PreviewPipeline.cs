using CasaEngine.Framework.Game.Components;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// A simplified render pipeline for inspector asset previews
/// (mesh viewer, material preview, animation thumbnail, etc.).
///
/// Differences from <see cref="DefaultViewPipeline"/>:
/// <list type="bullet">
///   <item>Renders only a single mesh/entity rather than a full world.</item>
///   <item>Uses a fixed, simplified lighting setup (directional + ambient).</item>
///   <item>Renders against a solid background color (no skybox/world).</item>
/// </list>
///
/// This class is a stub — expand it when implementing the inspector asset previewer.
/// </summary>
public sealed class PreviewPipeline : IViewRenderPipeline
{
    /// <summary>Background color of the preview render. Default: dark grey.</summary>
    public Color BackgroundColor { get; set; } = new Color(0.15f, 0.15f, 0.15f);

    /// <inheritdoc/>
    /// <remarks>
    /// Current implementation falls back to <see cref="DefaultViewPipeline"/> behaviour.
    /// Replace with a dedicated single-mesh render when the inspector system is built.
    /// </remarks>
    public void RenderView(
        GraphicsDevice                        graphicsDevice,
        RenderView                            view,
        in RenderFrame                        frame,
        IReadOnlyList<IViewFlushableRenderer> renderers)
    {
        // TODO: Replace with a single-mesh, simplified-lighting pass.
        // For now, render the full world to keep the preview functional.
        view.World.Draw(in frame);

        foreach (var renderer in renderers)
        {
            renderer.Flush(in frame);
        }
    }
}
