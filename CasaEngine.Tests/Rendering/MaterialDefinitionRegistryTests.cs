using CasaEngine.Framework.Assets;

using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class MaterialDefinitionRegistryTests
{
    private sealed class RegisteredTestMaterial : MaterialBase
    {
        public override void Bind(ShaderWrapper shader, in RenderContext context, Matrix world)
            => throw new NotSupportedException();
    }

    [Fact]
    public void TryGetById_ReturnsBuiltInDefinitions()
    {
        Assert.True(MaterialDefinitionRegistry.TryGetById("lit-diffuse", out var litDiffuseDefinition));
        Assert.Equal(typeof(LitDiffuseMaterial), litDiffuseDefinition.RuntimeMaterialType);

        Assert.True(MaterialDefinitionRegistry.TryGetById("unlit-texture", out var unlitDefinition));
        Assert.Equal(typeof(UnlitTextureMaterial), unlitDefinition.RuntimeMaterialType);

        Assert.False(MaterialDefinitionRegistry.TryGetById("legacy-multi-texture", out _));
    }

    [Fact]
    public void LitDiffuseDefinition_ExposesExpectedPropertyMetadata()
    {
        var definition = MaterialDefinitionRegistry.GetRequiredById("lit-diffuse");

        var baseColorProperty = definition.GetRequiredProperty("base_color_texture");
        Assert.Equal(MaterialPropertyType.Texture, baseColorProperty.ValueType);
        Assert.Equal(MaterialPropertyGroup.Textures, baseColorProperty.Group);
        Assert.Equal(Guid.Empty, baseColorProperty.GetDefaultValue<Guid>());
        Assert.Contains("BasColor_asset_id", baseColorProperty.LegacyAliases);
        Assert.Contains("albedo_asset_id", baseColorProperty.LegacyAliases);
        Assert.Equal("texture", baseColorProperty.AssetKind);

        var specularPowerProperty = definition.GetRequiredProperty("specular_power");
        Assert.Equal(MaterialPropertyType.Float, specularPowerProperty.ValueType);
        Assert.Equal(MaterialPropertyGroup.Lighting, specularPowerProperty.Group);
        Assert.Equal(16.0f, specularPowerProperty.GetDefaultValue<float>());
        Assert.Equal(0.0f, specularPowerProperty.MinValue);
        Assert.Equal(128.0f, specularPowerProperty.MaxValue);
        Assert.Equal(1.0f, specularPowerProperty.Step);

        var reflectionTextureProperty = definition.GetRequiredProperty("reflection_texture");
        Assert.Equal(MaterialPropertyType.Texture, reflectionTextureProperty.ValueType);
        Assert.Equal(MaterialPropertyGroup.Textures, reflectionTextureProperty.Group);
        Assert.Equal(Guid.Empty, reflectionTextureProperty.GetDefaultValue<Guid>());
        Assert.Equal("dds", reflectionTextureProperty.AssetKind);

        var ambientColorProperty = definition.GetRequiredProperty("ambient_color");
        Assert.Equal(MaterialPropertyType.Vector3, ambientColorProperty.ValueType);
        Assert.Equal(MaterialPropertyGroup.Lighting, ambientColorProperty.Group);
        Assert.Equal(Vector3.Zero, ambientColorProperty.GetDefaultValue<Vector3>());

        var alphaCutoffProperty = definition.GetRequiredProperty("alpha_cutoff");
        Assert.Equal(MaterialPropertyType.Float, alphaCutoffProperty.ValueType);
        Assert.Equal(MaterialPropertyGroup.Rendering, alphaCutoffProperty.Group);
        Assert.Equal(0.5f, alphaCutoffProperty.GetDefaultValue<float>());
        Assert.Equal(0.0f, alphaCutoffProperty.MinValue);
        Assert.Equal(1.0f, alphaCutoffProperty.MaxValue);
        Assert.Equal(0.01f, alphaCutoffProperty.Step);
    }

    [Fact]
    public void UnlitDefinition_ExposesAlphaSettingsAsRenderingProperties()
    {
        var definition = MaterialDefinitionRegistry.GetRequiredById("unlit-texture");

        var alphaProperty = definition.GetRequiredProperty("alpha");
        Assert.Equal(MaterialPropertyType.Float, alphaProperty.ValueType);
        Assert.Equal(MaterialPropertyGroup.Rendering, alphaProperty.Group);
        Assert.Equal(1.0f, alphaProperty.GetDefaultValue<float>());
        Assert.Equal(0.0f, alphaProperty.MinValue);
        Assert.Equal(1.0f, alphaProperty.MaxValue);
        Assert.Equal(0.01f, alphaProperty.Step);

        var alphaCutoffProperty = definition.GetRequiredProperty("alpha_cutoff");
        Assert.Equal(MaterialPropertyType.Float, alphaCutoffProperty.ValueType);
        Assert.Equal(MaterialPropertyGroup.Rendering, alphaCutoffProperty.Group);
        Assert.Equal(0.5f, alphaCutoffProperty.GetDefaultValue<float>());
        Assert.Equal(0.0f, alphaCutoffProperty.MinValue);
        Assert.Equal(1.0f, alphaCutoffProperty.MaxValue);
        Assert.Equal(0.01f, alphaCutoffProperty.Step);
    }

    [Fact]
    public void MaterialPropertyDefinition_RejectsIncompatibleDefaultValue()
    {
        Assert.Throws<ArgumentException>(() => new MaterialPropertyDefinition(
            key: "invalid_default",
            displayName: "Invalid Default",
            valueType: MaterialPropertyType.Float,
            group: MaterialPropertyGroup.Surface,
            defaultValue: Color.White));
    }

    [Fact]
    public void MaterialDefinition_RejectsDuplicateSerializedNames()
    {
        var firstProperty = new MaterialPropertyDefinition(
            key: "base_color_texture",
            displayName: "Base Color",
            valueType: MaterialPropertyType.Texture,
            group: MaterialPropertyGroup.Textures,
            defaultValue: Guid.Empty,
            legacyAliases: new[] { "albedo_asset_id" },
            assetKind: "texture");

        var conflictingProperty = new MaterialPropertyDefinition(
            key: "other_texture",
            displayName: "Other Texture",
            valueType: MaterialPropertyType.Texture,
            group: MaterialPropertyGroup.Textures,
            defaultValue: Guid.Empty,
            legacyAliases: new[] { "albedo_asset_id" },
            assetKind: "texture");

        Assert.Throws<ArgumentException>(() => new MaterialDefinition(
            id: "invalid-definition",
            displayName: "Invalid Definition",
            runtimeMaterialType: typeof(LitDiffuseMaterial),
            properties: new[] { firstProperty, conflictingProperty }));
    }

    [Fact]
    public void Register_AddsDefinitionAndAssociatedServices_AndRemovesThemOnDispose()
    {
        var definition = new MaterialDefinition(
            id: "registered-test-material",
            displayName: "Registered Test Material",
            runtimeMaterialType: typeof(RegisteredTestMaterial),
            properties: new[]
            {
                new MaterialPropertyDefinition(
                    key: "tint_color",
                    displayName: "Tint",
                    valueType: MaterialPropertyType.Color,
                    group: MaterialPropertyGroup.Surface,
                    defaultValue: Color.White),
            });
        var customShaderId = Guid.NewGuid();

        var registration = MaterialDefinitionRegistry.Register(
            definition,
            runtimeMaterialFactory: (materialAsset, registeredDefinition, effectiveValues, resolvedTextures, assetContentManager) => new RegisteredTestMaterial
            {
                Id = materialAsset.Id,
                Name = materialAsset.Name,
                ShaderAssetId = customShaderId,
            },
            overrideMapper: static (propertyBlock, materialAsset, registeredDefinition, materialInstanceData, parentResolver) =>
            {
                propertyBlock.SetFloat(ShaderParameterNames.Alpha, 0.5f);
            });

        Assert.True(MaterialDefinitionRegistry.TryGetById(definition.Id, out var registeredById));
        Assert.Same(definition, registeredById);
        Assert.True(MaterialDefinitionRegistry.TryGetByRuntimeType(typeof(RegisteredTestMaterial), out var registeredByRuntimeType));
        Assert.Same(definition, registeredByRuntimeType);

        var materialAsset = new MaterialAsset(definition.Id);
        var compiledMaterial = new MaterialCompiler().Compile(materialAsset, new AssetContentManager());
        Assert.Equal(customShaderId, compiledMaterial.EffectiveShader.ShaderId);

        var materialInstanceData = new MaterialInstanceData();
        materialInstanceData.SetPropertyOverride("tint_color", MaterialValue.FromColor(Color.CornflowerBlue));
        var propertyBlock = MaterialInstancePropertyBlockMapper.Create(materialAsset, materialInstanceData);
        Assert.True(propertyBlock.TryGetFloat(ShaderParameterNames.Alpha, out var alpha));
        Assert.Equal(0.5f, alpha);

        registration.Dispose();

        Assert.False(MaterialDefinitionRegistry.TryGetById(definition.Id, out _));
        Assert.False(MaterialDefinitionRegistry.TryGetByRuntimeType(typeof(RegisteredTestMaterial), out _));
    }
}