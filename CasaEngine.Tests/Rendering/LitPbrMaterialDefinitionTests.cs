using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>The lit-pbr built-in definition, its compilation to a <see cref="LitPbrMaterial"/> and its per-instance overrides.</summary>
public class LitPbrMaterialDefinitionTests
{
    private static readonly string[] ExpectedKeys =
    {
        "base_color_texture",
        "normal_texture",
        "metallic_roughness_texture",
        "occlusion_texture",
        "emissive_texture",
        "base_color",
        "metallic",
        "roughness",
        "emissive_color",
        "occlusion_strength",
        "normal_scale",
        "alpha_cutoff",
    };

    [Fact]
    public void Registry_ExposesTheThreeBuiltInDefinitions()
    {
        Assert.True(MaterialDefinitionRegistry.TryGetById("lit-diffuse", out var litDiffuse));
        Assert.Equal(typeof(LitDiffuseMaterial), litDiffuse.RuntimeMaterialType);
        Assert.True(MaterialDefinitionRegistry.TryGetById("unlit-texture", out var unlit));
        Assert.Equal(typeof(UnlitTextureMaterial), unlit.RuntimeMaterialType);
        Assert.True(MaterialDefinitionRegistry.TryGetById("lit-pbr", out var litPbr));
        Assert.Equal(typeof(LitPbrMaterial), litPbr.RuntimeMaterialType);
    }

    [Fact]
    public void Definition_DeclaresExactlyTheExpectedKeys_WithoutAnyAliasThatCollides()
    {
        var definition = MaterialDefinitionRegistry.GetRequiredById("lit-pbr");

        var keys = new List<string>();
        for (int i = 0; i < definition.Properties.Count; i++)
        {
            keys.Add(definition.Properties[i].Key);
            Assert.Empty(definition.Properties[i].LegacyAliases);
        }

        Assert.Equal(ExpectedKeys.OrderBy(key => key, StringComparer.Ordinal), keys.OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(keys.Count, new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase).Count);
    }

    [Fact]
    public void Definition_ExposesTheExpectedMetadataAndDefaultsOfTheRuntimeMaterial()
    {
        var definition = MaterialDefinitionRegistry.GetRequiredById("lit-pbr");
        var defaults = new LitPbrMaterial();

        var baseColor = definition.GetRequiredProperty("base_color");
        Assert.Equal(MaterialPropertyType.Color, baseColor.ValueType);
        Assert.Equal(MaterialPropertyGroup.Surface, baseColor.Group);
        Assert.True((baseColor.Flags & MaterialPropertyFlags.AffectsTransparency) != 0);
        Assert.Equal(defaults.BaseColor, baseColor.GetDefaultValue<Color>());

        var metallic = definition.GetRequiredProperty("metallic");
        Assert.Equal(MaterialPropertyType.Float, metallic.ValueType);
        Assert.Equal(defaults.Metallic, metallic.GetDefaultValue<float>());
        Assert.Equal(0.0f, metallic.MinValue);
        Assert.Equal(1.0f, metallic.MaxValue);
        Assert.Equal(0.01f, metallic.Step);

        var roughness = definition.GetRequiredProperty("roughness");
        Assert.Equal(defaults.Roughness, roughness.GetDefaultValue<float>());
        Assert.Equal(0.0f, roughness.MinValue);
        Assert.Equal(1.0f, roughness.MaxValue);

        var emissive = definition.GetRequiredProperty("emissive_color");
        Assert.Equal(MaterialPropertyType.Vector3, emissive.ValueType);
        Assert.Equal(defaults.EmissiveColor, emissive.GetDefaultValue<Vector3>());
        Assert.Equal("ColorPicker", emissive.EditorControlHint);

        Assert.Equal(defaults.OcclusionStrength, definition.GetRequiredProperty("occlusion_strength").GetDefaultValue<float>());
        Assert.Equal(defaults.NormalScale, definition.GetRequiredProperty("normal_scale").GetDefaultValue<float>());
        Assert.Equal(0.5f, definition.GetRequiredProperty("alpha_cutoff").GetDefaultValue<float>());

        foreach (var key in new[] { "base_color_texture", "normal_texture", "metallic_roughness_texture", "occlusion_texture", "emissive_texture" })
        {
            var texture = definition.GetRequiredProperty(key);
            Assert.Equal(MaterialPropertyType.Texture, texture.ValueType);
            Assert.Equal(MaterialPropertyGroup.Textures, texture.Group);
            Assert.Equal("texture", texture.AssetKind);
            Assert.Equal(Guid.Empty, texture.GetDefaultValue<Guid>());
        }
    }

