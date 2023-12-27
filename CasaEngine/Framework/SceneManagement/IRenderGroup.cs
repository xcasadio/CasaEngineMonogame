using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.SceneManagement;

public interface IRenderGroup
{
    bool HasDrawableElements();
    void Reset();
    IEnumerable<IRenderGroupState> GetStateList();
    IRenderGroupState GetOrCreateState(GraphicsDevice device, IPipelineState pso, PrimitiveType pt, VertexDeclaration vl);
}