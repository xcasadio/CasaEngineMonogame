using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The PBR techniques of the compiled skinning effect on a real device (ADR-0110): they exist, the resolver and the shader selector
/// mounted as <c>SkinnedMeshRendererComponent.LoadContent</c> mounts them leave the right technique current for a lit-pbr material in
/// both skinning modes (and the Blinn-Phong one for a lit-diffuse material), and the mesh texture is bound for a lit-pbr material
/// that carries none. Skipped where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class SkinnedPbrShaderGpuTests
{
    [GpuFact]
    public void CompiledEffect_DeclaresThePbrTechniquesAndTheirConstants()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.SkinEffectContentName);

            Assert.NotNull(effect.Techniques["RiggedModelDrawPbr"]);
            Assert.NotNull(effect.Techniques["RiggedModelDrawPbrDualQuaternion"]);
            Assert.NotNull(effect.Techniques["RiggedModelDraw"]);
            Assert.NotNull(effect.Techniques["RiggedModelDrawDualQuaternion"]);

            // The compiler drops an unused constant: these four survive because the PBR pixel shader reads them.
            Assert.NotNull(effect.Parameters[ShaderParameterNames.BaseColor]);
            Assert.NotNull(effect.Parameters[ShaderParameterNames.Metallic]);
            Assert.NotNull(effect.Parameters[ShaderParameterNames.Roughness]);
            Assert.NotNull(effect.Parameters[ShaderParameterNames.HasBaseColorTexture]);

            // No image-based lighting in the skinning effect (P9): its IBL constants and textures do not exist.
            Assert.Null(effect.Parameters["HasIbl"]);
            Assert.Null(effect.Parameters["IrradianceCubeTexture"]);

            // No clear coat either (plan decision P5, ADR-0114): Pbr.fxh compiles its clear-coat code only under PBR_CLEAR_COAT, which
            // skinEffect.fx never defines, so the constants do not exist.
            Assert.Null(effect.Parameters[ShaderParameterNames.ClearCoat]);
            Assert.Null(effect.Parameters[ShaderParameterNames.ClearCoatRoughness]);
        });
    }

    [GpuFact]
    public void ResolverAndSelector_LeaveThePbrTechniqueCurrentForALitPbrMaterial_AndTheBlinnPhongOneForALitDiffuseMaterial()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.SkinEffectContentName);
            var wrapper = new ShaderWrapper(effect);
            var selector = MountLikeTheRenderer(wrapper);

            var modes = new (SkinningMode Mode, string Pbr, string BlinnPhong)[]
            {
                (SkinningMode.LinearBlend, "RiggedModelDrawPbr", "RiggedModelDraw"),
                (SkinningMode.DualQuaternion, "RiggedModelDrawPbrDualQuaternion", "RiggedModelDrawDualQuaternion"),
            };

            foreach (var (skinningMode, expectedPbrTechnique, expectedBlinnPhongTechnique) in modes)
            foreach (bool withTexture in new[] { false, true })
            {
                var pbr = new LitPbrMaterial();
                var pbrFeatures = ShaderFeature.Skinned | (withTexture ? ShaderFeature.BasColorTexture : ShaderFeature.None);
                var pbrShader = EffectiveShaderResolver.Resolve(pbr, pbrFeatures, skinningMode);
                var resolvedPbr = selector.Resolve(pbrShader.ShaderId, pbrFeatures);

                Assert.Same(wrapper, resolvedPbr.Shader);
                Assert.True(resolvedPbr.TechniqueSelectedBySelector);
                Assert.Equal(expectedPbrTechnique, effect.CurrentTechnique.Name);

                var diffuse = new LitDiffuseMaterial();
                var diffuseShader = EffectiveShaderResolver.Resolve(diffuse, pbrFeatures, skinningMode);
                var resolvedDiffuse = selector.Resolve(diffuseShader.ShaderId, pbrFeatures);

                Assert.Same(wrapper, resolvedDiffuse.Shader);
                Assert.True(resolvedDiffuse.TechniqueSelectedBySelector);
                Assert.Equal(expectedBlinnPhongTechnique, effect.CurrentTechnique.Name);
            }
        });
    }

    [GpuFact]
    public void BindPbrMaterial_BindsTheMeshTextureWhenTheMaterialHasNone_AndGivesTheMaterialBackUntouched()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.SkinEffectContentName);
            var shader = new ShaderWrapper(effect);
            var context = new RenderContext { Device = device };

            using var meshTexture = new Texture2D(device, 1, 1);
            using var ownTexture = new Texture2D(device, 1, 1);

            var withoutTexture = new LitPbrMaterial { BaseColor = new Color(10, 20, 30, 255), Metallic = 0.25f, Roughness = 0.75f };
            SkinnedMeshRendererComponent.BindPbrMaterial(withoutTexture, shader, in context, Matrix.Identity, meshTexture);

            Assert.Equal(1.0f, effect.Parameters[ShaderParameterNames.HasBaseColorTexture]!.GetValueSingle());
            Assert.Same(meshTexture, effect.Parameters[ShaderParameterNames.BasColorTexture]!.GetValueTexture2D());
            Assert.Equal(0.25f, effect.Parameters[ShaderParameterNames.Metallic]!.GetValueSingle());
            Assert.Equal(0.75f, effect.Parameters[ShaderParameterNames.Roughness]!.GetValueSingle());
            Assert.Equal(new Vector4(10f / 255f, 20f / 255f, 30f / 255f, 1.0f), effect.Parameters[ShaderParameterNames.BaseColor]!.GetValueVector4());
            Assert.Null(withoutTexture.BaseColorTexture);

            // A material with its own texture keeps it: the mesh texture is not bound in its place.
            var withTexture = new LitPbrMaterial { BaseColorTexture = ownTexture };
            SkinnedMeshRendererComponent.BindPbrMaterial(withTexture, shader, in context, Matrix.Identity, meshTexture);

            Assert.Same(ownTexture, effect.Parameters[ShaderParameterNames.BasColorTexture]!.GetValueTexture2D());
            Assert.Same(ownTexture, withTexture.BaseColorTexture);

            // A second bind of the first material starts from a clean state: still no texture of its own afterwards.
            SkinnedMeshRendererComponent.BindPbrMaterial(withoutTexture, shader, in context, Matrix.Identity, meshTexture);
            Assert.Null(withoutTexture.BaseColorTexture);
        });
    }

    // The registrations of SkinnedMeshRendererComponent.LoadContent: one effect under the four ids, each with its alias table.
    private static RenderShaderSelector MountLikeTheRenderer(ShaderWrapper wrapper)
    {
        var shaderManager = new ShaderManager(new AssetContentManager());
        var library = new ShaderVariantLibrary(shaderManager);

        var linear = SkinningModeShaderResolver.Resolve(SkinningMode.LinearBlend);
        var dualQuaternion = SkinningModeShaderResolver.Resolve(SkinningMode.DualQuaternion);
        var linearPbr = SkinningModeShaderResolver.ResolvePbr(SkinningMode.LinearBlend);
        var dualQuaternionPbr = SkinningModeShaderResolver.ResolvePbr(SkinningMode.DualQuaternion);

        shaderManager.RegisterShader(linear.ShaderId, wrapper);
        library.RegisterTechniqueAliases(linear.ShaderId, ShaderVariantLibrary.BuildSkinnedEffectAliases());
        shaderManager.RegisterShader(dualQuaternion.ShaderId, wrapper);
        library.RegisterTechniqueAliases(dualQuaternion.ShaderId, ShaderVariantLibrary.BuildDualQuaternionSkinnedEffectAliases());
        shaderManager.RegisterShader(linearPbr.ShaderId, wrapper);
        library.RegisterTechniqueAliases(linearPbr.ShaderId, ShaderVariantLibrary.BuildSkinnedPbrEffectAliases());
        shaderManager.RegisterShader(dualQuaternionPbr.ShaderId, wrapper);
        library.RegisterTechniqueAliases(dualQuaternionPbr.ShaderId, ShaderVariantLibrary.BuildDualQuaternionSkinnedPbrEffectAliases());

        var selector = new RenderShaderSelector(wrapper, shaderManager, library);
        selector.RegisterShader(linear.ShaderId, wrapper);
        selector.RegisterShader(dualQuaternion.ShaderId, wrapper);
        selector.RegisterShader(linearPbr.ShaderId, wrapper);
        selector.RegisterShader(dualQuaternionPbr.ShaderId, wrapper);
        return selector;
    }
}
