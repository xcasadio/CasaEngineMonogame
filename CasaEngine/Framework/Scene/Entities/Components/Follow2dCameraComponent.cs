using System.ComponentModel;
using CasaEngine.Core.Logging;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// Makes the camera of its entity follow an entity in the XY plane (ADR-0145). The camera stays still while the target
/// is inside a dead zone centred on the camera; when the target passes an edge of the zone, the camera advances by the
/// excess on that axis. The x, y position of the camera is then brought back inside <see cref="Limits"/>. Its z is
/// never changed.
/// <para/>
/// It writes the local position of the camera component: the camera is expected to have no parent. It does not drive a
/// <see cref="Camera2dComponent"/>, which places itself from its own target.
/// </summary>
[DisplayName("Follow Camera 2d")]
public class Follow2dCameraComponent : EntityComponent
{
    private bool _missingTargetReported;

    /// <summary>Name of the entity of the world to follow. Used when <see cref="Target"/> is not set by code.</summary>
    [SerializedProperty("target_entity_name")]
    public string TargetEntityName { get; set; } = string.Empty;

    /// <summary>Width and height of the dead zone, in world units, centred on the camera.</summary>
    [SerializedProperty("dead_zone")]
    public Vector2 DeadZone { get; set; }

    /// <summary>
    /// Rectangle of the world the x, y position of the camera stays in. A zero width leaves x free, a zero height
    /// leaves y free.
    /// </summary>
    public Rectangle Limits { get; set; }

    /// <summary>The followed entity. Set by code, or found by <see cref="TargetEntityName"/> in the world of the camera.</summary>
    public Entity Target { get; set; }

    public Follow2dCameraComponent()
    {
    }

    public Follow2dCameraComponent(Follow2dCameraComponent other) : base(other)
    {
        TargetEntityName = other.TargetEntityName;
        DeadZone = other.DeadZone;
        Limits = other.Limits;
        Target = other.Target;
    }

    public override Follow2dCameraComponent Clone()
    {
        return new Follow2dCameraComponent(this);
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);
        ResolveTarget(world);
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);

        if (Target != null && Target.ToBeRemoved)
        {
            Target = null;
        }

        if (Target == null)
        {
            ResolveTarget(Owner?.World);
        }

        CameraComponent camera = Owner?.GetComponent<CameraComponent>();
        SceneComponent targetRoot = Target?.RootComponent;
        if (camera == null || targetRoot == null)
        {
            return;
        }

        Vector3 targetPosition = targetRoot.WorldMatrixNoScale.Translation;
        Vector3 position = camera.LocalPosition;
        Vector3 followed = position;

        followed.X = FollowAxis(position.X, targetPosition.X, DeadZone.X * 0.5f);
        followed.Y = FollowAxis(position.Y, targetPosition.Y, DeadZone.Y * 0.5f);

        if (Limits.Width > 0)
        {
            followed.X = MathHelper.Clamp(followed.X, Limits.Left, Limits.Right);
        }

        if (Limits.Height > 0)
        {
            followed.Y = MathHelper.Clamp(followed.Y, Limits.Top, Limits.Bottom);
        }

        if (followed != position)
        {
            camera.LocalPosition = followed;
        }
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        //  Optional: a component saved without limits has none.
        if (element["limits"] is JObject limitsNode)
        {
            Limits = limitsNode.GetRectangle();
        }
    }

    /// <summary>Finds the entity named <see cref="TargetEntityName"/> in <paramref name="world"/>, when no target is set.</summary>
    internal void ResolveTarget(CasaEngine.Framework.Scene.World.World world)
    {
        if (Target != null || world == null || string.IsNullOrEmpty(TargetEntityName))
        {
            return;
        }

        IList<Entity> entities = world.Entities;
        for (int index = 0; index < entities.Count; index++)
        {
            Entity entity = entities[index];
            if (!entity.ToBeRemoved && string.Equals(entity.Name, TargetEntityName, StringComparison.Ordinal))
            {
                Target = entity;
                _missingTargetReported = false;
                return;
            }
        }

        if (!_missingTargetReported)
        {
            _missingTargetReported = true;
            Logs.WriteWarning($"Follow2dCameraComponent '{Name}' of entity '{Owner?.Name}': no entity named '{TargetEntityName}' in world '{world.Name}'. The camera does not follow anything.");
        }
    }

    private static float FollowAxis(float camera, float target, float halfZone)
    {
        float offset = target - camera;
        if (offset > halfZone)
        {
            return camera + offset - halfZone;
        }

        if (offset < -halfZone)
        {
            return camera + offset + halfZone;
        }

        return camera;
    }
}
