using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Design;

public interface IBoundingBoxable
{
    BoundingBox BoundingBox { get; }
}