using System.Collections.Generic;
using System.Numerics;

namespace CasaEngine.Framework.Physics.Bepu;

/// <summary>
/// One contact recorded by <see cref="BepuNarrowPhaseCallbacks"/> during a single Bepu <c>Timestep</c>.
/// Fully self-contained: everything a consumer needs is snapshotted while the manifold is reported (the
/// pair's collidables and positions are guaranteed alive at that moment), so nothing here ever needs to
/// dereference the simulation again. That matters because gameplay code invoked later in the same frame
/// (<c>OnHit</c>, via <see cref="BepuPhysicsEngine.SendEvents"/>) may remove and dispose either side of a
/// pair before the debug renderer or <see cref="BepuPhysicsEngine.LatestContactPointsFor"/> read these
/// records back: a live <c>CollidableReference</c> would then index a freed (or reused) simulation handle.
/// </summary>
internal struct ContactRecord
{
    /// <summary>Backend of collidable A, resolved through the engine's managed handle tables at the
    /// moment the manifold was reported. Never re-resolved from a handle afterwards.</summary>
    public BepuBodyBackend BackendA;

    /// <summary>Backend of collidable B, resolved the same way as <see cref="BackendA"/>.</summary>
    public BepuBodyBackend BackendB;

    /// <summary>Compound child index on each side, or -1 when that side is not a compound.</summary>
    public int ChildA;
    public int ChildB;

    /// <summary>World position of collidable A's pose when the manifold was reported.</summary>
    public Vector3 PositionA;

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
