using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.Entities.Components;

public interface IComponentDrawable
{
    BoundingBox GetBoundingBox();
    void Draw(float elapsedTime);
}