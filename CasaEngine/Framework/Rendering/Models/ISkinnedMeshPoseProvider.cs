using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.Models;

public interface ISkinnedMeshPoseProvider
{
    Matrix[] SkinningPalette { get; }

    Vector4[] DualQuaternionSkinningPalette { get; }

    bool CanUseDualQuaternionSkinning { get; }

    Matrix GetMeshNodeTransform(RiggedModel.RiggedModelMesh mesh);
}