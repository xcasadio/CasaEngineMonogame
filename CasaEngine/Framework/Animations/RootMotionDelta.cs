using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public readonly record struct RootMotionDelta(Vector3 Translation, Quaternion Rotation)
{
    public static RootMotionDelta Identity { get; } = new(Vector3.Zero, Quaternion.Identity);
}