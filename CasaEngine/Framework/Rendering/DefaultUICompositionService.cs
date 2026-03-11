using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Default UI composition service that draws the hosted view UI runtime into the current surface.
/// </summary>
public sealed class DefaultUICompositionService : IUICompositionService
{
    public static readonly DefaultUICompositionService Instance = new();

    public void Compose(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame)
    {
        view.UIView?.Draw();
    }
}