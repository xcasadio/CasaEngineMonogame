using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.SceneManagement;

public interface IGeometry<T> : IDrawable where T : struct, IPrimitiveElement, IVertexType
{/*
    T[] VertexData { get; set; }
    uint[] IndexData { get; set; }*/
}