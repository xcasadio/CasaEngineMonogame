using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Composes the UI phase for a render view after world rendering has completed.
/// </summary>
public interface IUICompositionService
{
    void Compose(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame);
}