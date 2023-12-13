using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Design;

public interface IBoundingBoxable
{
#if EDITOR
    BoundingBox BoundingBox { get; }
#endif
}