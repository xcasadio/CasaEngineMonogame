using CasaEngine.Framework.SceneManagement.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics;

public struct ContactPoint : IEquatable<ContactPoint>
{
    public PhysicsComponent ColliderA;
    public PhysicsComponent ColliderB;
    public float Distance;
    public Vector3 Normal;
    public Vector3 PositionOnA;
    public Vector3 PositionOnB;

    public bool Equals(ContactPoint other)
    {
        return ((ColliderA == other.ColliderA && ColliderB == other.ColliderB)
                || (ColliderA == other.ColliderB && ColliderB == other.ColliderA))
               && Distance == other.Distance
               && Normal == other.Normal
               && PositionOnA == other.PositionOnA
               && PositionOnB == other.PositionOnB;
    }

    public override bool Equals(object obj) => obj is ContactPoint other && Equals(other);

    public override int GetHashCode()
    {
        return HashCode.Combine(ColliderA, ColliderB, Distance, Normal, PositionOnA, PositionOnB);
    }
}