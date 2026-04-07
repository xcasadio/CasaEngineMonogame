using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.Entities;

public interface IBoundingBoxable
{
    BoundingBox BoundingBox { get; }
}