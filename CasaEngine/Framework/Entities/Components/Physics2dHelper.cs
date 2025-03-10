﻿using BulletSharp;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Shapes;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game.Components.Physics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Entities.Components;

public static class Physics2dHelper
{
    public static CollisionObject? CreateCollisionsFromSprite(Collision2d collisionShape, Vector3 localScale, Matrix worldMatrix,
        PhysicsEngineComponent physicsEngineComponent, ICollideableComponent collideableComponent, Color color)
    {
        switch (collisionShape.Shape.Type)
        {
            //case Shape2dType.Compound:
            //    break;
            //case Shape2dType.Polygone:
            //    break;
            case Shape2dType.Rectangle:
                var rectangle = collisionShape.Shape as ShapeRectangle;
                var box = new BoxShape(rectangle.Width / 2f, rectangle.Height / 2f, 0.5f);
                box.LocalScaling = localScale;
                box.UserObject = collideableComponent;
                return physicsEngineComponent.CreateGhostObject(worldMatrix, collideableComponent, box, color);
            //case Shape2dType.Circle:
            //    break;
            //case Shape2dType.Line:
            //    break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return null;
    }

    public static void UpdateBodyTransformation(Vector3 position, Quaternion rotation, Vector3 scale,
        CollisionObject collisionObject, Shape2d shape2d, Point origin, Rectangle spriteBounds)
    {
        var rect = shape2d as ShapeRectangle;
        var translation = new Vector3(
            position.X + (rect.Position.X - origin.X + rect.Width / 2f) * scale.X,
            position.Y - (rect.Position.Y - origin.Y + rect.Height / 2f) * scale.Y,
            position.Z);
        collisionObject.WorldTransform = MatrixExtensions.Transformation(scale, rotation, translation);
    }
}