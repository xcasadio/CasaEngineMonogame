using CasaEngine.EditorServices;
using CasaEngine.Engine;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Project;
using CasaEngine.Framework.World;
using CasaEngine.Tests;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

[Collection(ProjectEnvironmentCollection.Name)]
public class EditorProjectAuthoringServiceTests
{
    [Fact]
    public void SaveProject_WithCurrentWorld_PersistsWorldAndProjectFiles()
    {
        string tempDirectory = CreateTempDirectory();
        string projectFilePath = Path.Combine(tempDirectory, "SampleProject.json");

        string? previousProjectPath = EngineEnvironment.ProjectPath;
        string? previousProjectFilePath = EditorProjectSession.CurrentProjectFilePath;
        var snapshot = ProjectSettingsSnapshot.Capture();

        try
        {
            ConfigureProjectSettings(projectFilePath);
            ProjectSettingsHelper.Save(projectFilePath);
            EditorProjectAuthoringService.LoadProject(projectFilePath);

            var world = new World
            {
                Name = "DefaultWorld",
                FileName = "DefaultWorld.world",
                GameplayProxyClassName = "Gameplay.Proxy.WorldLogic",
            };

            EditorProjectAuthoringService.SaveProject(world);

            string worldFilePath = Path.Combine(tempDirectory, "DefaultWorld.world");
            Assert.True(File.Exists(worldFilePath));

            var worldDocument = JObject.Parse(File.ReadAllText(worldFilePath));
            Assert.Equal("Gameplay.Proxy.WorldLogic", (string?)worldDocument["script_class_name"]);
            Assert.NotNull(worldDocument["entity_references"]);

            var projectDocument = JObject.Parse(File.ReadAllText(projectFilePath));
            Assert.Equal("DefaultWorld.world", (string?)projectDocument["FirstWorldLoaded"]);
            Assert.Equal(true, (bool?)projectDocument["VSyncEnabled"]);
            Assert.Equal("ExternalTools", (string?)projectDocument["ExternalToolsDirectory"]);
        }
        finally
        {
            EditorProjectAuthoringService.ClearProject();
            RestoreProjectSettings(snapshot);
            EditorProjectSessionAccessor.TryRestoreProjectFilePath(previousProjectFilePath);
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void SaveProject_WithStaticModelOverrides_WritesSingleOverridesArray()
    {
        string tempDirectory = CreateTempDirectory();
        string projectFilePath = Path.Combine(tempDirectory, "SampleProject.json");

        string? previousProjectPath = EngineEnvironment.ProjectPath;
        string? previousProjectFilePath = EditorProjectSession.CurrentProjectFilePath;
        var snapshot = ProjectSettingsSnapshot.Capture();

        try
        {
            ConfigureProjectSettings(projectFilePath);
            ProjectSettingsHelper.Save(projectFilePath);
            EditorProjectAuthoringService.LoadProject(projectFilePath);

            var world = new World
            {
                Name = "DefaultWorld",
                FileName = "DefaultWorld.world",
            };

            var staticModelComponent = new StaticModelComponent();
            staticModelComponent.AddChildComponent(new StaticModelSubMeshComponent
            {
                Name = "GeneratedSubMesh",
                IsGeneratedFromModel = true,
            });

            var materialOverride = new MaterialSlotOverride
            {
                SlotName = "ground",
                SlotIndex = 0,
                MaterialAssetId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            };
            materialOverride.MaterialInstanceData.SetPropertyOverride("specular_power", MaterialPropertyType.Float, 12.5f);
            staticModelComponent.MaterialOverrides.Add(materialOverride);

            var entity = new Entity
            {
                Name = "ground",
                RootComponent = staticModelComponent,
            };

            var entityReference = new EntityReference
            {
                AssetId = Guid.Empty,
                Entity = entity,
            };

            AddEntityReference(world, entityReference);

            EditorProjectAuthoringService.SaveProject(world);

            var worldDocument = JObject.Parse(File.ReadAllText(Path.Combine(tempDirectory, "DefaultWorld.world")));
            var entityReferences = Assert.IsType<JArray>(worldDocument["entity_references"]);
            var entityNode = Assert.IsType<JObject>(Assert.IsType<JObject>(entityReferences[0])["entity"]);
            var rootComponentNode = Assert.IsType<JObject>(entityNode["root_component"]);
            var overridesArray = Assert.IsType<JArray>(rootComponentNode["material_slot_overrides"]);

            var overrideNode = Assert.IsType<JObject>(Assert.Single(overridesArray));
            Assert.Equal("ground", (string?)overrideNode["slot_name"]);
            Assert.Equal(0, (int?)overrideNode["slot_index"]);
            Assert.Equal("11111111-1111-1111-1111-111111111111", (string?)overrideNode["material_asset_id"]);

            var materialInstanceNode = Assert.IsType<JObject>(overrideNode["material_instance"]);
            var propertyOverridesNode = Assert.IsType<JObject>(materialInstanceNode["property_overrides"]);
            Assert.NotNull(propertyOverridesNode["specular_power"]);
        }
        finally
        {
            EditorProjectAuthoringService.ClearProject();
            RestoreProjectSettings(snapshot);
            EditorProjectSessionAccessor.TryRestoreProjectFilePath(previousProjectFilePath);
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static void ConfigureProjectSettings(string projectFilePath)
    {
        string projectDirectory = Path.GetDirectoryName(projectFilePath)!;
        EngineEnvironment.ProjectPath = projectDirectory;

        GameSettings.ProjectSettings.WindowTitle = "Sample Project";
        GameSettings.ProjectSettings.ProjectName = "SampleProject";
        GameSettings.ProjectSettings.FirstScreenName = string.Empty;
        GameSettings.ProjectSettings.AllowUserResizing = false;
        GameSettings.ProjectSettings.IsFixedTimeStep = false;
        GameSettings.ProjectSettings.IsMouseVisible = false;
        GameSettings.ProjectSettings.FirstWorldLoaded = "DefaultWorld.world";
        GameSettings.ProjectSettings.GameplayDllName = string.Empty;
        GameSettings.ProjectSettings.DebugIsFullScreen = false;
        GameSettings.ProjectSettings.VSyncEnabled = true;
        GameSettings.ProjectSettings.DebugWidth = 1024;
        GameSettings.ProjectSettings.DebugHeight = 768;
        GameSettings.ProjectSettings.ExternalToolsDirectory = "ExternalTools";
    }

    private static void RestoreProjectSettings(ProjectSettingsSnapshot snapshot)
    {
        GameSettings.ProjectSettings.WindowTitle = snapshot.WindowTitle;
        GameSettings.ProjectSettings.ProjectName = snapshot.ProjectName;
        GameSettings.ProjectSettings.FirstScreenName = snapshot.FirstScreenName;
        GameSettings.ProjectSettings.AllowUserResizing = snapshot.AllowUserResizing;
        GameSettings.ProjectSettings.IsFixedTimeStep = snapshot.IsFixedTimeStep;
        GameSettings.ProjectSettings.IsMouseVisible = snapshot.IsMouseVisible;
        GameSettings.ProjectSettings.FirstWorldLoaded = snapshot.FirstWorldLoaded;
        GameSettings.ProjectSettings.GameplayDllName = snapshot.GameplayDllName;
        GameSettings.ProjectSettings.DebugIsFullScreen = snapshot.DebugIsFullScreen;
        GameSettings.ProjectSettings.VSyncEnabled = snapshot.VSyncEnabled;
        GameSettings.ProjectSettings.DebugWidth = snapshot.DebugWidth;
        GameSettings.ProjectSettings.DebugHeight = snapshot.DebugHeight;
        GameSettings.ProjectSettings.ExternalToolsDirectory = snapshot.ExternalToolsDirectory;
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void AddEntityReference(World world, EntityReference entityReference)
    {
        var field = typeof(World).GetField("_entityReferences", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var entityReferences = Assert.IsType<List<EntityReference>>(field!.GetValue(world));
        entityReferences.Add(entityReference);
    }

    private readonly record struct ProjectSettingsSnapshot(
        string WindowTitle,
        string ProjectName,
        string FirstScreenName,
        bool AllowUserResizing,
        bool IsFixedTimeStep,
        bool IsMouseVisible,
        string FirstWorldLoaded,
        string GameplayDllName,
        bool DebugIsFullScreen,
        bool VSyncEnabled,
        int DebugWidth,
        int DebugHeight,
        string ExternalToolsDirectory)
    {
        public static ProjectSettingsSnapshot Capture()
        {
            return new ProjectSettingsSnapshot(
                GameSettings.ProjectSettings.WindowTitle,
                GameSettings.ProjectSettings.ProjectName,
                GameSettings.ProjectSettings.FirstScreenName,
                GameSettings.ProjectSettings.AllowUserResizing,
                GameSettings.ProjectSettings.IsFixedTimeStep,
                GameSettings.ProjectSettings.IsMouseVisible,
                GameSettings.ProjectSettings.FirstWorldLoaded,
                GameSettings.ProjectSettings.GameplayDllName,
                GameSettings.ProjectSettings.DebugIsFullScreen,
                GameSettings.ProjectSettings.VSyncEnabled,
                GameSettings.ProjectSettings.DebugWidth,
                GameSettings.ProjectSettings.DebugHeight,
                GameSettings.ProjectSettings.ExternalToolsDirectory);
        }
    }

    private static class EditorProjectSessionAccessor
    {
        public static void TryRestoreProjectFilePath(string? projectFilePath)
        {
            if (projectFilePath == null)
            {
                return;
            }

            EditorProjectAuthoringService.LoadProject(projectFilePath);
        }
    }
}
