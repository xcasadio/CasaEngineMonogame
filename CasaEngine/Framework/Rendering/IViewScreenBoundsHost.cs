using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering;

public interface IViewScreenBoundsHost
{
    Rectangle ScreenBounds { get; }
}