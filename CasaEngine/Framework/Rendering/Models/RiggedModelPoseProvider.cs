using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.Models;

public sealed class RiggedModelPoseProvider : ISkinnedMeshPoseProvider
{
    public RiggedModelPoseProvider(RiggedModel riggedModel)
    {
        RiggedModel = riggedModel ?? throw new ArgumentNullException(nameof(riggedModel));
    }

    public RiggedModel RiggedModel { get; }

    public Matrix[] SkinningPalette => RiggedModel.GlobalShaderMatrixs;

    public Matrix GetMeshNodeTransform(RiggedModel.RiggedModelMesh mesh)
    {
        return RiggedModel.GetMeshNodeTransform(mesh);
    }
}