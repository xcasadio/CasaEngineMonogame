using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Rendering.Shaders;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class SkinningModeShaderResolverTests
{
    [Fact]
    public void Resolve_ReturnsLinearBlendBuiltInShader()
    {
        var resolved = SkinningModeShaderResolver.Resolve(SkinningMode.LinearBlend);

        Assert.Equal(EffectiveShaderResolver.LinearBlendSkinnedEffectShaderId, resolved.ShaderId);
        Assert.Equal(EffectiveShaderResolver.LinearBlendSkinnedEffectContentName, resolved.ContentName);
        Assert.True(resolved.IsBuiltIn);
    }

    [Fact]
    public void ResolveVertexDeclaration_ReturnsLinearBlendVertexContract()
    {
        var declaration = SkinningModeShaderResolver.ResolveVertexDeclaration(SkinningMode.LinearBlend);

        Assert.Same(VertexPositionTextureNormalTangentWeights.VertexDeclaration, declaration);
    }
}