    [Fact]
    public void MaterialFile_RoundTripsThroughTheAuthoringSerializer()
    {
        var materialAsset = new MaterialAsset("lit-pbr") { Name = "Pbr Material", Queue = RenderQueue.AlphaTest };
        var baseTextureId = Guid.NewGuid();
        var metallicRoughnessId = Guid.NewGuid();
        materialAsset.SetPropertyValue("base_color_texture", MaterialValue.FromTextureId(baseTextureId));
        materialAsset.SetPropertyValue("metallic_roughness_texture", MaterialValue.FromTextureId(metallicRoughnessId));
        materialAsset.SetPropertyValue("base_color", MaterialValue.FromColor(new Color(200, 100, 50, 255)));
        materialAsset.SetPropertyValue("metallic", MaterialValue.FromFloat(0.8f));
        materialAsset.SetPropertyValue("roughness", MaterialValue.FromFloat(0.2f));
        materialAsset.SetPropertyValue("emissive_color", MaterialValue.FromVector3(new Vector3(0.1f, 0.2f, 0.3f)));
        materialAsset.SetPropertyValue("occlusion_strength", MaterialValue.FromFloat(0.6f));
        materialAsset.SetPropertyValue("normal_scale", MaterialValue.FromFloat(1.5f));
        materialAsset.SetPropertyValue("alpha_cutoff", MaterialValue.FromFloat(0.3f));

        var document = new JObject();
        MaterialAssetJsonSerializer.Save(materialAsset, document);

        var propertiesNode = Assert.IsType<JObject>(document["properties"]);
        Assert.Equal(9, propertiesNode.Count);
        Assert.Null(propertiesNode["normal_texture"]);
        Assert.Equal("lit-pbr", document["definition_id"]!.Value<string>());

        var loaded = new MaterialAsset();
        loaded.Load(document);

        Assert.Equal("lit-pbr", loaded.DefinitionId);
        Assert.Equal(RenderQueue.AlphaTest, loaded.Queue);
        Assert.True(loaded.GetPropertyValueOrDefault("base_color_texture")!.TryGetTextureId(out var loadedBaseTexture));
        Assert.Equal(baseTextureId, loadedBaseTexture);
        Assert.True(loaded.GetPropertyValueOrDefault("metallic_roughness_texture")!.TryGetTextureId(out var loadedMetallicRoughness));
        Assert.Equal(metallicRoughnessId, loadedMetallicRoughness);
        Assert.True(loaded.GetPropertyValueOrDefault("base_color")!.TryGetColor(out var loadedColor));
        Assert.Equal(new Color(200, 100, 50, 255), loadedColor);
        Assert.True(loaded.GetPropertyValueOrDefault("metallic")!.TryGetFloat(out var loadedMetallic));
        Assert.Equal(0.8f, loadedMetallic);
        Assert.True(loaded.GetPropertyValueOrDefault("roughness")!.TryGetFloat(out var loadedRoughness));
        Assert.Equal(0.2f, loadedRoughness);
        Assert.True(loaded.GetPropertyValueOrDefault("emissive_color")!.TryGetVector3(out var loadedEmissive));
        Assert.Equal(new Vector3(0.1f, 0.2f, 0.3f), loadedEmissive);
        Assert.True(loaded.GetPropertyValueOrDefault("occlusion_strength")!.TryGetFloat(out var loadedOcclusion));
        Assert.Equal(0.6f, loadedOcclusion);
        Assert.True(loaded.GetPropertyValueOrDefault("normal_scale")!.TryGetFloat(out var loadedNormalScale));
        Assert.Equal(1.5f, loadedNormalScale);
        Assert.True(loaded.GetPropertyValueOrDefault("alpha_cutoff")!.TryGetFloat(out var loadedCutoff));
        Assert.Equal(0.3f, loadedCutoff);
        Assert.True(loaded.GetPropertyValueOrDefault("normal_texture")!.TryGetTextureId(out var defaultNormal));
        Assert.Equal(Guid.Empty, defaultNormal);
    }

