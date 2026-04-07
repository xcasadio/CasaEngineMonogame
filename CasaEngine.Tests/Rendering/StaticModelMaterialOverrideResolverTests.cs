using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering.Models;

using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class StaticModelMaterialOverrideResolverTests
{
    [Fact]
    public void ResolveForMesh_UsesOverrideMaterialAssetAndInstanceOverrides()
    {
        var assetContentManager = new AssetContentManager();
        var defaultMaterialAsset = CreateLitDiffuseMaterialAsset("Default", Color.White);
        var overrideMaterialAsset = CreateLitDiffuseMaterialAsset("Override", Color.LightGray);
        assetContentManager.AddAsset(defaultMaterialAsset.Id, defaultMaterialAsset.Name, defaultMaterialAsset);
        assetContentManager.AddAsset(overrideMaterialAsset.Id, overrideMaterialAsset.Name, overrideMaterialAsset);

        var mesh = new StaticModelMesh
        {
            Name = "Body",
            SlotName = "Body",
            MaterialSlotIndex = 0,
            MaterialAssetId = defaultMaterialAsset.Id,
        };

        var slotOverride = new MaterialSlotOverride
        {
            SlotName = "Body",
            SlotIndex = 12,
            MaterialAssetId = overrideMaterialAsset.Id,
        };
        slotOverride.MaterialInstanceData.SetPropertyOverride("diffuse_color", MaterialValue.FromColor(Color.OrangeRed));

        var resolvedOverrides = StaticModelMaterialOverrideResolver.ResolveForMesh(
            mesh,
            new[] { slotOverride },
            assetContentManager);

        Assert.NotNull(resolvedOverrides);
        Assert.NotNull(resolvedOverrides!.MaterialOverridesBySlotIndex);
        Assert.True(resolvedOverrides.MaterialOverridesBySlotIndex!.TryGetValue(0, out var runtimeMaterial));
        var litMaterial = Assert.IsType<LitDiffuseMaterial>(runtimeMaterial);
        Assert.Equal(Color.LightGray, litMaterial.DiffuseColor);

        Assert.NotNull(resolvedOverrides.PropertyOverridesBySlotIndex);
        Assert.True(resolvedOverrides.PropertyOverridesBySlotIndex!.TryGetValue(0, out var propertyBlock));
        Assert.True(propertyBlock.TryGetVector4(ShaderParameterNames.DiffuseColor, out var diffuseColor));
        Assert.Equal(Color.OrangeRed.ToVector4(), diffuseColor);
    }

    [Fact]
    public void ResolveForMesh_UsesDefaultSlotMaterialAssetForInstanceOverrides()
    {
        var assetContentManager = new AssetContentManager();
        var defaultMaterialAsset = CreateLitDiffuseMaterialAsset("Default", Color.White);
        assetContentManager.AddAsset(defaultMaterialAsset.Id, defaultMaterialAsset.Name, defaultMaterialAsset);

        var mesh = new StaticModelMesh
        {
            Name = "Body",
            SlotName = "Body",
            MaterialSlotIndex = 0,
            MaterialAssetId = defaultMaterialAsset.Id,
        };

        var slotOverride = new MaterialSlotOverride
        {
            SlotName = "Body",
            SlotIndex = 3,
        };
        slotOverride.MaterialInstanceData.SetPropertyOverride("diffuse_color", MaterialValue.FromColor(Color.CadetBlue));

        var resolvedOverrides = StaticModelMaterialOverrideResolver.ResolveForMesh(
            mesh,
            new[] { slotOverride },
            assetContentManager);

        Assert.NotNull(resolvedOverrides);
        Assert.Null(resolvedOverrides!.MaterialOverridesBySlotIndex);
        Assert.NotNull(resolvedOverrides.PropertyOverridesBySlotIndex);
        Assert.True(resolvedOverrides.PropertyOverridesBySlotIndex!.TryGetValue(0, out var propertyBlock));
        Assert.True(propertyBlock.TryGetVector4(ShaderParameterNames.DiffuseColor, out var diffuseColor));
        Assert.Equal(Color.CadetBlue.ToVector4(), diffuseColor);
    }

    [Fact]
    public void MaterialSlotOverrideJsonSerializer_RoundTripsMaterialInstanceData()
    {
        var materialSlotOverride = new MaterialSlotOverride
        {
            SlotName = "Door",
            SlotIndex = 4,
            MaterialAssetId = Guid.NewGuid(),
        };
        materialSlotOverride.MaterialInstanceData.SetPropertyOverride("specular_power", MaterialValue.FromFloat(24.0f));

        var node = new JObject();
        MaterialSlotOverrideJsonSerializer.Save(materialSlotOverride, node);

        var loadedOverride = new MaterialSlotOverride();
        loadedOverride.Load(node);

        Assert.Equal(materialSlotOverride.SlotName, loadedOverride.SlotName);
        Assert.Equal(materialSlotOverride.SlotIndex, loadedOverride.SlotIndex);
        Assert.Equal(materialSlotOverride.MaterialAssetId, loadedOverride.MaterialAssetId);
        Assert.True(loadedOverride.MaterialInstanceData.TryGetPropertyOverride("specular_power", out var specularPower));
        Assert.True(specularPower.TryGetFloat(out var value));
        Assert.Equal(24.0f, value);
    }

    private static MaterialAsset CreateLitDiffuseMaterialAsset(string name, Color diffuseColor)
    {
        var materialAsset = new MaterialAsset("lit-diffuse")
        {
            Name = name,
        };
        materialAsset.SetPropertyValue("base_color_texture", MaterialValue.FromTextureId(Guid.Empty));
        materialAsset.SetPropertyValue("normal_texture", MaterialValue.FromTextureId(Guid.Empty));
        materialAsset.SetPropertyValue("diffuse_color", MaterialValue.FromColor(diffuseColor));
        materialAsset.SetPropertyValue("emissive_color", MaterialValue.FromVector3(Vector3.Zero));
        materialAsset.SetPropertyValue("specular_color", MaterialValue.FromVector3(new Vector3(0.5f)));
        materialAsset.SetPropertyValue("specular_power", MaterialValue.FromFloat(32.0f));
        return materialAsset;
    }
}