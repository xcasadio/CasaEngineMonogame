using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SceneManagement;

public interface ICullVisitor : INodeVisitor
{
    void Reset();
    void SetView(Matrix viewMatrix, Matrix projectionMatrix);
}