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
        var rectangle = (ShapeRectangle)collision2d.Shape;
        var translation = new Vector3(
            position.X + (collision2d.LocalPosition.X - origin.X + rectangle.Width / 2f) * scale.X,
            position.Y - (collision2d.LocalPosition.Y - origin.Y + rectangle.Height / 2f) * scale.Y,
            position.Z);
        collisionObject.WorldTransform = MatrixExtensions.Transformation(scale, rotation, translation);
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
