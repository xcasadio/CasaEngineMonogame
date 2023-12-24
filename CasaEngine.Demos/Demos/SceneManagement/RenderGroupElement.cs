using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Veldrid.SceneGraph.RenderGraph;

public struct RenderGroupElement
{
    public List<IPrimitiveSet> PrimitiveSets;
    public Matrix ModelViewMatrix;
    public VertexBuffer VertexBuffer;
    public IndexBuffer IndexBuffer;
}