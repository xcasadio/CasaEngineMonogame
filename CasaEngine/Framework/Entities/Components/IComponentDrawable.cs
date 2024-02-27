using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Entities.Components;

public interface IComponentDrawable
{
    BoundingBox GetBoundingBox();
    void Draw(float elapsedTime);
}