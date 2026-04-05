using CasaEngine.EditorServices;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Materials;
using CasaEngine.Tests;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Rendering;

[Collection(ProjectEnvironmentCollection.Name)]
public class MaterialDependencyIndexTests
{
    [Fact]
    public void GetAffectedMaterialAssetIds_IncludesTransitiveChildren()
    {
        using var scope = new TestProjectScope();
        Guid rootMaterialId = Guid.NewGuid();
        Guid childMaterialId = Guid.NewGuid();
        Guid grandChildMaterialId = Guid.NewGuid();
        Guid unrelatedMaterialId = Guid.NewGuid();

        scope.WriteMaterial(Path.Combine("Materials", "Root.material"), rootMaterialId, "Root", Guid.Empty);
        scope.WriteMaterial(Path.Combine("Materials", "Child.material"), childMaterialId, "Child", rootMaterialId);
        scope.WriteMaterial(Path.Combine("Materials", "GrandChild.material"), grandChildMaterialId, "GrandChild", childMaterialId);
        scope.WriteMaterial(Path.Combine("Materials", "Unrelated.material"), unrelatedMaterialId, "Unrelated", Guid.Empty);

        var dependencyIndex = new MaterialDependencyIndex();

        var affectedMaterialAssetIds = dependencyIndex.GetAffectedMaterialAssetIds(rootMaterialId);

        Assert.Equal(3, affectedMaterialAssetIds.Count);
        Assert.Contains(rootMaterialId, affectedMaterialAssetIds);
        Assert.Contains(childMaterialId, affectedMaterialAssetIds);
        Assert.Contains(grandChildMaterialId, affectedMaterialAssetIds);
        Assert.DoesNotContain(unrelatedMaterialId, affectedMaterialAssetIds);
    }

    [Fact]
    public void RefreshMaterialDependency_RewiresParentRelationshipWithoutRebuild()
    {
        using var scope = new TestProjectScope();
        Guid originalParentId = Guid.NewGuid();
        Guid newParentId = Guid.NewGuid();
        Guid childMaterialId = Guid.NewGuid();

        scope.WriteMaterial(Path.Combine("Materials", "Original.material"), originalParentId, "Original", Guid.Empty);
        scope.WriteMaterial(Path.Combine("Materials", "Replacement.material"), newParentId, "Replacement", Guid.Empty);
        string childRelativePath = Path.Combine("Materials", "Child.material");
        scope.WriteMaterial(childRelativePath, childMaterialId, "Child", originalParentId);

        var dependencyIndex = new MaterialDependencyIndex();
        Assert.Contains(childMaterialId, dependencyIndex.GetAffectedMaterialAssetIds(originalParentId));

        scope.WriteMaterial(childRelativePath, childMaterialId, "Child", newParentId);
        dependencyIndex.RefreshMaterialDependency(childMaterialId);

        Assert.DoesNotContain(childMaterialId, dependencyIndex.GetAffectedMaterialAssetIds(originalParentId));
        Assert.Contains(childMaterialId, dependencyIndex.GetAffectedMaterialAssetIds(newParentId));
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

        public void WriteMaterial(string relativeFileName, Guid assetId, string assetName, Guid parentMaterialAssetId)
        {
            var materialAsset = new MaterialAsset("lit-diffuse")
            {
                Name = assetName,
                ParentMaterialAssetId = parentMaterialAssetId,
            };

            var document = new JObject();
            MaterialAssetJsonSerializer.Save(materialAsset, document);
            document["id"] = assetId.ToString();
            document["name"] = assetName;

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