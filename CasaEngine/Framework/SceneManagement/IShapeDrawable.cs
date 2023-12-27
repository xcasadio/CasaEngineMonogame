using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.SceneManagement;

public interface IShapeDrawable<T> : IGeometry<T> where T : struct, ISettablePrimitiveElement, IVertexType
{

}