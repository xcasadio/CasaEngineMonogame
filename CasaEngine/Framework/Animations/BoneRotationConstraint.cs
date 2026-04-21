using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public readonly record struct BoneRotationConstraint(
    int JointIndex,
    Quaternion TargetLocalRotation,
    float Weight = 1f,
    bool Enabled = true);