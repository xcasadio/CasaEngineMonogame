using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class StaticModelMaterialResolverTests
{
    [Fact]
    public void CreateTextureFallbackMaterial_ReturnsLitDiffuseMaterialBoundToTextureAsset()
    {
        var textureAssetId = Guid.NewGuid();

        var material = StaticModelMaterialResolver.CreateTextureFallbackMaterial("Car Paint", textureAssetId, basColor: null);

        Assert.Equal("Car Paint [Generated Texture Material]", material.Name);
        Assert.Equal(textureAssetId, material.BasColorAssetId);
        Assert.Equal(Color.White, material.DiffuseColor);
        Assert.Equal(Vector3.Zero, material.EmissiveColor);
        Assert.Null(material.BasColor);
    }

    [Fact]
    public void CreateMissingMaterial_ReturnsExplicitMagentaMaterial()
    {
        var material = StaticModelMaterialResolver.CreateMissingMaterial("Wheel");

        Assert.Equal("Wheel [Missing Material]", material.Name);
        Assert.Equal(Color.Magenta, material.DiffuseColor);
        Assert.Equal(new Vector3(0.1f, 0.0f, 0.1f), material.EmissiveColor);
        Assert.Equal(Vector3.Zero, material.SpecularColor);
        Assert.Equal(1.0f, material.SpecularPower);
    }
}