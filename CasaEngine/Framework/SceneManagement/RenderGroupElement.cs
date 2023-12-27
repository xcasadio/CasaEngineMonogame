using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.SceneManagement;

public struct RenderGroupElement
{
    public List<IPrimitiveSet> PrimitiveSets;
    public Matrix ModelViewMatrix;
    public VertexBuffer VertexBuffer;
    public IndexBuffer IndexBuffer;
}