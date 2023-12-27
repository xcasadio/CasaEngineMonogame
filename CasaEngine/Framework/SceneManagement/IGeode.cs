using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SceneManagement;

public interface IGeode : INode
{
    IReadOnlyList<IDrawable> Drawables { get; }
    BoundingBox GetBoundingBox();
    event Func<INode, BoundingBox> ComputeBoundingBoxCallback;
    void AddDrawable(IDrawable drawable);
}