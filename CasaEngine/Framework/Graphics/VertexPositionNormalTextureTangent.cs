using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Graphics;

public struct VertexPositionNormalTextureTangent : IVertexType
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 TextureCoordinate;
    public Vector4 Tangent;

    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.Tangent, 0));

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

    public VertexPositionNormalTextureTangent(Vector3 position, Vector3 normal, Vector2 textureCoordinate, Vector4 tangent)
    {
        Position = position;
        Normal = normal;
        TextureCoordinate = textureCoordinate;
        Tangent = tangent;
    }
}