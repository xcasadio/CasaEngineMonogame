using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public readonly record struct LookAtConstraint(
    int JointIndex,
    Vector3 TargetPosition,
    Vector3 LocalForwardAxis,
    Vector3 LocalUpAxis,
    Vector3 WorldUp,
    float Weight = 1f,
    bool Enabled = true)
{
    public static LookAtConstraint CreateDefault(int jointIndex, Vector3 targetPosition)
    {
        return new LookAtConstraint(
            jointIndex,
            targetPosition,
            Vector3.UnitZ,
            Vector3.UnitY,
            Vector3.UnitY);
    }
}