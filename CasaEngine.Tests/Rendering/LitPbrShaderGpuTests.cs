using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The compiled LitPbr effect on a real device: it loads from the build output, declares every technique the alias table and the
/// material can ask for, and the shader selector plus the variant library leave the technique the alias table names as the
/// current one. Skipped where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class LitPbrShaderGpuTests
{
    private static readonly string[] ExpectedTechniques =
    {
        "LitPbr",
        "LitPbr_AlphaTest",
        "LitPbr_NormalMap",
        "LitPbr_NormalMap_AlphaTest",
        "LitPbr_Transparent",
        "LitPbr_NormalMap_Transparent",
    };

    [GpuFact]
    public void CompiledEffect_LoadsWithEveryTechniqueTheAliasTableAndTheMaterialNeed()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitPbrContentName);

            foreach (string technique in ExpectedTechniques)
            {
                Assert.NotNull(effect.Techniques[technique]);
            }

            foreach (string technique in ShaderVariantLibrary.BuildLitPbrAliases().Values)
            {
                Assert.NotNull(effect.Techniques[technique]);
            }

            // The constants the PBR material binds survive the compilation (an unused constant is dropped by the compiler).
            Assert.NotNull(effect.Parameters[ShaderParameterNames.BaseColor]);
            Assert.NotNull(effect.Parameters[ShaderParameterNames.Metallic]);
            Assert.NotNull(effect.Parameters[ShaderParameterNames.Roughness]);
            Assert.NotNull(effect.Parameters[ShaderParameterNames.EmissiveColor]);
            Assert.NotNull(effect.Parameters[ShaderParameterNames.AlphaCutoff]);
            Assert.NotNull(effect.Parameters[ShaderParameterNames.ColorPipelineIsLinear]);
            Assert.NotNull(effect.Parameters[ShaderParameterNames.ActiveDirectionalLightCount]);
        });
    }

    [GpuFact]
    public void SelectorAndVariantLibrary_LeaveTheAlphaTestTechniqueCurrent_ForAnAlphaTestedTexturedItem()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitPbrContentName);
            var wrapper = new ShaderWrapper(effect);

            using var shaderManager = new ShaderManager(new AssetContentManager());
            shaderManager.RegisterShader(EffectiveShaderResolver.LitPbrShaderId, wrapper);
            var library = new ShaderVariantLibrary(shaderManager);
            library.RegisterTechniqueAliases(EffectiveShaderResolver.LitPbrShaderId, ShaderVariantLibrary.BuildLitPbrAliases());
            var selector = new RenderShaderSelector(wrapper, shaderManager, library);
            selector.RegisterShader(EffectiveShaderResolver.LitPbrShaderId, wrapper);

            var alphaTest = selector.Resolve(EffectiveShaderResolver.LitPbrShaderId, ShaderFeature.AlphaTest | ShaderFeature.BasColorTexture);
            Assert.Same(wrapper, alphaTest.Shader);
            Assert.True(alphaTest.TechniqueSelectedBySelector);
            Assert.Equal("LitPbr_AlphaTest", alphaTest.Shader.Effect.CurrentTechnique.Name);

            var opaque = selector.Resolve(EffectiveShaderResolver.LitPbrShaderId, ShaderFeature.BasColorTexture);
            Assert.Equal("LitPbr", opaque.Shader.Effect.CurrentTechnique.Name);

            var transparent = selector.Resolve(EffectiveShaderResolver.LitPbrShaderId, ShaderFeature.Transparent | ShaderFeature.BasColorTexture);
            Assert.Equal("LitPbr", transparent.Shader.Effect.CurrentTechnique.Name);

            var vertexColor = selector.Resolve(EffectiveShaderResolver.LitPbrShaderId, ShaderFeature.VertexColor);
            Assert.Equal("LitPbr", vertexColor.Shader.Effect.CurrentTechnique.Name);
        });
    }
}