    [Fact]
    public void Compile_Defaults_GivesAnOpaqueLitPbrMaterialOnTheLitPbrShader()
    {
        var materialAsset = new MaterialAsset("lit-pbr") { Name = "Defaults" };

        var (compiled, runtime) = Compile(materialAsset);

        var material = Assert.IsType<LitPbrMaterial>(runtime);
        Assert.Equal(EffectiveShaderResolver.LitPbrShaderId, compiled.EffectiveShader.ShaderId);
        Assert.Equal("lit-pbr", compiled.DefinitionId);
        Assert.Equal(ShaderFeature.None, compiled.Features);
        Assert.Equal(RenderQueue.Opaque, compiled.Queue);
        Assert.False(compiled.IsTransparent);
        Assert.Equal(Color.White, material.BaseColor);
        Assert.Equal(0.0f, material.Metallic);
        Assert.Equal(0.5f, material.Roughness);
        Assert.Equal(Vector3.Zero, material.EmissiveColor);
        Assert.Equal(1.0f, material.OcclusionStrength);
        Assert.Equal(1.0f, material.NormalScale);
        Assert.Equal(0.5f, material.AlphaCutoff);
        Assert.Equal("LitPbr", LitPbrMaterial.GetTechniqueName(compiled.Features));
    }

    [Fact]
    public void Compile_ExplicitValues_ReachTheRuntimeMaterialAndTheCompiledProperties()
    {
        var materialAsset = new MaterialAsset("lit-pbr");
        var baseTextureId = Guid.NewGuid();
        materialAsset.SetPropertyValue("base_color_texture", MaterialValue.FromTextureId(baseTextureId));
        materialAsset.SetPropertyValue("base_color", MaterialValue.FromColor(new Color(10, 20, 30, 255)));
        materialAsset.SetPropertyValue("metallic", MaterialValue.FromFloat(0.9f));
        materialAsset.SetPropertyValue("roughness", MaterialValue.FromFloat(0.1f));
        materialAsset.SetPropertyValue("emissive_color", MaterialValue.FromVector3(new Vector3(1.0f, 0.5f, 0.0f)));
        materialAsset.SetPropertyValue("occlusion_strength", MaterialValue.FromFloat(0.7f));
        materialAsset.SetPropertyValue("normal_scale", MaterialValue.FromFloat(2.0f));
        materialAsset.SetPropertyValue("alpha_cutoff", MaterialValue.FromFloat(0.35f));

        var (compiled, runtime) = Compile(materialAsset);

        var material = Assert.IsType<LitPbrMaterial>(runtime);
        Assert.Equal(baseTextureId, material.BaseColorTextureAssetId);
        Assert.Equal(new Color(10, 20, 30, 255), material.BaseColor);
        Assert.Equal(0.9f, material.Metallic);
        Assert.Equal(0.1f, material.Roughness);
        Assert.Equal(new Vector3(1.0f, 0.5f, 0.0f), material.EmissiveColor);
        Assert.Equal(0.7f, material.OcclusionStrength);
        Assert.Equal(2.0f, material.NormalScale);
        Assert.Equal(0.35f, material.AlphaCutoff);
        Assert.Equal(ShaderFeature.BasColorTexture | ShaderFeature.Emissive, compiled.Features);
        Assert.True(compiled.TryGetPropertyValue("metallic", out var metallicValue));
        Assert.True(metallicValue.TryGetFloat(out var metallic));
        Assert.Equal(0.9f, metallic);
    }

