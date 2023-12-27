using SharpDX.Direct3D11;

namespace CasaEngine.Framework.SceneManagement;

public interface IBindable : IObject
{
    BufferDescription BufferDescription { get; }

    //ResourceLayoutElementDescription ResourceLayoutElementDescription { get; }

    //DeviceBufferRange DeviceBufferRange { get; }

    //DeviceBuffer ConfigureDeviceBuffers(GraphicsDevice device, ResourceFactory factory);
}