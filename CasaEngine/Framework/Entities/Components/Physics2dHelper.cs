using BulletSharp;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Shapes;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game.Components.Physics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Entities.Components;

public static class Physics2dHelper
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
            case Shape2dType.Circle:
                break;
            case Shape2dType.Line:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return null;
    }

    public static void UpdateBodyTransformation(Entity entity, CollisionObject collisionObject, Shape2d shape2d,
        Point origin, Rectangle spriteBounds)
    {
        var rect = shape2d as ShapeRectangle;
        var scale = entity.Coordinates.Scale;
        var position = entity.Coordinates.Position;
        var translation = new Vector3(
            position.X + (rect.Position.X - origin.X + rect.Width / 2f) * scale.X,
            position.Y - (rect.Position.Y - origin.Y + rect.Height / 2f) * scale.Y,
            position.Z);
        var rotation = entity.Coordinates.Rotation;
        collisionObject.WorldTransform = MatrixExtensions.Transformation(scale, rotation, translation);
    }
}