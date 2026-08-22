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
    /// Maximum distance, in model units, the animated ankle may drift away from the locked world
    /// position before the lock releases (with a blend-out, not an instant pop).
    /// </summary>
    public float MaxLockDistance { get; init; } = 60f;

    /// <summary>Distance, in model units, the IK pole is pushed away from the knee along the bend direction.</summary>
    public float PoleOffset { get; init; } = 50f;

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
    }
}
