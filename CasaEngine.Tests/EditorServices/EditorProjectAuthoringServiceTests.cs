using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Application;

using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Tests;
using Microsoft.Xna.Framework;
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

    [Fact]
    public void SaveProject_WithWorldEnvironment_PersistsAndReloadsEnvironmentSettings()
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

            world.EnvironmentSettings.Type = EnvironmentType.Cubemap;
            world.EnvironmentSettings.BackgroundMode = EnvironmentBackgroundMode.Environment;
            world.EnvironmentSettings.BackgroundColor = new Color(12, 34, 56, 78);
            world.EnvironmentSettings.EnvironmentAssetId = Guid.Parse("11111111-2222-3333-4444-555555555555");
            world.EnvironmentSettings.BackgroundCubemapAssetId = Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa");
            world.EnvironmentSettings.SpecularEnvironmentCubemapAssetId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
            world.EnvironmentSettings.AmbientColor = new Vector3(0.2f, 0.3f, 0.4f);
            world.EnvironmentSettings.AmbientIntensity = 1.5f;
            world.EnvironmentSettings.SpecularIntensity = 2.5f;

            EditorProjectAuthoringService.SaveProject(world);

            var worldDocument = JObject.Parse(File.ReadAllText(Path.Combine(tempDirectory, "DefaultWorld.world")));
            var environmentNode = Assert.IsType<JObject>(worldDocument["environment"]);
            Assert.Equal("Cubemap", (string?)environmentNode["type"]);
            Assert.Equal("Environment", (string?)environmentNode["background_mode"]);
            Assert.Equal("11111111-2222-3333-4444-555555555555", (string?)environmentNode["environment_asset_id"]);
            Assert.Equal("66666666-7777-8888-9999-aaaaaaaaaaaa", (string?)environmentNode["background_cubemap_asset_id"]);
            Assert.Equal("bbbbbbbb-cccc-dddd-eeee-ffffffffffff", (string?)environmentNode["specular_cubemap_asset_id"]);
            Assert.Equal(1.5f, (float?)environmentNode["ambient_intensity"]);
            Assert.Equal(2.5f, (float?)environmentNode["specular_intensity"]);

            var reloadedWorld = new World();
            reloadedWorld.Load(worldDocument);

            Assert.Equal(EnvironmentType.Cubemap, reloadedWorld.EnvironmentSettings.Type);
            Assert.Equal(EnvironmentBackgroundMode.Environment, reloadedWorld.EnvironmentSettings.BackgroundMode);
            Assert.Equal(new Color(12, 34, 56, 78), reloadedWorld.EnvironmentSettings.BackgroundColor);
            Assert.Equal(Guid.Parse("11111111-2222-3333-4444-555555555555"), reloadedWorld.EnvironmentSettings.EnvironmentAssetId);
            Assert.Equal(Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa"), reloadedWorld.EnvironmentSettings.BackgroundCubemapAssetId);
            Assert.Equal(Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"), reloadedWorld.EnvironmentSettings.SpecularEnvironmentCubemapAssetId);
            Assert.Equal(new Vector3(0.2f, 0.3f, 0.4f), reloadedWorld.EnvironmentSettings.AmbientColor);
            Assert.Equal(1.5f, reloadedWorld.EnvironmentSettings.AmbientIntensity);
            Assert.Equal(2.5f, reloadedWorld.EnvironmentSettings.SpecularIntensity);
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
