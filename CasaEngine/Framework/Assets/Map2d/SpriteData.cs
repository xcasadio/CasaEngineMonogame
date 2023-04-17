using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Map2d;

public class SpriteData : Asset
{
    public Rectangle PositionInTexture { get; set; }
    public Vector2 Origin { get; set; }
    //List<Vector2I> m_Sockets;
    ////List<Collision> _collisionShapes;
};