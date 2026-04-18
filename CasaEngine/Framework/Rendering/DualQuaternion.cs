using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering;

public readonly struct DualQuaternion
{
    private const float RigidTransformTolerance = 0.01f;
    private const float MinimumAxisLength = 1e-5f;

    public static DualQuaternion Identity { get; } = new(Quaternion.Identity, new Quaternion(0f, 0f, 0f, 0f));

    public DualQuaternion(Quaternion real, Quaternion dual)
    {
        Real = Normalize(real);
        Dual = dual;
    }

    public Quaternion Real { get; }

    public Quaternion Dual { get; }

    public Matrix ToMatrix()
    {
        var rotation = Normalize(Real);
        var translationQuaternion = Quaternion.Multiply(Dual, Quaternion.Conjugate(rotation));
        translationQuaternion.X *= 2f;
        translationQuaternion.Y *= 2f;
        translationQuaternion.Z *= 2f;
        translationQuaternion.W *= 2f;

        var matrix = Matrix.CreateFromQuaternion(rotation);
        matrix.Translation = new Vector3(translationQuaternion.X, translationQuaternion.Y, translationQuaternion.Z);
        return matrix;
    }

    public void WriteTo(Vector4[] destination, int paletteIndex)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var baseIndex = paletteIndex * 2;
        if ((uint)(baseIndex + 1) >= (uint)destination.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(paletteIndex));
        }

        destination[baseIndex] = new Vector4(Real.X, Real.Y, Real.Z, Real.W);
        destination[baseIndex + 1] = new Vector4(Dual.X, Dual.Y, Dual.Z, Dual.W);
    }

    public static bool TryWriteSkinningPalette(Matrix[] sourcePalette, Vector4[] destinationPalette)
    {
        ArgumentNullException.ThrowIfNull(sourcePalette);
        ArgumentNullException.ThrowIfNull(destinationPalette);

        if (destinationPalette.Length < sourcePalette.Length * 2)
        {
            throw new ArgumentException("The dual quaternion palette is too small for the source matrix palette.", nameof(destinationPalette));
        }

        var canUseDualQuaternionSkinning = true;
        for (var paletteIndex = 0; paletteIndex < sourcePalette.Length; paletteIndex++)
        {
            canUseDualQuaternionSkinning &= TryCreate(sourcePalette[paletteIndex], out var dualQuaternion);
            dualQuaternion.WriteTo(destinationPalette, paletteIndex);
        }

        return canUseDualQuaternionSkinning;
    }

    public static bool TryCreate(Matrix transform, out DualQuaternion dualQuaternion)
    {
        var canUseDualQuaternionSkinning = TryExtractRigidTransform(transform, out var rotation, out var translation);
        var real = Normalize(rotation);
        var translationQuaternion = new Quaternion(translation, 0f);
        var dual = Quaternion.Multiply(translationQuaternion, real);
        dual.X *= 0.5f;
        dual.Y *= 0.5f;
        dual.Z *= 0.5f;
        dual.W *= 0.5f;
        dualQuaternion = new DualQuaternion(real, dual);
        return canUseDualQuaternionSkinning;
    }

    private static bool TryExtractRigidTransform(Matrix transform, out Quaternion rotation, out Vector3 translation)
    {
        translation = transform.Translation;

        var right = transform.Right;
        var up = transform.Up;
        var backward = transform.Backward;
        var scaleX = right.Length();
        var scaleY = up.Length();
        var scaleZ = backward.Length();

        if (scaleX <= MinimumAxisLength || scaleY <= MinimumAxisLength || scaleZ <= MinimumAxisLength)
        {
            rotation = Quaternion.Identity;
            return false;
        }

        var rigidScale =
            NearlyEqual(scaleX, 1f) &&
            NearlyEqual(scaleY, 1f) &&
            NearlyEqual(scaleZ, 1f);

        var rotationMatrix = transform;
        rotationMatrix.Right = right / scaleX;
        rotationMatrix.Up = up / scaleY;
        rotationMatrix.Backward = backward / scaleZ;
        rotationMatrix.Translation = Vector3.Zero;

        if (rotationMatrix.Determinant() <= 0f)
        {
            rotation = Quaternion.Identity;
            return false;
        }

        rotation = Normalize(Quaternion.CreateFromRotationMatrix(rotationMatrix));
        return rigidScale;
    }

    private static Quaternion Normalize(Quaternion quaternion)
    {
        if (quaternion.LengthSquared() <= float.Epsilon)
        {
            return Quaternion.Identity;
        }

        return Quaternion.Normalize(quaternion);
    }

    private static bool NearlyEqual(float left, float right)
    {
        return MathF.Abs(left - right) <= RigidTransformTolerance;
    }
}