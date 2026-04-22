using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public readonly record struct BoneTransform(Vector3 Translation, Quaternion Rotation, Vector3 Scale)
{
    public static BoneTransform Identity { get; } = new(Vector3.Zero, Quaternion.Identity, Vector3.One);

    public Matrix ToMatrix()
    {
        return Matrix.CreateScale(Scale)
               * Matrix.CreateFromQuaternion(GetNormalizedRotation(Rotation))
               * Matrix.CreateTranslation(Translation);
    }

    public static BoneTransform FromMatrix(Matrix matrix)
    {
        if (!matrix.Decompose(out var scale, out var rotation, out var translation))
        {
            return Identity;
        }

        return new BoneTransform(translation, GetNormalizedRotation(rotation), scale);
    }

    private static Quaternion GetNormalizedRotation(Quaternion rotation)
    {
        if (rotation.LengthSquared() <= float.Epsilon)
        {
            return Quaternion.Identity;
        }

        return Quaternion.Normalize(rotation);
    }
}