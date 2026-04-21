using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Models;

public interface ISkinnedMeshPoseProvider
{
    Matrix[] SkinningPalette { get; }

    Vector4[] DualQuaternionSkinningPalette { get; }

    bool CanUseDualQuaternionSkinning { get; }

    VertexBuffer? GetVertexBufferOverride(RiggedModel.RiggedModelMesh mesh, GraphicsDevice graphicsDevice, VertexDeclaration vertexDeclaration);

    Matrix GetMeshNodeTransform(RiggedModel.RiggedModelMesh mesh);
}