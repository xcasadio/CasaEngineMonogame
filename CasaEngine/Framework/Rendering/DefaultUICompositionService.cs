using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Default UI composition service that draws the hosted view UI runtime into the current surface,
/// then any post-UI overlays registered on the view (D2, screen-effect-above-ui-tasks.md T1.1).
/// </summary>
public sealed class DefaultUICompositionService : IUICompositionService
{
    public static readonly DefaultUICompositionService Instance = new();

    public void Compose(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame)
    {
        view.UIView?.Draw();

        var overlays = view.PostUIOverlays;
        for (var i = 0; i < overlays.Count; i++)
        {
            overlays[i].Draw(graphicsDevice, view, in frame);
        }
    }
}