using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Assets;
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

    [Fact]
    public void ResolveMeshMaterial_PreservesExplicitRuntimeMaterialWhenNoAssetBindingExists()
    {
        var explicitMaterial = new LitDiffuseMaterial
        {
            Name = "Explicit Mesh Material",
            DiffuseColor = Color.Orange,
        };
        var mesh = new StaticModelMesh
        {
            Material = explicitMaterial,
            MaterialAssetId = Guid.Empty,
            TextureAssetId = Guid.Empty,
        };

        var resolvedMaterial = StaticModelMaterialResolver.ResolveMeshMaterial(mesh, new AssetContentManager());

        Assert.Same(explicitMaterial, resolvedMaterial);
    }

    [Fact]
    public void ResolveSubMeshMaterial_PreservesExplicitSubMeshRuntimeMaterialWhenNoAssetBindingExists()
    {
        var meshMaterial = new LitDiffuseMaterial
        {
            Name = "Mesh Material",
            DiffuseColor = Color.CadetBlue,
        };
        var explicitSubMeshMaterial = new LitDiffuseMaterial
        {
            Name = "Explicit SubMesh Material",
            DiffuseColor = Color.Goldenrod,
        };
        var mesh = new StaticModelMesh();
        var subMesh = new SubMesh
        {
            Material = explicitSubMeshMaterial,
            MaterialAssetId = Guid.Empty,
        };

        var resolvedMaterial = StaticModelMaterialResolver.ResolveSubMeshMaterial(
            mesh,
            subMesh,
            new AssetContentManager(),
            meshMaterial);

        Assert.Same(explicitSubMeshMaterial, resolvedMaterial);
    }
}