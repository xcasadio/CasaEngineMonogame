using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.SceneManagement;

public interface IPrimitiveSet : IObject
{
    BoundingBox InitialBoundingBox { get; set; }
    IDrawable Drawable { get; }
    PrimitiveType PrimitiveTopology { get; set; }
    event Func<PrimitiveSet, BoundingBox> ComputeBoundingBoxCallback;
    void DirtyBound();
    BoundingBox GetBoundingBox();
    void Draw(GraphicsDevice graphicsDevice);
}