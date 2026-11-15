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

    /// <summary>The skinning effect under the id of its lit-pbr techniques (<c>RiggedModelDrawPbr[DualQuaternion]</c>, ADR-0108).</summary>
    public static EffectiveShaderReference ResolvePbr(SkinningMode skinningMode)
    {
        return skinningMode switch
        {
            SkinningMode.LinearBlend => new EffectiveShaderReference(
                EffectiveShaderResolver.LinearBlendSkinnedPbrEffectShaderId,
                EffectiveShaderResolver.LinearBlendSkinnedEffectContentName),
            SkinningMode.DualQuaternion => new EffectiveShaderReference(
                EffectiveShaderResolver.DualQuaternionSkinnedPbrEffectShaderId,
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