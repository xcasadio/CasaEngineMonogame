using CasaEngine.EditorServices;
using CasaEngine.Engine;
using CasaEngine.Framework;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Materials;
using CasaEngine.Tests;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Rendering;

[Collection(ProjectEnvironmentCollection.Name)]
public class MaterialRuntimeResolverTests
{
    [Fact]
    public void TryLoadRuntimeMaterial_AuthoringFile_LoadsAndCompilesCurrentRuntimeMaterial()
    {
        using var scope = new TestProjectScope();
        Guid materialAssetId = Guid.NewGuid();
        string relativeFileName = Path.Combine("Materials", "AuthoringLit.material");

        var materialAsset = new MaterialAsset("lit-diffuse")
        {
            Name = "Authoring Lit",
        };
        materialAsset.SetPropertyValue("diffuse_color", MaterialValue.FromColor(Color.OrangeRed));
        materialAsset.SetPropertyValue("specular_power", MaterialValue.FromFloat(14.0f));

        var document = new JObject();
        MaterialAssetJsonSerializer.Save(materialAsset, document);
        document["id"] = materialAssetId.ToString();
        document["name"] = "Authoring Lit";

        scope.WriteAsset(relativeFileName, materialAssetId, "Authoring Lit", document);

        var assetContentManager = CreateAssetContentManager();
        bool loaded = MaterialRuntimeResolver.TryLoadRuntimeMaterial(materialAssetId, assetContentManager, out var runtimeMaterial);

        Assert.True(loaded);
        var litMaterial = Assert.IsType<LitDiffuseMaterial>(runtimeMaterial);
        Assert.Equal(Color.OrangeRed, litMaterial.DiffuseColor);
        Assert.Equal(14.0f, litMaterial.SpecularPower);
        Assert.Equal(materialAssetId, litMaterial.Id);
    }

    [Fact]
    public void TryLoadRuntimeMaterial_LegacyLitFile_LoadsAndCompilesCurrentRuntimeMaterial()
    {
        using var scope = new TestProjectScope();
        Guid materialAssetId = Guid.NewGuid();
        Guid baseColorTextureId = Guid.NewGuid();
        string relativeFileName = Path.Combine("Materials", "LegacyLit.material");

        var document = new JObject
        {
            ["id"] = materialAssetId.ToString(),
            ["name"] = "Legacy Lit",
            ["type"] = nameof(LitDiffuseMaterial),
            ["is_transparent"] = false,
            ["queue"] = RenderQueue.Opaque.ToString(),
            ["shader_asset_id"] = Guid.Empty.ToString(),
            ["cast_shadows"] = true,
            ["receive_shadows"] = true,
            ["blend_state"] = "Opaque",
            ["depth_stencil_state"] = "Default",
            ["rasterizer_state"] = "CullCounterClockwise",
            ["sampler_state"] = "AnisotropicClamp",
            ["BasColor_asset_id"] = baseColorTextureId.ToString(),
            ["normal_map_asset_id"] = Guid.Empty.ToString(),
            ["diffuse_color"] = new JObject
            {
                ["r"] = 12,
                ["g"] = 34,
                ["b"] = 56,
                ["a"] = 255,
            },
            ["emissive_color"] = new JObject
            {
                ["r"] = 0.1f,
                ["g"] = 0.2f,
                ["b"] = 0.3f,
            },
            ["specular_color"] = new JObject
            {
                ["r"] = 0.4f,
                ["g"] = 0.5f,
                ["b"] = 0.6f,
            },
            ["specular_power"] = 8.0f,
        };

        scope.WriteAsset(relativeFileName, materialAssetId, "Legacy Lit", document);

        var assetContentManager = CreateAssetContentManager();
        bool loaded = MaterialRuntimeResolver.TryLoadRuntimeMaterial(materialAssetId, assetContentManager, out var runtimeMaterial);

        Assert.True(loaded);
        var litMaterial = Assert.IsType<LitDiffuseMaterial>(runtimeMaterial);
        Assert.Equal(baseColorTextureId, litMaterial.BasColorAssetId);
        Assert.Equal(new Color(12, 34, 56, 255), litMaterial.DiffuseColor);
        Assert.Equal(new Vector3(0.1f, 0.2f, 0.3f), litMaterial.EmissiveColor);
        Assert.Equal(new Vector3(0.4f, 0.5f, 0.6f), litMaterial.SpecularColor);
        Assert.Equal(8.0f, litMaterial.SpecularPower);
    }

