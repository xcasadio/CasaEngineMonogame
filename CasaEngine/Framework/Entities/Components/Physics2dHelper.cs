using BulletSharp;
using CasaEngine.Core.Shapes;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game.Components.Physics;

namespace CasaEngine.Framework.Entities.Components;

public class Physics2dHelper
{
    public static CollisionObject? CreateCollisionsFromSprite(Collision2d collisionShape, Entity entity,
        PhysicsEngineComponent physicsEngineComponent, ICollideableComponent collideableComponent, Color color)
    {
        switch (collisionShape.Shape.Type)
        {
            case Shape2dType.Compound:
                break;
            case Shape2dType.Polygone:
                break;
            case Shape2dType.Rectangle:
                var rectangle = collisionShape.Shape as ShapeRectangle;
                var worldMatrix = entity.Coordinates.WorldMatrix;
                return physicsEngineComponent.CreateGhostObject(rectangle, ref worldMatrix, collideableComponent, color);
                break;
            case Shape2dType.Circle:
                break;
            case Shape2dType.Line:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return null;
    }
}