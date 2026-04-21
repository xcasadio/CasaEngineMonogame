using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Models;

public sealed class RiggedModelPoseProvider : ISkinnedMeshPoseProvider
{
    public RiggedModelPoseProvider(RiggedModel riggedModel)
    {
        RiggedModel = riggedModel ?? throw new ArgumentNullException(nameof(riggedModel));
    }

    public RiggedModel RiggedModel { get; }

    public Matrix[] SkinningPalette => RiggedModel.GlobalShaderMatrixs;

    public Vector4[] DualQuaternionSkinningPalette => RiggedModel.DualQuaternionSkinningPalette;

    public bool CanUseDualQuaternionSkinning => RiggedModel.CanUseDualQuaternionSkinning;

    public VertexBuffer? GetVertexBufferOverride(RiggedModel.RiggedModelMesh mesh, GraphicsDevice graphicsDevice, VertexDeclaration vertexDeclaration)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(vertexDeclaration);
        return null;
    }

    public Matrix GetMeshNodeTransform(RiggedModel.RiggedModelMesh mesh)
    {
        return RiggedModel.GetMeshNodeTransform(mesh);
    }
}