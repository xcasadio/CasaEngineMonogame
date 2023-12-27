using CasaEngine.Framework.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.SceneManagement;

public interface IDrawable : IObject
{
    string Name { get; set; }
    Type VertexType { get; }
    BoundingBox InitialBoundingBox { get; set; }
    VertexDeclaration VertexLayout { get; set; }
    List<IPrimitiveSet> PrimitiveSets { get; }
    IPipelineState PipelineState { get; set; }
    bool HasPipelineState { get; }
    StaticMesh Mesh { get; set; }
    void ConfigureDeviceBuffers(GraphicsDevice device);
    VertexBuffer GetVertexBufferForDevice(GraphicsDevice device);
    IndexBuffer GetIndexBufferForDevice(GraphicsDevice device);
    void DirtyBound();
    BoundingBox GetBoundingBox();
}