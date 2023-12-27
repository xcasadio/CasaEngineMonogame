using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.SceneManagement;

public interface ISettablePrimitiveElement : IPrimitiveElement
{
    bool HasPosition { get; }
    bool HasNormal { get; }
    bool HasTexCoord { get; }
    bool HasColor3 { get; }
    bool HasColor4 { get; }

    void SetPosition(Vector3 position);
    void SetNormal(Vector3 normal);
    void SetTexCoord(Vector2 texCoord);
    void SetColor3(Vector3 color);
    void SetColor4(Vector4 color);
    VertexDeclaration GetVertexLayoutDescription();
}