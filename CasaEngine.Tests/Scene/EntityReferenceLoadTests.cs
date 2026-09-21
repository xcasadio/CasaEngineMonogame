using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Scene.Entities;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Scene;

public class EntityReferenceLoadTests
{
    [Fact]
    public void Load_ReferencedEntity_AppliesTheReferenceNameToTheClone()
    {
        using var scope = new EntityAssetScope("AlundraCamera");

        var entityReference = new EntityReference
        {
            AssetId = scope.AssetId,
            Name = "camera",
        };

        entityReference.Load(scope.AssetContentManager);

        Assert.Equal("camera", entityReference.Entity.Name);
    }

    [Fact]
    public void Load_ReferencedEntityWithoutReferenceName_KeepsTheAssetName()
    {
        using var scope = new EntityAssetScope("AlundraCamera");

        var entityReference = new EntityReference
        {
            AssetId = scope.AssetId,
            Name = string.Empty,
        };

        entityReference.Load(scope.AssetContentManager);

        Assert.Equal("AlundraCamera", entityReference.Entity.Name);
    }

    /// <summary>
    /// ADR-0037 (P4): an inline entity (no asset id) is deserialized directly - <c>new Entity()</c> then
    /// <c>Load</c> - instead of going through <see cref="AssetContentManager"/>. This is the only path
    /// that never touches the asset manager at all, so an empty one (no registered loader) is enough.
    /// </summary>
    [Fact]
    public void Load_InlineEntity_DeserializesDirectlyWithoutTheAssetManager()
    {
        var inlineEntityId = Guid.NewGuid();
        var entityNode = new JObject
        {
            ["id"] = inlineEntityId,
            ["name"] = "InlineActor",
            ["root_component"] = null,
            ["components"] = new JArray(),
            ["script_class_name"] = null,
        };
        var referenceNode = new JObject
        {
            ["asset_id"] = Guid.Empty,
            ["entity"] = entityNode,
        };

        var entityReference = new EntityReference();
        entityReference.Load(referenceNode);
        entityReference.Load(new AssetContentManager());

        Assert.Equal(inlineEntityId, entityReference.Entity.Id);
        Assert.Equal("InlineActor", entityReference.Entity.Name);
    }

    private sealed class EntityAssetScope : IDisposable
    {
        private readonly string _projectPath;

        public EntityAssetScope(string assetName)
        {
            _projectPath = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_projectPath);

            AssetId = Guid.NewGuid();
            string relativeFileName = Path.Combine("Entities", $"{assetName}.entity");
            string fullFileName = Path.Combine(_projectPath, relativeFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(fullFileName)!);

            var entityNode = new JObject
            {
                ["id"] = AssetId,
                ["name"] = assetName,
                ["root_component"] = null,
                ["components"] = new JArray(),
                ["script_class_name"] = null,
            };
            File.WriteAllText(fullFileName, entityNode.ToString());

            var assetInfo = new AssetInfo(AssetId)
            {
                Name = assetName,
                FileName = relativeFileName,
            };

            var runtimeContext = new EngineRuntimeContext(
                new ProjectSettings(),
                _projectPath,
                _ => assetInfo);

            AssetContentManager = new AssetContentManager
            {
                RuntimeContext = runtimeContext,
            };
            AssetContentManager.RegisterAssetLoader(typeof(Entity), new AssetLoader<Entity>());
        }

        public Guid AssetId { get; }

        public AssetContentManager AssetContentManager { get; }

        public void Dispose()
        {
            Directory.Delete(_projectPath, recursive: true);
        }
    }
}
