using System.Numerics;

namespace CasaEngine.Framework.Physics.Bepu;

/// <summary>
/// Per-collidable filtering and integration data, indexed by handle (bodies and statics have
/// separate handle spaces) through a <c>CollidableProperty&lt;BepuCollidableData&gt;</c>.
/// </summary>
/// <remarks>
/// <see cref="Friction"/> is not part of the design's field list but is required to reproduce
/// Bullet's per-pair friction (product of both bodies' friction) inside <c>ConfigureContactManifold</c>;
/// it is a narrow, internal-only addition, not a change to any public surface.
/// </remarks>
internal struct BepuCollidableData
{
    public uint GroupBit;
    public uint BroadphaseMask;
    public bool IsSensor;
    public bool ApplyGravity;
    public float LinearDamping;
    public float AngularDamping;
    public Vector3 LinearFactor;
    public float Friction;
}
