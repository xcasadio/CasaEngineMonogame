using CasaEngine.Core.Maths.Shape2D;
using CasaEngine.Framework.AI.Messaging;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Gameplay.Design;

public interface ICollide2Dable
    : IMessageable
{
    Shape2dObject[] Shape2DObjectList { get; }
    Vector2 Position { get; }
}