using BepuPhysics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Physics.Bepu;

/// <summary>
/// <see cref="Matrix"/> (no-scale world matrix) &lt;-&gt; Bepu <see cref="RigidPose"/> conversions.
/// The engine only ever passes scale-free matrices here (<c>WorldMatrixNoScale</c>), so this never
/// needs <see cref="Matrix.Decompose"/>.
/// </summary>
internal static class BepuConversions
{
    public static RigidPose ToRigidPose(this Matrix matrix)
    {
        var orientation = Quaternion.CreateFromRotationMatrix(matrix);
        return new RigidPose(matrix.Translation.ToNumerics(), orientation.ToNumerics());
    }

    public static Matrix ToMatrix(this RigidPose pose)
    {
        Quaternion orientation = pose.Orientation;
        var matrix = Matrix.CreateFromQuaternion(orientation);
        matrix.Translation = pose.Position;
        return matrix;
    }
}
