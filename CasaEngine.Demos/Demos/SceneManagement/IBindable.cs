using Microsoft.Xna.Framework.Graphics;
using SharpDX.Direct3D11;

namespace Veldrid.SceneGraph
{
    public interface IBindable : IObject
    {
        BufferDescription BufferDescription { get; }

        //ResourceLayoutElementDescription ResourceLayoutElementDescription { get; }

        //DeviceBufferRange DeviceBufferRange { get; }

        //DeviceBuffer ConfigureDeviceBuffers(GraphicsDevice device, ResourceFactory factory);
    }
}