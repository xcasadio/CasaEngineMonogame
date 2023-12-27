using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.SceneManagement;

public interface IRenderGroupState
{
    List<RenderGroupElement> Elements { get; }
    RenderInfo GetPipelineAndResources(GraphicsDevice graphicsDevice);
    void ReleaseUnmanagedResources();
}