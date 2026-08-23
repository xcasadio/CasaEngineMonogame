using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics;

/// <summary>
/// Ground found under a sample position by an <see cref="ICollisionField"/>.
/// </summary>
/// <remarks>
/// Axis contract: up is the elevation axis declared by the field, which must match the world's
/// <c>SimulationSpacePolicy</c>; <see cref="GroundHeight"/> is measured along that axis, and the
/// other two axes are horizontal.
/// </remarks>
public readonly struct GroundSample
{
    public GroundSample(float groundHeight, Vector3 normal, bool isWalkable, string surfaceTag)
    {
        HasGround = true;
        GroundHeight = groundHeight;
        Normal = normal;
        IsWalkable = isWalkable;
        SurfaceTag = surfaceTag;
    }

    /// <summary>True when the field found ground for the sample position.</summary>
    public bool HasGround { get; }

    /// <summary>
    /// World coordinate of the ground surface along the field's declared up axis. Only meaningful
    /// when <see cref="HasGround"/> is true.
    /// </summary>
    public float GroundHeight { get; }

    /// <summary>Up direction of the ground surface, in world space.</summary>
    public Vector3 Normal { get; }

    /// <summary>
    /// Whether the ground may be walked on. Presence and walkability are separate facts: a field
    /// reports ground it found even when that ground is not walkable.
    /// </summary>
    public bool IsWalkable { get; }

    /// <summary>Free surface identifier of the ground, or null when the field carries none.</summary>
    public string SurfaceTag { get; }
}

/// <summary>
/// Dense environment data — ground height, walkability, surface normal and surface tag — sampled
/// analytically instead of being expressed as collision geometry. A field is the second collider
/// family of the collision architecture, next to the volumes the broadphase holds: a terrain is a
/// field, never thousands of static bodies.
/// </summary>
/// <remarks>
/// Contract of every implementation:
/// <list type="bullet">
/// <item><description>sampling is O(1) and allocation-free — it may be called several times per entity per frame;</description></item>
/// <item><description>a field never creates a body and is never inserted into the physics world;</description></item>
/// <item><description>the axis contract is the field's declared up axis, matching the world's
/// simulation space policy (see <see cref="GroundSample"/>);</description></item>
/// <item><description>the CALLER chooses the sample position and owns any foot or center offset —
/// a field knows nothing about the shape probing it. That convention is exactly where the
/// integration with the character mover lives, and it is deferred to that chantier.</description></item>
/// </list>
/// </remarks>
public interface ICollisionField
{
    /// <summary>
    /// Samples the ground under <paramref name="worldPosition"/>.
    /// </summary>
    /// <param name="worldPosition">Position to sample from, in world space.</param>
    /// <param name="maxDropDistance">
    /// How far below the sample position ground is still accepted. Ground exactly at the sample
    /// position counts as found; ground strictly above it does not.
    /// </param>
    /// <param name="sample">Ground found, or a default sample when none was.</param>
    /// <returns>True when ground was found; equals <see cref="GroundSample.HasGround"/>.</returns>
    bool TrySampleGround(in Vector3 worldPosition, float maxDropDistance, out GroundSample sample);

    /// <summary>
    /// Samples the ground under <paramref name="worldPosition"/>, additionally filtering it against
    /// a walkability mask. The mask is opaque to the engine — it is defined and interpreted entirely
    /// by the caller and the field. Fields that support per-class walkability override this method;
    /// the default implementation ignores <paramref name="walkabilityMask"/> and forwards to
    /// <see cref="TrySampleGround(in Vector3, float, out GroundSample)"/>.
    /// </summary>
    /// <param name="worldPosition">Position to sample from, in world space.</param>
    /// <param name="maxDropDistance">
    /// How far below the sample position ground is still accepted. Ground exactly at the sample
    /// position counts as found; ground strictly above it does not.
    /// </param>
    /// <param name="walkabilityMask">Opaque walkability mask, meaningful only to fields that support it.</param>
    /// <param name="sample">Ground found, or a default sample when none was.</param>
    /// <returns>True when ground was found; equals <see cref="GroundSample.HasGround"/>.</returns>
    bool TrySampleGround(in Vector3 worldPosition, float maxDropDistance, uint walkabilityMask, out GroundSample sample)
        => TrySampleGround(worldPosition, maxDropDistance, out sample);
}
