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
        "detail_texture",
        "detail_tiling",
        "base_color",
        "metallic",
        "roughness",
        "emissive_color",
        "emissive_shadow_factor",
        "occlusion_strength",
        "normal_scale",
        "clear_coat",
        "clear_coat_roughness",
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
        Assert.Equal(defaults.ClearCoat, definition.GetRequiredProperty("clear_coat").GetDefaultValue<float>());
        Assert.Equal(defaults.ClearCoatRoughness, definition.GetRequiredProperty("clear_coat_roughness").GetDefaultValue<float>());
        Assert.Equal(0.0f, defaults.ClearCoat);
        Assert.Equal(0.1f, defaults.ClearCoatRoughness);

        var emissiveShadowFactor = definition.GetRequiredProperty("emissive_shadow_factor");
        Assert.Equal(MaterialPropertyType.Float, emissiveShadowFactor.ValueType);
        Assert.Equal(MaterialPropertyGroup.Lighting, emissiveShadowFactor.Group);
        Assert.Equal(0.0f, emissiveShadowFactor.GetDefaultValue<float>());
        Assert.Equal(defaults.EmissiveShadowFactor, emissiveShadowFactor.GetDefaultValue<float>());
        Assert.Equal(0.0f, emissiveShadowFactor.MinValue);
        Assert.Equal(1.0f, emissiveShadowFactor.MaxValue);
        Assert.Equal(MaterialPropertyFlags.None, emissiveShadowFactor.Flags & MaterialPropertyFlags.SupportsOverrides);

        var detailTiling = definition.GetRequiredProperty("detail_tiling");
        Assert.Equal(MaterialPropertyType.Float, detailTiling.ValueType);
        Assert.Equal(MaterialPropertyGroup.Textures, detailTiling.Group);
        Assert.Equal(defaults.DetailTiling, detailTiling.GetDefaultValue<float>());
        Assert.Equal(1.0f, defaults.DetailTiling);
        Assert.Equal(0.0f, detailTiling.MinValue);
        Assert.Equal(Guid.Empty, defaults.DetailTextureAssetId);
        Assert.Null(defaults.DetailTexture);

        foreach (var key in new[] { "base_color_texture", "normal_texture", "metallic_roughness_texture", "occlusion_texture", "emissive_texture", "detail_texture" })
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
        overrides.SetPropertyOverride("emissive_shadow_factor", MaterialValue.FromFloat(1.0f));

        var block = MaterialInstancePropertyBlockMapper.Create(materialAsset, overrides);

        Assert.Equal(1, block.PropertyCount);
        Assert.True(block.TryGetFloat(ShaderParameterNames.Roughness, out var roughness));
        Assert.Equal(0.1f, roughness);
    }

    [Fact]
    public void MaterialFile_WithTheClearCoatKeys_RoundTrips_AndWithoutThemReadsTheDefaults()
    {
        var withCoat = new MaterialAsset("lit-pbr") { Name = "Car paint" };
        withCoat.SetPropertyValue("clear_coat", MaterialValue.FromFloat(0.8f));
        withCoat.SetPropertyValue("clear_coat_roughness", MaterialValue.FromFloat(0.25f));

        var document = new JObject();
        MaterialAssetJsonSerializer.Save(withCoat, document);

        var propertiesNode = Assert.IsType<JObject>(document["properties"]);
        Assert.Equal(2, propertiesNode.Count);
        Assert.NotNull(propertiesNode["clear_coat"]);
        Assert.NotNull(propertiesNode["clear_coat_roughness"]);

        var loaded = new MaterialAsset();
        loaded.Load(document);
        Assert.True(loaded.GetPropertyValueOrDefault("clear_coat")!.TryGetFloat(out var coat));
        Assert.Equal(0.8f, coat);
        Assert.True(loaded.GetPropertyValueOrDefault("clear_coat_roughness")!.TryGetFloat(out var coatRoughness));
        Assert.Equal(0.25f, coatRoughness);

        // A file written before the clear coat existed carries neither key: it reads the defaults and compiles to a bare coat.
        var legacyDocument = new JObject();
        MaterialAssetJsonSerializer.Save(new MaterialAsset("lit-pbr") { Name = "Old file" }, legacyDocument);
        Assert.Null(((JObject)legacyDocument["properties"]!)["clear_coat"]);

        var legacy = new MaterialAsset();
        legacy.Load(legacyDocument);
        Assert.True(legacy.GetPropertyValueOrDefault("clear_coat")!.TryGetFloat(out var defaultCoat));
        Assert.Equal(0.0f, defaultCoat);
        Assert.True(legacy.GetPropertyValueOrDefault("clear_coat_roughness")!.TryGetFloat(out var defaultCoatRoughness));
        Assert.Equal(0.1f, defaultCoatRoughness);

        var (_, runtime) = Compile(legacy);
        var material = Assert.IsType<LitPbrMaterial>(runtime);
        Assert.Equal(0.0f, material.ClearCoat);
        Assert.Equal(0.1f, material.ClearCoatRoughness);
    }

    [Fact]
    public void MaterialFile_WithTheEmissiveShadowKey_RoundTrips_AndWithoutItReadsZero()
    {
        var withKey = new MaterialAsset("lit-pbr") { Name = "Lamp" };
        withKey.SetPropertyValue("emissive_shadow_factor", MaterialValue.FromFloat(0.7f));

        var document = new JObject();
        MaterialAssetJsonSerializer.Save(withKey, document);

        var propertiesNode = Assert.IsType<JObject>(document["properties"]);
        Assert.Equal(1, propertiesNode.Count);
        Assert.NotNull(propertiesNode["emissive_shadow_factor"]);

        var loaded = new MaterialAsset();
        loaded.Load(document);
        Assert.True(loaded.GetPropertyValueOrDefault("emissive_shadow_factor")!.TryGetFloat(out var factor));
        Assert.Equal(0.7f, factor);

        var (_, loadedRuntime) = Compile(loaded);
        Assert.Equal(0.7f, Assert.IsType<LitPbrMaterial>(loadedRuntime).EmissiveShadowFactor);

        // A file written before the key existed carries nothing for it: it reads 0 and compiles to an unshadowed emissive.
        var legacyDocument = new JObject();
        MaterialAssetJsonSerializer.Save(new MaterialAsset("lit-pbr") { Name = "Old file" }, legacyDocument);
        Assert.Null(((JObject)legacyDocument["properties"]!)["emissive_shadow_factor"]);

        var legacy = new MaterialAsset();
        legacy.Load(legacyDocument);
        Assert.True(legacy.GetPropertyValueOrDefault("emissive_shadow_factor")!.TryGetFloat(out var defaultFactor));
        Assert.Equal(0.0f, defaultFactor);

        var (compiled, runtime) = Compile(legacy);
        Assert.Equal(0.0f, Assert.IsType<LitPbrMaterial>(runtime).EmissiveShadowFactor);

        // A scalar like the clear coat: it changes neither the shader features nor the technique.
        Assert.Equal(ShaderFeature.None, compiled.Features);
    }

    [Fact]
    public void MaterialFile_WithTheDetailKeys_RoundTrips_AndWithoutThemReadsTheDefaults()
    {
        var detailTextureId = Guid.NewGuid();
        var withDetail = new MaterialAsset("lit-pbr") { Name = "Terrain" };
        withDetail.SetPropertyValue("detail_texture", MaterialValue.FromTextureId(detailTextureId));
        withDetail.SetPropertyValue("detail_tiling", MaterialValue.FromFloat(24.0f));

        var document = new JObject();
        MaterialAssetJsonSerializer.Save(withDetail, document);

        var propertiesNode = Assert.IsType<JObject>(document["properties"]);
        Assert.Equal(2, propertiesNode.Count);
        Assert.NotNull(propertiesNode["detail_texture"]);
        Assert.NotNull(propertiesNode["detail_tiling"]);

        var loaded = new MaterialAsset();
        loaded.Load(document);
        Assert.True(loaded.GetPropertyValueOrDefault("detail_texture")!.TryGetTextureId(out var loadedTexture));
        Assert.Equal(detailTextureId, loadedTexture);
        Assert.True(loaded.GetPropertyValueOrDefault("detail_tiling")!.TryGetFloat(out var loadedTiling));
        Assert.Equal(24.0f, loadedTiling);

        // A material that never set them writes neither key and reads the defaults, so a file written before the detail texture
        // existed loads and compiles as it did.
        var defaultDocument = new JObject();
        MaterialAssetJsonSerializer.Save(new MaterialAsset("lit-pbr") { Name = "Old file" }, defaultDocument);
        var defaultProperties = Assert.IsType<JObject>(defaultDocument["properties"]);
        Assert.Null(defaultProperties["detail_texture"]);
        Assert.Null(defaultProperties["detail_tiling"]);

        var legacy = new MaterialAsset();
        legacy.Load(defaultDocument);
        Assert.True(legacy.GetPropertyValueOrDefault("detail_texture")!.TryGetTextureId(out var defaultTexture));
        Assert.Equal(Guid.Empty, defaultTexture);
        Assert.True(legacy.GetPropertyValueOrDefault("detail_tiling")!.TryGetFloat(out var defaultTiling));
        Assert.Equal(1.0f, defaultTiling);

        var (compiled, runtime) = Compile(legacy);
        var material = Assert.IsType<LitPbrMaterial>(runtime);
        Assert.Equal(Guid.Empty, material.DetailTextureAssetId);
        Assert.Null(material.DetailTexture);
        Assert.Equal(1.0f, material.DetailTiling);
        Assert.Equal(ShaderFeature.None, compiled.Features);
    }

    [Fact]
    public void Compile_DetailValues_ReachTheRuntimeMaterial_WithoutChangingTheFeaturesOrTheTechnique()
    {
        var detailTextureId = Guid.NewGuid();
        var materialAsset = new MaterialAsset("lit-pbr");
        materialAsset.SetPropertyValue("detail_texture", MaterialValue.FromTextureId(detailTextureId));
        materialAsset.SetPropertyValue("detail_tiling", MaterialValue.FromFloat(24.0f));

        var (compiled, runtime) = Compile(materialAsset);

        var material = Assert.IsType<LitPbrMaterial>(runtime);
        Assert.Equal(detailTextureId, material.DetailTextureAssetId);
        Assert.Equal(24.0f, material.DetailTiling);
        Assert.Equal(ShaderFeature.None, compiled.Features);
        Assert.Equal("LitPbr", LitPbrMaterial.GetTechniqueName(compiled.Features));
        Assert.True(compiled.TryGetPropertyValue("detail_tiling", out var tiling));
        Assert.True(tiling.TryGetFloat(out var compiledTiling));
        Assert.Equal(24.0f, compiledTiling);
    }

    [Theory]
    [InlineData(4.0f, 4.0f)]
    [InlineData(0.0f, 1.0f)]
    [InlineData(-3.0f, 1.0f)]
    [InlineData(float.NaN, 1.0f)]
    [InlineData(float.PositiveInfinity, 1.0f)]
    public void DetailTiling_ReplacesAValueThatIsNotFiniteOrNotPositiveByOne(float assigned, float expected)
    {
        var material = new LitPbrMaterial { DetailTiling = assigned };

        Assert.Equal(expected, material.DetailTiling);
    }

    [Fact]
    public void Compile_ClearCoatValues_ReachTheRuntimeMaterial()
    {
        var materialAsset = new MaterialAsset("lit-pbr");
        materialAsset.SetPropertyValue("clear_coat", MaterialValue.FromFloat(0.6f));
        materialAsset.SetPropertyValue("clear_coat_roughness", MaterialValue.FromFloat(0.3f));

        var (compiled, runtime) = Compile(materialAsset);

        var material = Assert.IsType<LitPbrMaterial>(runtime);
        Assert.Equal(0.6f, material.ClearCoat);
        Assert.Equal(0.3f, material.ClearCoatRoughness);

        // The clear coat is two scalars: it changes neither the shader features nor the technique.
        Assert.Equal(ShaderFeature.None, compiled.Features);
        Assert.Equal("LitPbr", LitPbrMaterial.GetTechniqueName(compiled.Features));
    }

    [Fact]
    public void OverrideMapper_MapsTheClearCoatKeysOntoTheShaderConstants()
    {
        var materialAsset = new MaterialAsset("lit-pbr");
        var overrides = new MaterialInstanceData();
        overrides.SetPropertyOverride("clear_coat", MaterialValue.FromFloat(0.9f));
        overrides.SetPropertyOverride("clear_coat_roughness", MaterialValue.FromFloat(0.2f));

        var block = MaterialInstancePropertyBlockMapper.Create(materialAsset, overrides);

        Assert.Equal(2, block.PropertyCount);
        Assert.True(block.TryGetFloat(ShaderParameterNames.ClearCoat, out var clearCoat));
        Assert.Equal(0.9f, clearCoat);
        Assert.True(block.TryGetFloat(ShaderParameterNames.ClearCoatRoughness, out var clearCoatRoughness));
        Assert.Equal(0.2f, clearCoatRoughness);
        Assert.Equal("ClearCoat", ShaderParameterNames.ClearCoat);
        Assert.Equal("ClearCoatRoughness", ShaderParameterNames.ClearCoatRoughness);
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
