using CasaEngine.Core.Math.Extensions;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets.Sprites;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// Creates and places the physics bodies of the collision volumes authored on a sprite.
/// The shape is lowered by the simulation space policy of the project; the pose comes from the volume.
/// </summary>
public static class SpriteCollisionHelper
{
    public static PhysicsBody CreateCollisionBody(Collision2d collision2d, Vector3 localScale, Matrix worldMatrix,
        IPhysicsWorld physicsWorldContext, ICollideableComponent collideableComponent)
    {
        ArgumentNullException.ThrowIfNull(collision2d);

        var shape = physicsWorldContext.SpacePolicy.Lower(collision2d.Shape);
        int profileId = ResolveProfileId(collision2d);

        return physicsWorldContext.CreateGhostObject(worldMatrix, collideableComponent, shape, localScale, profileId,
            color: GetProfileDebugColor(profileId));
    }

    public static void UpdateBodyTransformation(Vector3 position, Quaternion rotation, Vector3 scale,
        PhysicsBody collisionObject, Collision2d collision2d, Point origin, Rectangle spriteBounds)
    {
        GetShapeHalfExtents(collision2d.Shape, out float halfWidth, out float halfHeight);

        var translation = new Vector3(
            position.X + (collision2d.LocalPosition.X - origin.X + halfWidth) * scale.X,
            position.Y - (collision2d.LocalPosition.Y - origin.Y + halfHeight) * scale.Y,
            position.Z);
        collisionObject.WorldTransform = MatrixExtensions.Transformation(scale, rotation, translation);
    }

    /// <summary>
    /// Half extents used to center a lowered volume on the authored top-left corner of the shape.
    /// A circle is placed by its radius, exactly like a rectangle is placed by its half size.
    /// </summary>
    private static void GetShapeHalfExtents(Shape2d shape, out float halfWidth, out float halfHeight)
    {
        switch (shape)
        {
            case ShapeRectangle rectangle:
                halfWidth = rectangle.Width / 2f;
                halfHeight = rectangle.Height / 2f;
                break;

            case ShapeCircle circle:
                halfWidth = circle.Radius;
                halfHeight = circle.Radius;
                break;

            default:
                throw new NotSupportedException(
                    $"A sprite collision volume of type '{shape?.Type}' cannot be placed in the world.");
        }
    }

    /// <summary>Profile of a collision volume: the profile it names, the reserved Trigger when it names none.</summary>
    public static int ResolveProfileId(Collision2d collision2d)
    {
        if (string.IsNullOrEmpty(collision2d.ProfileName))
        {
            return CollisionProfileIds.Trigger;
        }

        return GameSettings.PhysicsEngineSettings.CollisionProfiles.GetProfileId(collision2d.ProfileName);
    }

    /// <summary>
    /// Debug color of a collision volume. Drawing must never throw on authoring data, so an unknown
    /// profile name falls back to the reserved Trigger profile like an empty one.
    /// </summary>
    public static Color GetDebugColor(Collision2d collision2d)
    {
        var profiles = GameSettings.PhysicsEngineSettings.CollisionProfiles;
        int profileId = CollisionProfileIds.Trigger;

        if (!string.IsNullOrEmpty(collision2d.ProfileName)
            && profiles.TryGetProfileId(collision2d.ProfileName, out int namedProfileId))
        {
            profileId = namedProfileId;
        }

        return GetProfileDebugColor(profileId);
    }

    private static Color GetProfileDebugColor(int profileId)
    {
        return GameSettings.PhysicsEngineSettings.CollisionProfiles.GetProfile(profileId).DebugColor ?? Color.Green;
    }
}
