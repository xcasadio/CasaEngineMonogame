using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.Entities.Components;

public readonly struct CharacterControllerGroundInfo
{
    public static CharacterControllerGroundInfo None { get; } = new(false, Vector3.Up, null, 0f);

    public CharacterControllerGroundInfo(bool isGrounded, Vector3 normal, PhysicsBaseComponent collider, float slopeAngle)
    {
        IsGrounded = isGrounded;
        Normal = normal == Vector3.Zero ? Vector3.Up : Vector3.Normalize(normal);
        Collider = collider;
        SlopeAngle = slopeAngle;
    }

    public bool IsGrounded { get; }

    public Vector3 Normal { get; }

    public PhysicsBaseComponent Collider { get; }

    public float SlopeAngle { get; }
}