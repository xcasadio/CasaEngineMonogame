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
    public void SaveProject_WithoutOpenProject_ThrowsInvalidOperationException()
    {
        string? previousProjectPath = EngineEnvironment.ProjectPath;
        string? previousProjectFilePath = EditorProjectSession.CurrentProjectFilePath;
        var snapshot = ProjectSettingsSnapshot.Capture();

        try
        {
            EditorProjectAuthoringService.ClearProject();

            var exception = Assert.Throws<InvalidOperationException>(() => EditorProjectAuthoringService.SaveProject());

            Assert.Equal("No editor project is currently open.", exception.Message);
        }
        finally
        {
            RestoreProjectSettings(snapshot);
            EditorProjectSessionAccessor.TryRestoreProjectFilePath(previousProjectFilePath);
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    [Fact]
    public void SaveProject_WithWorldWithoutFileName_UsesProjectFallbackWorldFileName()
    {
        string tempDirectory = CreateTempDirectory();
        string projectFilePath = Path.Combine(tempDirectory, "SampleProject.json");

        string? previousProjectPath = EngineEnvironment.ProjectPath;
        string? previousProjectFilePath = EditorProjectSession.CurrentProjectFilePath;
        var snapshot = ProjectSettingsSnapshot.Capture();

        try
        {
            ConfigureProjectSettings(projectFilePath);
            GameSettings.ProjectSettings.FirstWorldLoaded = "FallbackWorld.world";
            ProjectSettingsHelper.Save(projectFilePath);
            EditorProjectAuthoringService.LoadProject(projectFilePath);

            var world = new World
            {
                Name = "FallbackWorld",
                FileName = string.Empty,
            };

            EditorProjectAuthoringService.SaveProject(world);

            string worldFilePath = Path.Combine(tempDirectory, "FallbackWorld.world");
            Assert.Equal("FallbackWorld.world", world.FileName);
            Assert.True(File.Exists(worldFilePath));

            var worldDocument = JObject.Parse(File.ReadAllText(worldFilePath));
            Assert.NotNull(worldDocument["entity_references"]);

            var projectDocument = JObject.Parse(File.ReadAllText(projectFilePath));
            Assert.Equal("FallbackWorld.world", (string?)projectDocument["FirstWorldLoaded"]);
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
    public void SaveProject_WithWorldWithoutAnyFileName_ThrowsInvalidOperationException()
    {
        string tempDirectory = CreateTempDirectory();
        string projectFilePath = Path.Combine(tempDirectory, "SampleProject.json");

        string? previousProjectPath = EngineEnvironment.ProjectPath;
        string? previousProjectFilePath = EditorProjectSession.CurrentProjectFilePath;
        var snapshot = ProjectSettingsSnapshot.Capture();

        try
        {
            ConfigureProjectSettings(projectFilePath);
            GameSettings.ProjectSettings.FirstWorldLoaded = string.Empty;
            ProjectSettingsHelper.Save(projectFilePath);
            EditorProjectAuthoringService.LoadProject(projectFilePath);

            var world = new World
            {
                Name = "UnsavedWorld",
                FileName = string.Empty,
            };

            var exception = Assert.Throws<InvalidOperationException>(() => EditorProjectAuthoringService.SaveProject(world));

            Assert.Equal("The current world does not have a file name and cannot be saved.", exception.Message);
            Assert.True(string.IsNullOrWhiteSpace(world.FileName));
            Assert.Empty(Directory.EnumerateFiles(tempDirectory, "*.world"));
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
    public void SaveProject_WithoutWorld_PersistsProjectOnly()
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

            EditorProjectAuthoringService.SaveProject(world);

            string worldFilePath = Path.Combine(tempDirectory, "DefaultWorld.world");
            string originalWorldContents = File.ReadAllText(worldFilePath);

            GameSettings.ProjectSettings.WindowTitle = "Updated Project Title";

            EditorProjectAuthoringService.SaveProject();

            Assert.Equal(originalWorldContents, File.ReadAllText(worldFilePath));

            var projectDocument = JObject.Parse(File.ReadAllText(projectFilePath));
            Assert.Equal("Updated Project Title", (string?)projectDocument["WindowTitle"]);
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
    public void SaveProject_WithLightComponent_PersistsCastShadowsFlag()
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

            var entity = new Entity
            {
                Name = "Sun",
                RootComponent = new LightComponent
                {
                    Type = LightType.Directional,
                    CastShadows = true,
                    Intensity = 3.0f,
                },
            };

            AddEntityReference(world, new EntityReference
            {
                AssetId = Guid.Empty,
                Entity = entity,
            });

            EditorProjectAuthoringService.SaveProject(world);

            var worldDocument = JObject.Parse(File.ReadAllText(Path.Combine(tempDirectory, "DefaultWorld.world")));
            var entityReferences = Assert.IsType<JArray>(worldDocument["entity_references"]);
            var entityNode = Assert.IsType<JObject>(Assert.IsType<JObject>(entityReferences[0])["entity"]);
            var rootComponentNode = Assert.IsType<JObject>(entityNode["root_component"]);

            Assert.Equal("LightComponent", (string?)rootComponentNode["type"]);
            Assert.True((bool?)rootComponentNode["cast_shadows"]);
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
    public void SaveProject_WithRenderComponents_PersistsShadowFlags()
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

            AddEntityReference(world, new EntityReference
            {
                AssetId = Guid.Empty,
                Entity = new Entity
                {
                    Name = "StaticEntity",
                    RootComponent = new StaticModelComponent
                    {
                        CastShadows = false,
                        ReceiveShadows = true,
                    },
                },
            });

            AddEntityReference(world, new EntityReference
            {
                AssetId = Guid.Empty,
                Entity = new Entity
                {
                    Name = "SkinnedEntity",
                    RootComponent = new SkinnedMeshComponent
                    {
                        CastShadows = true,
                        ReceiveShadows = false,
                    },
                },
            });

            EditorProjectAuthoringService.SaveProject(world);

            var worldDocument = JObject.Parse(File.ReadAllText(Path.Combine(tempDirectory, "DefaultWorld.world")));
            var entityReferences = Assert.IsType<JArray>(worldDocument["entity_references"]);

            JObject staticRoot = GetRootComponentNode(entityReferences, "StaticEntity");
            Assert.False((bool?)staticRoot["cast_shadows"]);
            Assert.True((bool?)staticRoot["receive_shadows"]);

            JObject skinnedRoot = GetRootComponentNode(entityReferences, "SkinnedEntity");
            Assert.True((bool?)skinnedRoot["cast_shadows"]);
            Assert.False((bool?)skinnedRoot["receive_shadows"]);
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
            world.EnvironmentSettings.Shadows.Enabled = true;
            world.EnvironmentSettings.Shadows.Resolution = 2048;
            world.EnvironmentSettings.Shadows.DepthBias = 0.0025f;
            world.EnvironmentSettings.Shadows.NormalBias = 0.01f;
            world.EnvironmentSettings.Shadows.MaxDistance = 42.0f;

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
            var shadowsNode = Assert.IsType<JObject>(environmentNode["shadows"]);
            Assert.True((bool?)shadowsNode["enabled"]);
            Assert.Equal(2048, (int?)shadowsNode["resolution"]);
            Assert.Equal(0.0025f, (float?)shadowsNode["depth_bias"]);
            Assert.Equal(0.01f, (float?)shadowsNode["normal_bias"]);
            Assert.Equal(42.0f, (float?)shadowsNode["max_distance"]);

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
            Assert.True(reloadedWorld.EnvironmentSettings.Shadows.Enabled);
            Assert.Equal(2048, reloadedWorld.EnvironmentSettings.Shadows.Resolution);
            Assert.Equal(0.0025f, reloadedWorld.EnvironmentSettings.Shadows.DepthBias);
            Assert.Equal(0.01f, reloadedWorld.EnvironmentSettings.Shadows.NormalBias);
            Assert.Equal(42.0f, reloadedWorld.EnvironmentSettings.Shadows.MaxDistance);
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
    public void CreateProject_WithoutBuild_ScaffoldsGameplayProjectAndWiresSettings()
    {
        string tempDirectory = CreateTempDirectory();

        string? previousProjectPath = EngineEnvironment.ProjectPath;
        string? previousProjectFilePath = EditorProjectSession.CurrentProjectFilePath;
        var snapshot = ProjectSettingsSnapshot.Capture();
        string previousCsproj = GameSettings.ProjectSettings.GameplayCsprojName;

        try
        {
            EditorProjectAuthoringService.CreateProject("SampleProject", tempDirectory, buildGameplayProject: false);

            string gameplayDirectory = Path.Combine(tempDirectory, "Gameplay");
            Assert.True(File.Exists(Path.Combine(gameplayDirectory, "SampleProject.Gameplay.csproj")));
            Assert.True(File.Exists(Path.Combine(gameplayDirectory, "CasaEngine.EnginePath.props")));
            Assert.True(File.Exists(Path.Combine(gameplayDirectory, "GamePlugin.cs")));
            Assert.True(File.Exists(Path.Combine(gameplayDirectory, "Scripts", "SampleProxy.cs")));
            Assert.True(File.Exists(Path.Combine(tempDirectory, "SampleProject.sln")));

            Assert.Equal("Gameplay/SampleProject.Gameplay.csproj", GameSettings.ProjectSettings.GameplayCsprojName);
            Assert.Equal("SampleProject.Gameplay.dll", GameSettings.ProjectSettings.GameplayDllName);

            string projectFilePath = Path.Combine(tempDirectory, "SampleProject.json");
            Assert.True(File.Exists(projectFilePath));

            var projectDocument = JObject.Parse(File.ReadAllText(projectFilePath));
            Assert.Equal("Gameplay/SampleProject.Gameplay.csproj", (string?)projectDocument["GameplayCsprojName"]);
            Assert.Equal("SampleProject.Gameplay.dll", (string?)projectDocument["GameplayDllName"]);

            // No build was requested: no canonical dll at the project root.
            Assert.False(File.Exists(Path.Combine(tempDirectory, "SampleProject.Gameplay.dll")));
        }
        finally
        {
            EditorProjectAuthoringService.ClearProject();
            RestoreProjectSettings(snapshot);
            GameSettings.ProjectSettings.GameplayCsprojName = previousCsproj;
            EditorProjectSessionAccessor.TryRestoreProjectFilePath(previousProjectFilePath);
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateProject_WhenGameplayCsprojAlreadyExists_DoesNotOverwriteAndKeepsSettingsWired()
    {
        string tempDirectory = CreateTempDirectory();

        string? previousProjectPath = EngineEnvironment.ProjectPath;
        string? previousProjectFilePath = EditorProjectSession.CurrentProjectFilePath;
        var snapshot = ProjectSettingsSnapshot.Capture();
        string previousCsproj = GameSettings.ProjectSettings.GameplayCsprojName;

        try
        {
            string gameplayDirectory = Path.Combine(tempDirectory, "Gameplay");
            Directory.CreateDirectory(gameplayDirectory);
            string csprojPath = Path.Combine(gameplayDirectory, "SampleProject.Gameplay.csproj");
            const string userOwnedContent = "<!-- user-edited, must not be overwritten -->";
            File.WriteAllText(csprojPath, userOwnedContent);

            EditorProjectAuthoringService.CreateProject("SampleProject", tempDirectory, buildGameplayProject: false);

            Assert.Equal(userOwnedContent, File.ReadAllText(csprojPath));
            Assert.False(File.Exists(Path.Combine(gameplayDirectory, "GamePlugin.cs")));
            Assert.False(File.Exists(Path.Combine(tempDirectory, "SampleProject.sln")));

            Assert.Equal("Gameplay/SampleProject.Gameplay.csproj", GameSettings.ProjectSettings.GameplayCsprojName);
            Assert.Equal("SampleProject.Gameplay.dll", GameSettings.ProjectSettings.GameplayDllName);

            string projectFilePath = Path.Combine(tempDirectory, "SampleProject.json");
            var projectDocument = JObject.Parse(File.ReadAllText(projectFilePath));
            Assert.Equal("Gameplay/SampleProject.Gameplay.csproj", (string?)projectDocument["GameplayCsprojName"]);
            Assert.Equal("SampleProject.Gameplay.dll", (string?)projectDocument["GameplayDllName"]);
        }
        finally
        {
            EditorProjectAuthoringService.ClearProject();
            RestoreProjectSettings(snapshot);
            GameSettings.ProjectSettings.GameplayCsprojName = previousCsproj;
            EditorProjectSessionAccessor.TryRestoreProjectFilePath(previousProjectFilePath);
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void LoadProject_WhenEnginePathPropsIsMissing_RewritesItWithCurrentEnginePath()
    {
        string tempDirectory = CreateTempDirectory();

        string? previousProjectPath = EngineEnvironment.ProjectPath;
        string? previousProjectFilePath = EditorProjectSession.CurrentProjectFilePath;
        var snapshot = ProjectSettingsSnapshot.Capture();
        string previousCsproj = GameSettings.ProjectSettings.GameplayCsprojName;

        try
        {
            EditorProjectAuthoringService.CreateProject("SampleProject", tempDirectory, buildGameplayProject: false);

            string propsPath = Path.Combine(tempDirectory, "Gameplay", "CasaEngine.EnginePath.props");
            Assert.True(File.Exists(propsPath));
            File.Delete(propsPath);

            string projectFilePath = Path.Combine(tempDirectory, "SampleProject.json");
            EditorProjectAuthoringService.LoadProject(projectFilePath);

            Assert.True(File.Exists(propsPath));
            string propsContent = File.ReadAllText(propsPath);
            Assert.Contains(AppContext.BaseDirectory, propsContent);
        }
        finally
        {
            EditorProjectAuthoringService.ClearProject();
            RestoreProjectSettings(snapshot);
            GameSettings.ProjectSettings.GameplayCsprojName = previousCsproj;
            EditorProjectSessionAccessor.TryRestoreProjectFilePath(previousProjectFilePath);
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void LoadProject_WhenEnginePathPropsIsCorrupted_RewritesItWithCurrentEnginePath()
    {
        string tempDirectory = CreateTempDirectory();

        string? previousProjectPath = EngineEnvironment.ProjectPath;
        string? previousProjectFilePath = EditorProjectSession.CurrentProjectFilePath;
        var snapshot = ProjectSettingsSnapshot.Capture();
        string previousCsproj = GameSettings.ProjectSettings.GameplayCsprojName;

        try
        {
            EditorProjectAuthoringService.CreateProject("SampleProject", tempDirectory, buildGameplayProject: false);

            string propsPath = Path.Combine(tempDirectory, "Gameplay", "CasaEngine.EnginePath.props");
            Assert.True(File.Exists(propsPath));

            string staleEnginePath = Path.Combine(tempDirectory, "NoEngineHere");
            Directory.CreateDirectory(staleEnginePath);
            File.WriteAllText(propsPath, $"""
                <Project>
                  <PropertyGroup>
                    <CasaEnginePath Condition="'$(CASAENGINE_PATH)' != ''">$(CASAENGINE_PATH)</CasaEnginePath>
                    <CasaEnginePath Condition="'$(CasaEnginePath)' == ''">{staleEnginePath}</CasaEnginePath>
                  </PropertyGroup>
                </Project>
                """);

            string projectFilePath = Path.Combine(tempDirectory, "SampleProject.json");
            EditorProjectAuthoringService.LoadProject(projectFilePath);

            string propsContent = File.ReadAllText(propsPath);
            Assert.Contains(AppContext.BaseDirectory, propsContent);
            Assert.DoesNotContain(staleEnginePath, propsContent);
        }
        finally
        {
            EditorProjectAuthoringService.ClearProject();
            RestoreProjectSettings(snapshot);
            GameSettings.ProjectSettings.GameplayCsprojName = previousCsproj;
            EditorProjectSessionAccessor.TryRestoreProjectFilePath(previousProjectFilePath);
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void LoadProject_WhenEnginePathPropsIsValid_DoesNotRewriteIt()
    {
        string tempDirectory = CreateTempDirectory();

        string? previousProjectPath = EngineEnvironment.ProjectPath;
        string? previousProjectFilePath = EditorProjectSession.CurrentProjectFilePath;
        var snapshot = ProjectSettingsSnapshot.Capture();
        string previousCsproj = GameSettings.ProjectSettings.GameplayCsprojName;

        try
        {
            EditorProjectAuthoringService.CreateProject("SampleProject", tempDirectory, buildGameplayProject: false);

            string propsPath = Path.Combine(tempDirectory, "Gameplay", "CasaEngine.EnginePath.props");
            Assert.True(File.Exists(propsPath));
            string originalContent = File.ReadAllText(propsPath);
            DateTime originalWriteTimeUtc = File.GetLastWriteTimeUtc(propsPath);

            string projectFilePath = Path.Combine(tempDirectory, "SampleProject.json");
            EditorProjectAuthoringService.LoadProject(projectFilePath);

            Assert.Equal(originalContent, File.ReadAllText(propsPath));
            Assert.Equal(originalWriteTimeUtc, File.GetLastWriteTimeUtc(propsPath));
        }
        finally
        {
            EditorProjectAuthoringService.ClearProject();
            RestoreProjectSettings(snapshot);
            GameSettings.ProjectSettings.GameplayCsprojName = previousCsproj;
            EditorProjectSessionAccessor.TryRestoreProjectFilePath(previousProjectFilePath);
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void LoadProject_WithoutGameplayCsproj_DoesNotCreateGameplayFolderAndDoesNotThrow()
    {
        string tempDirectory = CreateTempDirectory();
        string projectFilePath = Path.Combine(tempDirectory, "SampleProject.json");

        string? previousProjectPath = EngineEnvironment.ProjectPath;
        string? previousProjectFilePath = EditorProjectSession.CurrentProjectFilePath;
        var snapshot = ProjectSettingsSnapshot.Capture();

        try
        {
            ConfigureProjectSettings(projectFilePath);
            GameSettings.ProjectSettings.GameplayCsprojName = string.Empty;
            ProjectSettingsHelper.Save(projectFilePath);

            var exception = Record.Exception(() => EditorProjectAuthoringService.LoadProject(projectFilePath));

            Assert.Null(exception);
            Assert.False(Directory.Exists(Path.Combine(tempDirectory, "Gameplay")));
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

    private static JObject GetRootComponentNode(JArray entityReferences, string entityName)
    {
        for (int i = 0; i < entityReferences.Count; i++)
        {
            if (entityReferences[i] is not JObject entityReferenceNode
                || entityReferenceNode["entity"] is not JObject entityNode
                || !string.Equals((string?)entityNode["name"], entityName, StringComparison.Ordinal)
                || entityNode["root_component"] is not JObject rootComponentNode)
            {
                continue;
            }

            return rootComponentNode;
        }

        throw new Xunit.Sdk.XunitException($"Unable to find entity '{entityName}' in the saved world document.");
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
