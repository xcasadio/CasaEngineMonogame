using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Physics;

public struct ContactPoint : IEquatable<ContactPoint>
{
    public PhysicsBaseComponent ColliderA;
    public PhysicsBaseComponent ColliderB;
    public float Distance;
    public Vector3 Normal;
    public Vector3 PositionOnA;
    public Vector3 PositionOnB;

    /// <summary>Tag of the fixture of <see cref="ColliderA"/> involved in this contact, when known.</summary>
    public string FixtureTagA;

    /// <summary>Tag of the fixture of <see cref="ColliderB"/> involved in this contact, when known.</summary>
    public string FixtureTagB;

    public bool Equals(ContactPoint other)
    {
        return ((ColliderA == other.ColliderA && ColliderB == other.ColliderB)
                || (ColliderA == other.ColliderB && ColliderB == other.ColliderA))
               && Distance == other.Distance
               && Normal == other.Normal
               && PositionOnA == other.PositionOnA
               && PositionOnB == other.PositionOnB
               && FixtureTagA == other.FixtureTagA
               && FixtureTagB == other.FixtureTagB;
    }

    public override bool Equals(object obj) => obj is ContactPoint other && Equals(other);

    public override int GetHashCode()
    {
        return HashCode.Combine(ColliderA, ColliderB, Distance, Normal, PositionOnA, PositionOnB);
    }
}