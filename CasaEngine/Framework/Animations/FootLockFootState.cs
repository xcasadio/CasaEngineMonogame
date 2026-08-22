using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

/// <summary>Snapshot of one foot's lock state, as reported by <see cref="FootLockController.GetFootState"/>.</summary>
public readonly record struct FootLockFootState(
    bool IsLocked,
    float Weight,
    Vector3 LockedWorldPosition,
    Vector3 AnimatedWorldPosition,
    float SlideDistance);
