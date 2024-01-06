using CasaEngine.Framework.SceneManagement;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Entities.Components;

public interface IComponentDrawable
{
    BoundingBox GetBoundingBox();
    void Draw();

    void Apply(CullVisitor cullVisitor);
}