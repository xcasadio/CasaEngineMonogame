using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Veldrid.SceneGraph.RenderGraph
{
    public interface IRenderGroup
    {
        bool HasDrawableElements();
        void Reset();
        IEnumerable<IRenderGroupState> GetStateList();
        IRenderGroupState GetOrCreateState(GraphicsDevice device, IPipelineState pso, PrimitiveType pt, VertexDeclaration vl);
    }
}