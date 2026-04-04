using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Materials;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class MaterialCacheTests
{
    [Fact]
    public void GetOrCompile_ReusesCachedCompiledMaterialUntilInvalidated()
    {
        var materialAsset = new MaterialAsset("unlit-texture");
        materialAsset.SetPropertyValue("alpha", MaterialValue.FromFloat(0.5f));

        var materialCache = new MaterialCache();
        var assetContentManager = new AssetContentManager();

        var firstCompiledMaterial = materialCache.GetOrCompile(materialAsset, assetContentManager);

        materialAsset.SetPropertyValue("alpha", MaterialValue.FromFloat(1.0f));
        var secondCompiledMaterial = materialCache.GetOrCompile(materialAsset, assetContentManager);

        Assert.Same(firstCompiledMaterial, secondCompiledMaterial);
        Assert.True(secondCompiledMaterial.TryGetPropertyValue("alpha", out var alphaValue));
        Assert.True(alphaValue.TryGetFloat(out var alpha));
        Assert.Equal(0.5f, alpha);
    }

    [Fact]
    public void Invalidate_RemovesCachedMaterialAndNextRequestRecompiles()
    {
        var materialAsset = new MaterialAsset("unlit-texture");
        materialAsset.SetPropertyValue("alpha", MaterialValue.FromFloat(0.5f));

        var materialCache = new MaterialCache();
        var assetContentManager = new AssetContentManager();

        var firstCompiledMaterial = materialCache.GetOrCompile(materialAsset, assetContentManager);

        materialAsset.SetPropertyValue("alpha", MaterialValue.FromFloat(1.0f));
        Assert.True(materialCache.Invalidate(materialAsset.Id));

        var secondCompiledMaterial = materialCache.GetOrCompile(materialAsset, assetContentManager);

        Assert.NotSame(firstCompiledMaterial, secondCompiledMaterial);
        Assert.True(secondCompiledMaterial.TryGetPropertyValue("alpha", out var alphaValue));
        Assert.True(alphaValue.TryGetFloat(out var alpha));
        Assert.Equal(1.0f, alpha);
    }

    [Fact]
    public void Clear_RemovesAllCachedMaterials()
    {
        var firstMaterialAsset = new MaterialAsset("unlit-texture");
        var secondMaterialAsset = new MaterialAsset("lit-diffuse");

        var materialCache = new MaterialCache();
        var assetContentManager = new AssetContentManager();

        materialCache.GetOrCompile(firstMaterialAsset, assetContentManager);
        materialCache.GetOrCompile(secondMaterialAsset, assetContentManager);

        Assert.Equal(2, materialCache.Count);

        materialCache.Clear();

        Assert.Equal(0, materialCache.Count);
        Assert.False(materialCache.TryGet(firstMaterialAsset.Id, out _));
        Assert.False(materialCache.TryGet(secondMaterialAsset.Id, out _));
    }
}