    [Fact]
    public void Compile_AlphaTestQueue_GivesTheAlphaTestFeatureAndTechnique()
    {
        var materialAsset = new MaterialAsset("lit-pbr") { Queue = RenderQueue.AlphaTest };
        materialAsset.SetPropertyValue("base_color_texture", MaterialValue.FromTextureId(Guid.NewGuid()));

        var (compiled, runtime) = Compile(materialAsset);

        Assert.Equal(RenderQueue.AlphaTest, compiled.Queue);
        Assert.False(compiled.IsTransparent);
        Assert.Equal(ShaderFeature.BasColorTexture | ShaderFeature.AlphaTest, compiled.Features);
        Assert.Equal(RenderQueue.AlphaTest, runtime.Queue);
        Assert.Equal("LitPbr_AlphaTest", LitPbrMaterial.GetTechniqueName(compiled.Features));
    }

    [Fact]
    public void Compile_BaseColorWithAnAlphaBelowOne_InfersTheTransparentPipelineState()
    {
        var materialAsset = new MaterialAsset("lit-pbr");
        materialAsset.SetPropertyValue("base_color", MaterialValue.FromColor(new Color(255, 200, 180, 96)));

        var (compiled, _) = Compile(materialAsset);

        Assert.Equal(ShaderFeature.Transparent, compiled.Features);
        Assert.True(compiled.IsTransparent);
        Assert.Equal(RenderQueue.Transparent, compiled.Queue);
        Assert.Same(BlendState.AlphaBlend, compiled.BlendState);
        Assert.Same(DepthStencilState.DepthRead, compiled.DepthStencilState);
        Assert.Equal("LitPbr_Transparent", LitPbrMaterial.GetTechniqueName(compiled.Features));
    }

    [Fact]
    public void Compile_ExplicitTransparentQueue_IsTransparentWithAnOpaqueBaseColor()
    {
        var materialAsset = new MaterialAsset("lit-pbr") { Queue = RenderQueue.Transparent };

        var (compiled, _) = Compile(materialAsset);

        Assert.Equal(ShaderFeature.Transparent, compiled.Features);
        Assert.Equal(RenderQueue.Transparent, compiled.Queue);
        Assert.Equal("LitPbr_Transparent", LitPbrMaterial.GetTechniqueName(compiled.Features));
    }

    [Fact]
    public void Compile_NormalTextureAlone_GivesTheNormalMapFeatureWithoutABaseTexture()
    {
        var materialAsset = new MaterialAsset("lit-pbr");
        materialAsset.SetPropertyValue("normal_texture", MaterialValue.FromTextureId(Guid.NewGuid()));

        var (compiled, _) = Compile(materialAsset);

        Assert.Equal(ShaderFeature.NormalMap, compiled.Features);
        Assert.Equal("LitPbr_NormalMap", LitPbrMaterial.GetTechniqueName(compiled.Features));
    }

    [Fact]
    public void Compile_MetallicRoughnessAndOcclusionTextures_DoNotChangeTheFeatures()
    {
        var materialAsset = new MaterialAsset("lit-pbr");
        materialAsset.SetPropertyValue("metallic_roughness_texture", MaterialValue.FromTextureId(Guid.NewGuid()));
        materialAsset.SetPropertyValue("occlusion_texture", MaterialValue.FromTextureId(Guid.NewGuid()));

        var (compiled, runtime) = Compile(materialAsset);

        Assert.Equal(ShaderFeature.None, compiled.Features);
        var material = Assert.IsType<LitPbrMaterial>(runtime);
        Assert.NotEqual(Guid.Empty, material.MetallicRoughnessTextureAssetId);
        Assert.NotEqual(Guid.Empty, material.OcclusionTextureAssetId);
    }

