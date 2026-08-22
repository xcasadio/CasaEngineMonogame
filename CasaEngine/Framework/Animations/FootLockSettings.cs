namespace CasaEngine.Framework.Animations;

/// <summary>
/// Tuning parameters for <see cref="FootLockController"/>.
/// </summary>
public sealed record FootLockSettings
{
    /// <summary>Time, in seconds, over which the lock weight ramps from 0 to 1 after a contact rising edge.</summary>
    public float BlendInSeconds { get; init; } = 0.08f;

    /// <summary>Time, in seconds, over which the lock weight ramps from 1 to 0 after a contact falling edge (or a max-distance release).</summary>
    public float BlendOutSeconds { get; init; } = 0.12f;

    /// <summary>
    /// Maximum distance the animated ankle may drift away from the locked world position before
    /// the lock releases (with a blend-out, not an instant pop). Expressed in the units of the
    /// entity world matrix passed to <see cref="FootLockController.Update"/> (model units for an
    /// unscaled entity).
    /// </summary>
    public float MaxLockDistance { get; init; } = 60f;

    /// <summary>
    /// After a drift release (<see cref="MaxLockDistance"/> exceeded while the contact flag stayed
    /// true - typically a transition dragging a planted foot to the target clip's stance), the foot
    /// is pinned again as soon as its animated ankle moves slower than this in world space, in
    /// world units per second. 0 (default) never re-pins: the foot stays free until the next contact
    /// rising edge. A speed gate, not an immediate re-pin, so a foot that keeps moving under a wrong
    /// contact flag is not locked/released again and again.
    /// </summary>
    public float RelockMaxSpeed { get; init; }

    /// <summary>Distance, in model units, the IK pole is pushed away from the knee along the bend direction.</summary>
    public float PoleOffset { get; init; } = 50f;

    /// <summary>
    /// Model-space Y of the ankle joint when the foot rests flat on the ground (for a rig whose
    /// ankle sits above the sole, this is the ankle's resting height above the model's ground
    /// plane). Only used together with <see cref="MaxLockHeight"/>.
    /// </summary>
    public float GroundHeight { get; init; }

    /// <summary>
    /// Maximum height, in model units, of the <b>animated</b> ankle above <see cref="GroundHeight"/>
    /// at which a contact rising edge is allowed to engage the lock. A contact reported while the
    /// ankle is still higher than that (typically: the target clip's contact flags are read from its
    /// first frame while the blended pose still follows the source clip's swing) is kept pending
    /// and the lock engages, at the then-current position, as soon as the ankle comes down. The
    /// default (<see cref="float.PositiveInfinity"/>) disables the check.
    /// </summary>
    public float MaxLockHeight { get; init; } = float.PositiveInfinity;

    /// <summary>
    /// When false (default), only the X/Z of the locked position are enforced and the ankle Y
    /// keeps following the animated pose (useful for PSX clips whose feet bob slightly on contact).
    /// When true, the locked Y is enforced as well.
    /// </summary>
    public bool LockVertical { get; init; }

    /// <summary>Validates the settings, throwing <see cref="ArgumentOutOfRangeException"/> on an invalid value.</summary>
    public void Validate()
    {
        if (BlendInSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(BlendInSeconds), BlendInSeconds, "Blend-in duration cannot be negative.");
        }

        if (BlendOutSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(BlendOutSeconds), BlendOutSeconds, "Blend-out duration cannot be negative.");
        }

        if (MaxLockDistance <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxLockDistance), MaxLockDistance, "The maximum lock distance must be positive.");
        }

        if (PoleOffset < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(PoleOffset), PoleOffset, "The pole offset cannot be negative.");
        }

        if (float.IsNaN(RelockMaxSpeed) || RelockMaxSpeed < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(RelockMaxSpeed), RelockMaxSpeed, "The relock speed cannot be negative.");
        }

        if (float.IsNaN(GroundHeight))
        {
            throw new ArgumentOutOfRangeException(nameof(GroundHeight), GroundHeight, "The ground height must be a number.");
        }

        if (float.IsNaN(MaxLockHeight) || MaxLockHeight <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxLockHeight), MaxLockHeight, "The maximum lock height must be positive (or positive infinity to disable the check).");
        }
    }
}
