using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Entities;

public interface IBoundingBoxable
{
    BoundingBox BoundingBox { get; }
}