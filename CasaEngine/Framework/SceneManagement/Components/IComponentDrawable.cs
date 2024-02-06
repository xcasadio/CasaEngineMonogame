using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SceneManagement.Components;

public interface IComponentDrawable
{
    BoundingBox GetBoundingBox();
    void Draw(float elapsedTime);
}