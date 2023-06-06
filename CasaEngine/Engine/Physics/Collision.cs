using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.Engine.Physics;

/// <summary>
/// A pair of component colliding with each other.
/// Pair of [b,a] is considered equal to [a,b].
/// </summary>
public readonly struct Collision : IEquatable<Collision>
{
    public readonly ICollideableComponent ColliderA;

    public readonly ICollideableComponent ColliderB;

    public Collision(ICollideableComponent a, ICollideableComponent b)
    {
        ColliderA = a;
        ColliderB = b;
    }

    public static bool operator ==(in Collision a, in Collision b)
    {
        return (Equals(a.ColliderA, b.ColliderA) && Equals(a.ColliderB, b.ColliderB))
               || (Equals(a.ColliderB, b.ColliderA) && Equals(a.ColliderA, b.ColliderB));
    }

    public static bool operator !=(in Collision a, in Collision b) => (a == b) == false;

    public override bool Equals(object obj)
    {
        return obj is Collision other && Equals(other);
    }

    public bool Equals(Collision other) => this == other;

    public override int GetHashCode()
    {
        int aH = ColliderA.GetHashCode();
        int bH = ColliderB.GetHashCode();
        // This ensures that a pair of components will return the same hash regardless
        // of if they are setup as [b,a] or [a,b]
        return aH > bH ? HashCode.Combine(aH, bH) : HashCode.Combine(bH, aH);
    }
}