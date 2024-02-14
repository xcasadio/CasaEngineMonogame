using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Graphics;

/// <summary>
/// basically a wide spectrum vertice structure.
/// </summary>
public struct VertexPositionTextureNormalTangentWeights : IVertexType
{
    public Vector3 Position;
    public Vector4 Color;
    public Vector3 Normal;
    public Vector2 TextureCoordinate;
    public Vector3 Tangent;
    public Vector3 BiTangent;
    public Vector4 BlendIndices;
    public Vector4 BlendWeights;

    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(VertexElementByteOffset.StartVector3(), VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(VertexElementByteOffset.Vector4(), VertexElementFormat.Vector4, VertexElementUsage.Color, 0),
        new VertexElement(VertexElementByteOffset.Vector3(), VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(VertexElementByteOffset.Vector2(), VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(VertexElementByteOffset.Vector3(), VertexElementFormat.Vector3, VertexElementUsage.Normal, 1),
        new VertexElement(VertexElementByteOffset.Vector3(), VertexElementFormat.Vector3, VertexElementUsage.Normal, 2),
        new VertexElement(VertexElementByteOffset.Vector4(), VertexElementFormat.Vector4, VertexElementUsage.BlendIndices, 0),
        new VertexElement(VertexElementByteOffset.Vector4(), VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 0)
    );
    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}