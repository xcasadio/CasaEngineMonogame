using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.Models;

public interface ISkinnedMeshPoseProvider
{
    Matrix[] SkinningPalette { get; }

    Matrix GetMeshNodeTransform(RiggedModel.RiggedModelMesh mesh);
}