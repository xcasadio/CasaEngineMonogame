using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public readonly record struct TwoBoneIkConstraint(
    int RootJointIndex,
    int MidJointIndex,
    int EndJointIndex,
    Vector3 TargetPosition,
    Vector3 PolePosition,
    float Weight = 1f,
    bool Enabled = true);