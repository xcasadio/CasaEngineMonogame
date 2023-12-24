using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Veldrid.SceneGraph.RenderGraph
{
    public interface ICullVisitor : INodeVisitor
    {
        IRenderGroup OpaqueRenderGroup { get; set; }
        IRenderGroup TransparentRenderGroup { get; set; }
        GraphicsDevice GraphicsDevice { get; set; }
        int RenderElementCount { get; }
        void Reset();
        void SetViewMatrix(Matrix viewMatrix);
        void SetProjectionMatrix(Matrix projectionMatrix);
        void Prepare();
    }
}