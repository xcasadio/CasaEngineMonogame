using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.SceneManagement;

public class RenderInfo
{
    public GraphicsDevice GraphicsDevice;
    public Matrix ModelViewBuffer;
    public List<uint> UniformStrides;

    public RenderInfo()
    {
        UniformStrides = new List<uint>();
    }
}