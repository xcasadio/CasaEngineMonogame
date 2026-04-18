using CasaEngine.Framework.Rendering.Models;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Shaders;

public static class SkinningModeShaderResolver
{
    public static EffectiveShaderReference Resolve(SkinningMode skinningMode)
    {
        return skinningMode switch
        {
            SkinningMode.LinearBlend => new EffectiveShaderReference(
                EffectiveShaderResolver.LinearBlendSkinnedEffectShaderId,
                EffectiveShaderResolver.LinearBlendSkinnedEffectContentName),
            SkinningMode.DualQuaternion => new EffectiveShaderReference(
                EffectiveShaderResolver.DualQuaternionSkinnedEffectShaderId,
                EffectiveShaderResolver.DualQuaternionSkinnedEffectContentName),
            _ => throw new ArgumentOutOfRangeException(nameof(skinningMode), skinningMode, null),
        };
    }

    public static VertexDeclaration ResolveVertexDeclaration(SkinningMode skinningMode)
    {
        return skinningMode switch
        {
            SkinningMode.LinearBlend => VertexPositionTextureNormalTangentWeights.VertexDeclaration,
            SkinningMode.DualQuaternion => VertexPositionTextureNormalTangentWeights.VertexDeclaration,
            _ => throw new ArgumentOutOfRangeException(nameof(skinningMode), skinningMode, null),
        };
    }
}