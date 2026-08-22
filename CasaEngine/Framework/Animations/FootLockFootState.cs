using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

/// <summary>
/// Snapshot of one foot's lock state, as reported by <see cref="FootLockController.GetFootState"/>.
/// <see cref="SlideDistance"/> is the distance, on the axes the lock enforces (ground plane only
/// unless <see cref="FootLockSettings.LockVertical"/>), between the animated ankle and
/// <see cref="LockedWorldPosition"/> while the foot is locked; 0 while free.
/// </summary>
public readonly record struct FootLockFootState(
    bool IsLocked,
    float Weight,
    Vector3 LockedWorldPosition,
    Vector3 AnimatedWorldPosition,
    float SlideDistance)
{
    /// <summary>
    /// True while a locked foot is blending out (contact falling edge, drift past
    /// <see cref="FootLockSettings.MaxLockDistance"/>, or <see cref="FootLockController.Release"/>).
    /// <see cref="SlideDistance"/> keeps being reported during that blend-out, but it then measures
    /// the swing leaving the pin rather than the stance slide: stance-quality metrics should only
    /// sample it while <see cref="IsLocked"/> is true and this is false.
    /// </summary>
    public bool IsReleasing { get; init; }
}