    [Fact]
    public void TryLoadRuntimeMaterial_AuthoringChildFile_InheritsMissingParentProperties()
    {
        using var scope = new TestProjectScope();
        Guid parentMaterialAssetId = Guid.NewGuid();
        Guid childMaterialAssetId = Guid.NewGuid();
        string parentRelativeFileName = Path.Combine("Materials", "ParentLit.material");
        string childRelativeFileName = Path.Combine("Materials", "ChildLit.material");

        var parentMaterialAsset = new MaterialAsset("lit-diffuse")
        {
            Name = "Parent Lit",
        };
        parentMaterialAsset.SetPropertyValue("specular_power", MaterialValue.FromFloat(18.0f));
        parentMaterialAsset.SetPropertyValue("diffuse_color", MaterialValue.FromColor(Color.CornflowerBlue));

        var parentDocument = new JObject();
        MaterialAssetJsonSerializer.Save(parentMaterialAsset, parentDocument);
        parentDocument["id"] = parentMaterialAssetId.ToString();
        parentDocument["name"] = "Parent Lit";

        scope.WriteAsset(parentRelativeFileName, parentMaterialAssetId, "Parent Lit", parentDocument);

        var childMaterialAsset = new MaterialAsset("lit-diffuse")
        {
            Name = "Child Lit",
            ParentMaterialAssetId = parentMaterialAssetId,
        };
        childMaterialAsset.SetPropertyValue("diffuse_color", MaterialValue.FromColor(Color.Goldenrod));

        var childDocument = new JObject();
        MaterialAssetJsonSerializer.Save(childMaterialAsset, childDocument);
        childDocument["id"] = childMaterialAssetId.ToString();
        childDocument["name"] = "Child Lit";

        scope.WriteAsset(childRelativeFileName, childMaterialAssetId, "Child Lit", childDocument);

        var assetContentManager = CreateAssetContentManager();
        bool loaded = MaterialRuntimeResolver.TryLoadRuntimeMaterial(childMaterialAssetId, assetContentManager, out var runtimeMaterial);

        Assert.True(loaded);
        var litMaterial = Assert.IsType<LitDiffuseMaterial>(runtimeMaterial);
        Assert.Equal(Color.Goldenrod, litMaterial.DiffuseColor);
        Assert.Equal(18.0f, litMaterial.SpecularPower);
        Assert.Equal(childMaterialAssetId, litMaterial.Id);
    }

    [Fact]
    public void TryLoadRuntimeMaterial_LegacyMultiTextureFile_PreservesLegacyTextureSlots()
    {
        using var scope = new TestProjectScope();
        Guid materialAssetId = Guid.NewGuid();
        Guid baseColorTextureId = Guid.NewGuid();
        Guid reflectionTextureId = Guid.NewGuid();
        string relativeFileName = Path.Combine("Materials", "LegacyMulti.material");

        var document = new JObject
        {
            ["id"] = materialAssetId.ToString(),
            ["name"] = "Legacy Multi",
            ["type"] = nameof(Material),
            ["is_transparent"] = false,
            ["queue"] = RenderQueue.Opaque.ToString(),
            ["shader_asset_id"] = Guid.Empty.ToString(),
            ["cast_shadows"] = true,
            ["receive_shadows"] = true,
            ["blend_state"] = "Opaque",
            ["depth_stencil_state"] = "Default",
            ["rasterizer_state"] = "CullCounterClockwise",
            ["sampler_state"] = "AnisotropicClamp",
            ["texture_base_color_asset_id"] = baseColorTextureId.ToString(),
            ["texture_reflection_asset_id"] = reflectionTextureId.ToString(),
        };

        scope.WriteAsset(relativeFileName, materialAssetId, "Legacy Multi", document);

        var assetContentManager = CreateAssetContentManager();
        bool loaded = MaterialRuntimeResolver.TryLoadRuntimeMaterial(materialAssetId, assetContentManager, out var runtimeMaterial);

        Assert.True(loaded);
        var legacyMaterial = Assert.IsType<Material>(runtimeMaterial);
        Assert.Equal(baseColorTextureId, legacyMaterial.TextureBaseColorAssetId);
        Assert.Equal(reflectionTextureId, legacyMaterial.TextureReflectionAssetId);
    }

    private static AssetContentManager CreateAssetContentManager()
    {
        var assetContentManager = new AssetContentManager();
        AssetLoaderRegistry.RegisterLoaders(assetContentManager);
        return assetContentManager;
    }

    private sealed class TestProjectScope : IDisposable
    {
        private readonly string? _previousProjectPath;

        public TestProjectScope()
        {
            _previousProjectPath = EngineEnvironment.ProjectPath;
            ProjectPath = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ProjectPath);

            EngineEnvironment.ProjectPath = ProjectPath;
            EditorAssetCatalogService.Clear();
        }

        public string ProjectPath { get; }

        public void Dispose()
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = _previousProjectPath;
            Directory.Delete(ProjectPath, recursive: true);
        }

        public void WriteAsset(string relativeFileName, Guid assetId, string assetName, JObject document)
        {
            string fullFileName = Path.Combine(ProjectPath, relativeFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(fullFileName)!);
            File.WriteAllText(fullFileName, document.ToString());

            EditorAssetCatalogService.Add(new AssetInfo(assetId)
            {
                Name = assetName,
                FileName = relativeFileName,
                AssetType = AssetInfo.InferAssetType(relativeFileName),
            });
        }
    }
}