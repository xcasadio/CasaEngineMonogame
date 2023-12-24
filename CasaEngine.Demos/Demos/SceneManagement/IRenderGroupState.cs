using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Veldrid.SceneGraph.RenderGraph
{
    public interface IRenderGroupState
    {
        List<RenderGroupElement> Elements { get; }
        RenderInfo GetPipelineAndResources(GraphicsDevice graphicsDevice);
        void ReleaseUnmanagedResources();
    }
}