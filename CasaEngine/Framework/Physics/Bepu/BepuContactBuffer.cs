using System.Collections.Generic;
using System.Numerics;
using BepuPhysics.Collidables;

namespace CasaEngine.Framework.Physics.Bepu;

/// <summary>
/// One contact recorded by <see cref="BepuNarrowPhaseCallbacks"/> during a single Bepu <c>Timestep</c>.
/// </summary>
internal struct ContactRecord
{
    public CollidableReference A;
    public CollidableReference B;

    /// <summary>Compound child index on each side, or -1 when that side is not a compound.</summary>
    public int ChildA;
    public int ChildB;

    /// <summary>Offset from collidable A's position to the contact, in world space.</summary>
    public Vector3 Offset;

    /// <summary>Points from collidable B to collidable A.</summary>
    public Vector3 Normal;

    public float Depth;
}

/// <summary>
/// Contacts of the last Bepu step, pushed into by the narrow phase callbacks (mono-thread: always
/// worker 0) and read back by <see cref="BepuPhysicsEngine"/> after the step to rebuild collision
/// events. Cleared right before every sub-step so it always reflects only the most recent state,
/// mirroring what Bullet's manifold scan would have seen.
/// </summary>
internal sealed class BepuContactBuffer
{
    public readonly List<ContactRecord> Records = new();

    public void Clear() => Records.Clear();
}
