using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// What <see cref="CharacterControllerComponent"/> already knows about its last collision
/// resolution, published so callers stop re-deriving it themselves (M2,
/// <c>docs/plan-moteur-character-motion.md</c> in the parent repository). A <c>readonly struct</c>
/// stored in place on the component - reading it never allocates, and it is replaced wholesale
/// (never mutated field-by-field) every time it changes.
/// </summary>
/// <remarks>
/// The report is two halves with DIFFERENT freshness, each documented on its members:
/// <list type="bullet">
/// <item><description>the DISPLACEMENT half (<see cref="RequestedUpAmount"/> through
/// <see cref="SweepHit"/>) is filled at the end of every resolution, whether it was driven by
/// <see cref="CharacterControllerComponent.Update"/> or by
/// <see cref="CharacterControllerComponent.Move"/> - and zeroed at the ENTRY of both, including
/// every early-return path, so a step with no displacement publishes zeros and no flags rather
/// than a previous step's state;</description></item>
/// <item><description>the GROUND half (<see cref="IsGrounded"/> through <see cref="GroundCollider"/>)
/// reflects the ground as resolved by the last <see cref="CharacterControllerComponent.Update"/>
/// call only - <see cref="CharacterControllerComponent.Move"/> never touches ground state, so a
/// <c>Move</c> call after a grounded <c>Update</c> still reports that <c>Update</c>'s
/// ground.</description></item>
/// </list>
/// Under the fixed-step mode added by M1 (<see cref="Framework.CharacterMotion.CharacterMotionSystem"/>),
/// a frame-rate reader of this property sees only the LAST fixed step's report - the ones in
/// between are overwritten, exactly like every other per-frame reader of this component.
/// </remarks>
public readonly struct CharacterControllerContactReport
{
    internal CharacterControllerContactReport(
        float requestedUpAmount,
        float requestedH1Amount,
        float requestedH2Amount,
        float actualUpAmount,
        float actualH1Amount,
        float actualH2Amount,
        bool h1Curtailed,
        bool h2Curtailed,
        bool sweepHit,
        bool isGrounded,
        Vector3 groundNormal,
        string groundSurfaceTag,
        PhysicsBaseComponent groundCollider)
    {
        RequestedUpAmount = requestedUpAmount;
        RequestedH1Amount = requestedH1Amount;
        RequestedH2Amount = requestedH2Amount;
        ActualUpAmount = actualUpAmount;
        ActualH1Amount = actualH1Amount;
        ActualH2Amount = actualH2Amount;
        H1Curtailed = h1Curtailed;
        H2Curtailed = h2Curtailed;
        SweepHit = sweepHit;
        IsGrounded = isGrounded;
        GroundNormal = groundNormal;
        GroundSurfaceTag = groundSurfaceTag;
        GroundCollider = groundCollider;
    }

    /// <summary>Requested displacement for the step, projected on the resolved up axis.</summary>
    public float RequestedUpAmount { get; }

    /// <summary>Requested displacement for the step, projected on the resolved first horizontal axis (h1).</summary>
    public float RequestedH1Amount { get; }

    /// <summary>Requested displacement for the step, projected on the resolved second horizontal axis (h2).</summary>
    public float RequestedH2Amount { get; }

    /// <summary>Actually-resolved displacement for the step, projected on the resolved up axis.</summary>
    public float ActualUpAmount { get; }

    /// <summary>Actually-resolved displacement for the step, projected on the resolved first horizontal axis (h1).</summary>
    public float ActualH1Amount { get; }

    /// <summary>Actually-resolved displacement for the step, projected on the resolved second horizontal axis (h2).</summary>
    public float ActualH2Amount { get; }

    /// <summary>
    /// True when the h1 axis was curtailed to zero by the FIELD-based horizontal resolution
    /// (<see cref="ICollisionField"/>) during the step. Authoritative only on that path: the
    /// physics-sweep path has no per-axis notion of "blocked" and always reports this as
    /// <c>false</c> - see <see cref="SweepHit"/> for that path's indicator instead.
    /// </summary>
    public bool H1Curtailed { get; }

    /// <summary>Same as <see cref="H1Curtailed"/>, for the h2 axis.</summary>
    public bool H2Curtailed { get; }

    /// <summary>
    /// True when the physics-sweep resolution hit something during the step (fed by the same fact
    /// as <see cref="CharacterControllerComponent.LastCollisionHit"/>). This is the sweep path's
    /// counterpart to <see cref="H1Curtailed"/>/<see cref="H2Curtailed"/>: the sweep loop has no
    /// per-axis semantics, so it publishes one "did it hit" indicator instead of two per-axis flags.
    /// </summary>
    public bool SweepHit { get; }

    /// <summary>Ground half: whether the controller was grounded, as of the last <c>Update</c>.</summary>
    public bool IsGrounded { get; }

    /// <summary>
    /// Ground half: ground normal, as of the last <c>Update</c>. Field path always reports the up
    /// axis (the height-grid field carries no slope normal); sweep path reports the hit normal.
    /// </summary>
    public Vector3 GroundNormal { get; }

    /// <summary>
    /// Ground half: surface tag of the ground, as of the last <c>Update</c>. Field path reports the
    /// tag of the footprint corner with the MAXIMUM ground height among the 4-corner probe (the
    /// same corner that determines the resolved ground height); sweep path has no tag concept and
    /// always reports <c>null</c>.
    /// </summary>
    public string GroundSurfaceTag { get; }

    /// <summary>
    /// Ground half: collider carrying the ground, as of the last <c>Update</c>. Field path always
    /// reports <c>null</c> (a field is not a collider); sweep path reports the hit's collider.
    /// </summary>
    public PhysicsBaseComponent GroundCollider { get; }

    /// <summary>
    /// Returns a copy with the displacement half zeroed and no flags set, keeping the ground half
    /// unchanged. Used at the entry of every <see cref="CharacterControllerComponent.Update"/> and
    /// <see cref="CharacterControllerComponent.Move"/> call.
    /// </summary>
    internal CharacterControllerContactReport WithDisplacementReset()
    {
        return new CharacterControllerContactReport(
            0f, 0f, 0f, 0f, 0f, 0f, false, false, false,
            IsGrounded, GroundNormal, GroundSurfaceTag, GroundCollider);
    }

    /// <summary>Returns a copy with the displacement half replaced, keeping the ground half unchanged.</summary>
    internal CharacterControllerContactReport WithDisplacement(
        float requestedUpAmount,
        float requestedH1Amount,
        float requestedH2Amount,
        float actualUpAmount,
        float actualH1Amount,
        float actualH2Amount,
        bool h1Curtailed,
        bool h2Curtailed,
        bool sweepHit)
    {
        return new CharacterControllerContactReport(
            requestedUpAmount, requestedH1Amount, requestedH2Amount,
            actualUpAmount, actualH1Amount, actualH2Amount,
            h1Curtailed, h2Curtailed, sweepHit,
            IsGrounded, GroundNormal, GroundSurfaceTag, GroundCollider);
    }

    /// <summary>Returns a copy with the ground half replaced, keeping the displacement half unchanged.</summary>
    internal CharacterControllerContactReport WithGround(bool isGrounded, Vector3 groundNormal, string groundSurfaceTag, PhysicsBaseComponent groundCollider)
    {
        return new CharacterControllerContactReport(
            RequestedUpAmount, RequestedH1Amount, RequestedH2Amount,
            ActualUpAmount, ActualH1Amount, ActualH2Amount,
            H1Curtailed, H2Curtailed, SweepHit,
            isGrounded, groundNormal, groundSurfaceTag, groundCollider);
    }
}
