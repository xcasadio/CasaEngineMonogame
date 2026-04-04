using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class MaterialInstancePropertyBlockMapperTests
{
    [Fact]
    public void Create_LitDiffuseMaterial_MapsSafePerInstanceOverrides()
    {
        var materialAsset = new MaterialAsset("lit-diffuse");
        var materialInstanceData = new MaterialInstanceData();
        materialInstanceData.SetPropertyOverride("diffuse_color", MaterialValue.FromColor(Color.OrangeRed));
        materialInstanceData.SetPropertyOverride("specular_color", MaterialValue.FromVector3(new Vector3(0.2f, 0.4f, 0.6f)));
        materialInstanceData.SetPropertyOverride("specular_power", MaterialValue.FromFloat(42.0f));

        var propertyBlock = MaterialInstancePropertyBlockMapper.Create(materialAsset, materialInstanceData);

        Assert.True(propertyBlock.TryGetVector4(ShaderParameterNames.DiffuseColor, out var diffuseColor));
        Assert.Equal(Color.OrangeRed.ToVector4(), diffuseColor);
        Assert.True(propertyBlock.TryGetVector3(ShaderParameterNames.SpecularColor, out var specularColor));
        Assert.Equal(new Vector3(0.2f, 0.4f, 0.6f), specularColor);
        Assert.True(propertyBlock.TryGetFloat(ShaderParameterNames.SpecularPower, out var specularPower));
        Assert.Equal(42.0f, specularPower);
    }

    [Fact]
    public void Apply_UnlitTextureMaterial_PacksTintOverrideWithBaseAlpha()
    {
        var materialAsset = new MaterialAsset("unlit-texture")
        {
            IsTransparent = true,
            Queue = RenderQueue.Transparent,
            BlendStateName = "AlphaBlend",
        };
        materialAsset.SetPropertyValue("alpha", MaterialValue.FromFloat(0.35f));

        var materialInstanceData = new MaterialInstanceData();
        materialInstanceData.SetPropertyOverride("tint_color", MaterialValue.FromColor(Color.Aqua));

        var propertyBlock = new MaterialPropertyBlock();
        MaterialInstancePropertyBlockMapper.Apply(propertyBlock, materialAsset, materialInstanceData);

        Assert.True(propertyBlock.TryGetVector4(ShaderParameterNames.TintColor, out var tintColor));
        Assert.Equal(Color.Aqua.ToVector4(), tintColor);
        Assert.True(propertyBlock.TryGetFloat(ShaderParameterNames.Alpha, out var alpha));
        Assert.Equal(0.35f, alpha);
    }

    [Fact]
    public void Apply_UnlitTextureMaterial_UsesAlphaOverrideWhenBaseMaterialIsAlreadyTransparent()
    {
        var materialAsset = new MaterialAsset("unlit-texture")
        {
            IsTransparent = true,
            Queue = RenderQueue.Transparent,
            BlendStateName = "AlphaBlend",
        };
        materialAsset.SetPropertyValue("tint_color", MaterialValue.FromColor(Color.White));
        materialAsset.SetPropertyValue("alpha", MaterialValue.FromFloat(0.8f));

        var materialInstanceData = new MaterialInstanceData();
        materialInstanceData.SetPropertyOverride("alpha", MaterialValue.FromFloat(0.25f));

        var propertyBlock = MaterialInstancePropertyBlockMapper.Create(materialAsset, materialInstanceData);

        Assert.True(propertyBlock.TryGetVector4(ShaderParameterNames.TintColor, out var tintColor));
        Assert.Equal(Color.White.ToVector4(), tintColor);
        Assert.True(propertyBlock.TryGetFloat(ShaderParameterNames.Alpha, out var alpha));
        Assert.Equal(0.25f, alpha);
    }

    [Fact]
    public void Apply_UnlitTextureMaterial_SkipsAlphaOnlyOverrideWhenBaseMaterialIsOpaque()
    {
        var materialAsset = new MaterialAsset("unlit-texture");
        var materialInstanceData = new MaterialInstanceData();
        materialInstanceData.SetPropertyOverride("alpha", MaterialValue.FromFloat(0.2f));

        var propertyBlock = MaterialInstancePropertyBlockMapper.Create(materialAsset, materialInstanceData);

        Assert.True(propertyBlock.IsEmpty);
    }
}