using CasaEngine.EditorServices;
using CasaEngine.Engine;
using CasaEngine.Framework;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Project;
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
        Guid reflectionTextureId = Guid.NewGuid();
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
            ["texture_reflection_asset_id"] = reflectionTextureId.ToString(),
            ["diffuse_color"] = new JObject
            {
                ["r"] = 12,
                ["g"] = 34,
                ["b"] = 56,
                ["a"] = 255,
            },
            ["ambient_color"] = new JObject
            {
                ["r"] = 0.15f,
                ["g"] = 0.25f,
                ["b"] = 0.35f,
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
        Assert.Equal(reflectionTextureId, litMaterial.ReflectionCubeAssetId);
        Assert.Equal(new Color(12, 34, 56, 255), litMaterial.DiffuseColor);
        Assert.Equal(new Vector3(0.15f, 0.25f, 0.35f), litMaterial.AmbientColor);
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
    public void TryLoadRuntimeMaterial_WithMaterialCache_ReusesRuntimeMaterialUntilInvalidated()
    {
        using var scope = new TestProjectScope();
        Guid materialAssetId = Guid.NewGuid();
        string relativeFileName = Path.Combine("Materials", "CachedAuthoringLit.material");

        var initialDocument = CreateAuthoringMaterialDocument(materialAssetId, "Cached Authoring Lit", 14.0f);
        scope.WriteAsset(relativeFileName, materialAssetId, "Cached Authoring Lit", initialDocument);

        var runtimeContext = new EngineRuntimeContext(
            new ProjectSettings(),
            scope.ProjectPath,
            AssetCatalog.Get,
            AssetCatalog.GetByFileName)
        {
            MaterialCache = new MaterialCache(),
        };

        var assetContentManager = CreateAssetContentManager(runtimeContext);
        bool loaded = MaterialRuntimeResolver.TryLoadRuntimeMaterial(materialAssetId, assetContentManager, out var firstRuntimeMaterial);

        Assert.True(loaded);

        var updatedDocument = CreateAuthoringMaterialDocument(materialAssetId, "Cached Authoring Lit", 37.5f);
        scope.WriteAsset(relativeFileName, materialAssetId, "Cached Authoring Lit", updatedDocument);

        bool loadedWithoutInvalidation = MaterialRuntimeResolver.TryLoadRuntimeMaterial(materialAssetId, assetContentManager, out var cachedRuntimeMaterial);

        Assert.True(loadedWithoutInvalidation);
        Assert.Same(firstRuntimeMaterial, cachedRuntimeMaterial);
        Assert.Equal(14.0f, Assert.IsType<LitDiffuseMaterial>(cachedRuntimeMaterial).SpecularPower);

        runtimeContext.MaterialCache!.Invalidate(materialAssetId);

        bool loadedAfterInvalidation = MaterialRuntimeResolver.TryLoadRuntimeMaterial(materialAssetId, assetContentManager, out var refreshedRuntimeMaterial);

        Assert.True(loadedAfterInvalidation);
        Assert.NotSame(firstRuntimeMaterial, refreshedRuntimeMaterial);
        Assert.Equal(37.5f, Assert.IsType<LitDiffuseMaterial>(refreshedRuntimeMaterial).SpecularPower);
    }

    [Fact]
    public void TryLoadRuntimeMaterial_WithCachedRuntimeMaterial_DoesNotRequireAssetReload()
    {
        var materialAsset = new MaterialAsset("lit-diffuse")
        {
            Name = "Cached Authoring Lit",
        };
        materialAsset.SetPropertyValue("base_color_texture", MaterialValue.FromTextureId(Guid.Empty));
        materialAsset.SetPropertyValue("normal_texture", MaterialValue.FromTextureId(Guid.Empty));
        materialAsset.SetPropertyValue("diffuse_color", MaterialValue.FromColor(Color.OrangeRed));
        materialAsset.SetPropertyValue("alpha_cutoff", MaterialValue.FromFloat(0.0f));
        materialAsset.SetPropertyValue("emissive_color", MaterialValue.FromVector3(Vector3.Zero));
        materialAsset.SetPropertyValue("specular_color", MaterialValue.FromVector3(new Vector3(0.5f)));
        materialAsset.SetPropertyValue("specular_power", MaterialValue.FromFloat(14.0f));

        var runtimeContext = new EngineRuntimeContext(
            new ProjectSettings(),
            string.Empty,
            _ => null,
            _ => null)
        {
            MaterialCache = new MaterialCache(),
        };

        var seedingAssetContentManager = CreateAssetContentManager(runtimeContext);
        var cachedRuntimeMaterial = runtimeContext.MaterialCache.GetOrCompileRuntimeMaterial(materialAsset, seedingAssetContentManager);

        var cachedOnlyAssetContentManager = CreateAssetContentManager(runtimeContext);
        bool loaded = MaterialRuntimeResolver.TryLoadRuntimeMaterial(materialAsset.Id, cachedOnlyAssetContentManager, out var runtimeMaterial);

        Assert.True(loaded);
        Assert.Same(cachedRuntimeMaterial, runtimeMaterial);
    }

    [Fact]
    public void MaterialAuthoringCache_ReusesCachedAssetUntilInvalidated()
    {
        using var scope = new TestProjectScope();
        Guid materialAssetId = Guid.NewGuid();
        string relativeFileName = Path.Combine("Materials", "CachedAuthoringSource.material");

        var initialDocument = CreateAuthoringMaterialDocument(materialAssetId, "Cached Authoring Source", 14.0f);
        scope.WriteAsset(relativeFileName, materialAssetId, "Cached Authoring Source", initialDocument);

        var runtimeContext = new EngineRuntimeContext(
            new ProjectSettings(),
            scope.ProjectPath,
            AssetCatalog.Get,
            AssetCatalog.GetByFileName)
        {
            MaterialAuthoringCache = new MaterialAuthoringAssetCache(),
        };

        var assetContentManager = CreateAssetContentManager(runtimeContext);
        var firstAuthoringMaterial = runtimeContext.MaterialAuthoringCache.GetOrLoad(materialAssetId, assetContentManager);

        var updatedDocument = CreateAuthoringMaterialDocument(materialAssetId, "Cached Authoring Source", 37.5f);
        scope.WriteAsset(relativeFileName, materialAssetId, "Cached Authoring Source", updatedDocument);

        var cachedAuthoringMaterial = runtimeContext.MaterialAuthoringCache.GetOrLoad(materialAssetId, assetContentManager);
        Assert.Same(firstAuthoringMaterial, cachedAuthoringMaterial);
        Assert.True(cachedAuthoringMaterial.TryGetPropertyValue("specular_power", out var cachedSpecularPower));
        Assert.True(cachedSpecularPower.TryGetFloat(out var cachedValue));
        Assert.Equal(14.0f, cachedValue);

        Assert.True(runtimeContext.MaterialAuthoringCache.Invalidate(materialAssetId));

        var refreshedAuthoringMaterial = runtimeContext.MaterialAuthoringCache.GetOrLoad(materialAssetId, assetContentManager);
        Assert.NotSame(firstAuthoringMaterial, refreshedAuthoringMaterial);
        Assert.True(refreshedAuthoringMaterial.TryGetPropertyValue("specular_power", out var refreshedSpecularPower));
        Assert.True(refreshedSpecularPower.TryGetFloat(out var refreshedValue));
        Assert.Equal(37.5f, refreshedValue);
    }

    [Fact]
    public void TryLoadRuntimeMaterial_WithSeededAuthoringCache_DoesNotRequireAssetReload()
    {
        var materialAsset = new MaterialAsset("lit-diffuse")
        {
            Name = "Seeded Authoring Lit",
        };
        Guid materialAssetId = materialAsset.Id;
        materialAsset.SetPropertyValue("diffuse_color", MaterialValue.FromColor(Color.OrangeRed));
        materialAsset.SetPropertyValue("specular_power", MaterialValue.FromFloat(27.5f));

        var runtimeContext = new EngineRuntimeContext(
            new ProjectSettings(),
            string.Empty,
            _ => null,
            _ => null)
        {
            MaterialAuthoringCache = new MaterialAuthoringAssetCache(),
        };
        runtimeContext.MaterialAuthoringCache.Set(materialAsset);

        var assetContentManager = CreateAssetContentManager(runtimeContext);
        bool loaded = MaterialRuntimeResolver.TryLoadRuntimeMaterial(materialAssetId, assetContentManager, out var runtimeMaterial);

        Assert.True(loaded);
        Assert.Equal(27.5f, Assert.IsType<LitDiffuseMaterial>(runtimeMaterial).SpecularPower);
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

    private static AssetContentManager CreateAssetContentManager(EngineRuntimeContext? runtimeContext = null)
    {
        var assetContentManager = new AssetContentManager();
        AssetLoaderRegistry.RegisterLoaders(assetContentManager);
        assetContentManager.RuntimeContext = runtimeContext;
        return assetContentManager;
    }

    private static JObject CreateAuthoringMaterialDocument(Guid materialAssetId, string name, float specularPower)
    {
        var materialAsset = new MaterialAsset("lit-diffuse")
        {
            Name = name,
        };
        materialAsset.SetPropertyValue("diffuse_color", MaterialValue.FromColor(Color.OrangeRed));
        materialAsset.SetPropertyValue("specular_power", MaterialValue.FromFloat(specularPower));

        var document = new JObject();
        MaterialAssetJsonSerializer.Save(materialAsset, document);
        document["id"] = materialAssetId.ToString();
        document["name"] = name;
        return document;
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

            if (AssetCatalog.Get(assetId) != null)
            {
                return;
            }

            EditorAssetCatalogService.Add(new AssetInfo(assetId)
            {
                Name = assetName,
                FileName = relativeFileName,
                AssetType = AssetInfo.InferAssetType(relativeFileName),
            });
        }
    }
}