    [Fact]
    public void OverrideMapper_MapsBaseColorMetallicRoughnessAndEmissive()
    {
        var materialAsset = new MaterialAsset("lit-pbr");
        var overrides = new MaterialInstanceData();
        overrides.SetPropertyOverride("base_color", MaterialValue.FromColor(Color.OrangeRed));
        overrides.SetPropertyOverride("metallic", MaterialValue.FromFloat(0.25f));
        overrides.SetPropertyOverride("roughness", MaterialValue.FromFloat(0.75f));
        overrides.SetPropertyOverride("emissive_color", MaterialValue.FromVector3(new Vector3(0.2f, 0.4f, 0.6f)));

        var block = MaterialInstancePropertyBlockMapper.Create(materialAsset, overrides);

        Assert.Equal(4, block.PropertyCount);
        Assert.True(block.TryGetVector4(ShaderParameterNames.BaseColor, out var baseColor));
        Assert.Equal(Color.OrangeRed.ToVector4(), baseColor);
        Assert.True(block.TryGetFloat(ShaderParameterNames.Metallic, out var metallic));
        Assert.Equal(0.25f, metallic);
        Assert.True(block.TryGetFloat(ShaderParameterNames.Roughness, out var roughness));
        Assert.Equal(0.75f, roughness);
        Assert.True(block.TryGetVector3(ShaderParameterNames.EmissiveColor, out var emissive));
        Assert.Equal(new Vector3(0.2f, 0.4f, 0.6f), emissive);
    }

    [Fact]
    public void OverrideMapper_MapsOnlyTheOverriddenKeys_AndNothingForOtherProperties()
    {
        var materialAsset = new MaterialAsset("lit-pbr");
        var overrides = new MaterialInstanceData();
        overrides.SetPropertyOverride("roughness", MaterialValue.FromFloat(0.1f));
        overrides.SetPropertyOverride("normal_scale", MaterialValue.FromFloat(3.0f));
        overrides.SetPropertyOverride("occlusion_strength", MaterialValue.FromFloat(0.2f));

        var block = MaterialInstancePropertyBlockMapper.Create(materialAsset, overrides);

        Assert.Equal(1, block.PropertyCount);
        Assert.True(block.TryGetFloat(ShaderParameterNames.Roughness, out var roughness));
        Assert.Equal(0.1f, roughness);
    }

    [GpuFact]
    public void OverrideBlock_AppliedAfterBind_WinsOverTheMaterialValues_AndTheNextBindRestoresThem()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitPbrContentName);
            var shader = new ShaderWrapper(effect);
            var context = new RenderContext { Device = GpuDeviceHost.Instance.GraphicsDevice };
            var material = new LitPbrMaterial { Metallic = 0.1f, Roughness = 0.9f };

            var overrides = new MaterialInstanceData();
            overrides.SetPropertyOverride("metallic", MaterialValue.FromFloat(0.6f));
            overrides.SetPropertyOverride("base_color", MaterialValue.FromColor(Color.Red));
            var block = MaterialInstancePropertyBlockMapper.Create(new MaterialAsset("lit-pbr"), overrides);

            material.Bind(shader, in context, Matrix.Identity);
            block.Apply(shader);

            Assert.Equal(0.6f, effect.Parameters[ShaderParameterNames.Metallic]!.GetValueSingle());
            Assert.Equal(0.9f, effect.Parameters[ShaderParameterNames.Roughness]!.GetValueSingle());
            Assert.Equal(Color.Red.ToVector4(), effect.Parameters[ShaderParameterNames.BaseColor]!.GetValueVector4());

            material.Bind(shader, in context, Matrix.Identity);

            Assert.Equal(0.1f, effect.Parameters[ShaderParameterNames.Metallic]!.GetValueSingle());
            Assert.Equal(Color.White.ToVector4(), effect.Parameters[ShaderParameterNames.BaseColor]!.GetValueVector4());
        });
    }

    private static (CompiledMaterial Compiled, MaterialBase Runtime) Compile(MaterialAsset materialAsset)
    {
        var compiler = new MaterialCompiler();
        var assetContentManager = new AssetContentManager();
        return (compiler.Compile(materialAsset, assetContentManager), compiler.CompileRuntimeMaterial(materialAsset, assetContentManager));
    }
}
