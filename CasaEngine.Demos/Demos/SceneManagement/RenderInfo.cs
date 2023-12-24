using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Veldrid.SceneGraph.RenderGraph;

public class RenderInfo
{
    public GraphicsDevice GraphicsDevice;
    //public DeviceBuffer ModelViewBuffer;
    public List<uint> UniformStrides;

    public RenderInfo()
    {
        UniformStrides = new List<uint>();
    }
}