using CasaEngine.Framework.Rendering.Shaders;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class ShaderVariantLibraryTests
{
    private sealed class FakeShader
    {
        public string? CurrentTechnique { get; set; }
    }

    [Fact]
    public void GetOrResolve_ReappliesSelection_WhenCacheHitReusesSharedShader()
    {
        var sharedShader = new FakeShader();
        var resolved = new Dictionary<ShaderVariantKey, FakeShader?>();
        var baseShaderId = Guid.NewGuid();
        var opaqueKey = new ShaderVariantKey(baseShaderId, ShaderFeature.None);
        var texturedKey = new ShaderVariantKey(baseShaderId, ShaderFeature.BasColorTexture);

        static void ApplySelection(FakeShader shader, ShaderVariantKey key)
            => shader.CurrentTechnique = ShaderVariantLibrary.BuildTechniqueName(key.Features);

        FakeShader ResolveSharedShader(ShaderVariantKey _) => sharedShader;

        var first = ShaderVariantLibrary.GetOrResolve(opaqueKey, resolved, ResolveSharedShader, ApplySelection);
        Assert.Same(sharedShader, first);
        Assert.Equal("Opaque", sharedShader.CurrentTechnique);

        var second = ShaderVariantLibrary.GetOrResolve(texturedKey, resolved, ResolveSharedShader, ApplySelection);
        Assert.Same(sharedShader, second);
        Assert.Equal("Opaque_Textured", sharedShader.CurrentTechnique);

        var third = ShaderVariantLibrary.GetOrResolve(opaqueKey, resolved, ResolveSharedShader, ApplySelection);
        Assert.Same(sharedShader, third);
        Assert.Equal("Opaque", sharedShader.CurrentTechnique);
    }
}