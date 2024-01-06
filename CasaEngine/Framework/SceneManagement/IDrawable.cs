using CasaEngine.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SceneManagement;

public interface IDrawable : IObject
{
    string Name { get; set; }
    BoundingBox InitialBoundingBox { get; set; }
    StaticMesh Mesh { get; set; }
    void DirtyBound();
    BoundingBox GetBoundingBox();
}