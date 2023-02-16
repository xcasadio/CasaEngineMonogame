using CasaEngine.AI.Messaging;
using CasaEngine.Core.Math.Shape2D;
using Microsoft.Xna.Framework;

namespace CasaEngine.Gameplay.Design
{
    public interface ICollide2Dable
        : IMessageable
    {
        Shape2DObject[] Shape2DObjectList { get; }
        Vector2 Position { get; }
    }
}
