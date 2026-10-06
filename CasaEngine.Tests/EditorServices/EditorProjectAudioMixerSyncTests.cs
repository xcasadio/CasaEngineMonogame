using CasaEngine.EditorServices;
using CasaEngine.EditorServices.Audio;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Tests.Audio;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>
/// Editor wiring of the project mixer asset (plan decision P48): <see cref="EditorProjectAudioMixerSync"/> applies the
/// loaded project's <see cref="ProjectSettings.AudioMixerAsset"/> on every real
/// <see cref="EditorProjectAuthoringService.LoadProject"/>, releases it on close, reports the applied id, and stops once disposed.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class EditorProjectAudioMixerSyncTests
{
    private const string MixerName = "Project mixer";

    [Fact]
    public void ProjectLoadedAndClosed_ApplyAndReleaseTheMixerAsset_AndStopAfterDispose()
    {
        string tempDirectory = CreateTempDirectory();
        string mixerProjectDirectory = Path.Combine(tempDirectory, "WithMixer");
        string plainProjectDirectory = Path.Combine(tempDirectory, "Plain");
        string mixerProjectFile = WriteProject(mixerProjectDirectory, MixerName, withMixer: true, out Guid assetId);
        string plainProjectFile = WriteProject(plainProjectDirectory, string.Empty, withMixer: false, out _);

        string previousProjectPath = EngineEnvironment.ProjectPath;
        var snapshot = ProjectSettingsSnapshot.Capture();

        try
        {
            using var service = new AudioService(new FakeAudioBackend());
            var assets = new AssetContentManager();
            AssetLoaderRegistry.RegisterLoaders(assets);
            var projectMixer = new ProjectAudioMixer(service, assets);
            var sync = new EditorProjectAudioMixerSync(projectMixer);
            var raised = new List<Guid>();
            sync.ProjectMixerApplied += (_, id) => raised.Add(id);

            try
            {
                EditorProjectAuthoringService.LoadProject(mixerProjectFile);
                Assert.Equal(assetId, projectMixer.AppliedAssetId);
                Assert.Equal(assetId, raised[^1]);
                Assert.Equal(0.5f, service.Mixer.GetBus("Sfx").Volume);
                Assert.True(service.Mixer.TryGetBus("Ambience", out _));

                // A project without the setting: nothing of the previous project's mixer stays.
                EditorProjectAuthoringService.LoadProject(plainProjectFile);
                Assert.Equal(Guid.Empty, projectMixer.AppliedAssetId);
                Assert.Equal(Guid.Empty, raised[^1]);
                Assert.False(projectMixer.Applier.IsApplied);
                Assert.Equal(1f, service.Mixer.GetBus("Sfx").Volume);

                EditorProjectAuthoringService.LoadProject(mixerProjectFile);
                Assert.Equal(assetId, projectMixer.AppliedAssetId);

                raised.Clear();
                EditorProjectAuthoringService.ClearProject();
                Assert.Equal(Guid.Empty, projectMixer.AppliedAssetId);
                Assert.False(projectMixer.Applier.IsApplied);
                Assert.Equal(1f, service.Mixer.GetBus("Sfx").Volume);
                Assert.Equal(new[] { Guid.Empty }, raised);

                sync.Dispose();
                sync.Dispose(); // idempotent
                raised.Clear();

                EditorProjectAuthoringService.LoadProject(mixerProjectFile);
                Assert.Equal(Guid.Empty, projectMixer.AppliedAssetId);
                Assert.Equal(1f, service.Mixer.GetBus("Sfx").Volume);
                Assert.Empty(raised);
            }
            finally
            {
                sync.Dispose();
            }
        }
        finally
        {
            EditorProjectAuthoringService.ClearProject();
            snapshot.Restore();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    /// <summary>Writes a project folder (project file, catalog and a mixer asset that sets Sfx to 0.5) and returns the project file.</summary>
    private static string WriteProject(string directory, string mixerSetting, bool withMixer, out Guid assetId)
    {
        Directory.CreateDirectory(directory);
        assetId = Guid.NewGuid();

        var asset = new AudioMixerAsset { Name = MixerName };
        asset.Buses.Add(new AudioMixerBusData { Name = "Sfx", Parent = "Master", Volume = 0.5f });
        asset.Buses.Add(new AudioMixerBusData { Name = "Ambience", Parent = "Music", Volume = 0.25f });
        var node = new JObject();
        EditorAudioMixerAssetJsonWriter.Save(asset, node);
        File.WriteAllText(Path.Combine(directory, "Project.audioMixer"), node.ToString());

        File.WriteAllText(
            Path.Combine(directory, "AssetInfos.json"),
            new JObject
            {
                ["asset_infos"] = new JArray(new JObject
                {
                    ["id"] = assetId.ToString(),
                    ["name"] = MixerName,
                    ["file_name"] = "Project.audioMixer",
                    ["asset_type"] = "audioMixer",
                }),
            }.ToString());

        var project = new JObject
        {
            ["WindowTitle"] = "Sample Project",
            ["ProjectName"] = "SampleProject",
            ["FirstScreenName"] = string.Empty,
            ["FirstWorldLoaded"] = "DefaultWorld.world",
            ["GameplayDllName"] = string.Empty,
        };
        if (withMixer)
        {
            project["AudioMixerAsset"] = mixerSetting;
        }

        string projectFile = Path.Combine(directory, "Project.json");
        File.WriteAllText(projectFile, project.ToString());
        return projectFile;
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private readonly record struct ProjectSettingsSnapshot(
        string WindowTitle,
        string ProjectName,
        string FirstScreenName,
        string FirstWorldLoaded,
        string GameplayDllName,
        string GameplayCsprojName,
        string DialogueScreenAsset,
        string AudioMixerAsset,
        bool IsAudioMuted,
        bool? IsMasterLimiterEnabled,
        AudioBackendKind? AudioBackend,
        VirtualResolutionSettings VirtualResolution)
    {
        public static ProjectSettingsSnapshot Capture()
        {
            var settings = GameSettings.ProjectSettings;
            return new ProjectSettingsSnapshot(
                settings.WindowTitle,
                settings.ProjectName,
                settings.FirstScreenName,
                settings.FirstWorldLoaded,
                settings.GameplayDllName,
                settings.GameplayCsprojName,
                settings.DialogueScreenAsset,
                settings.AudioMixerAsset,
                settings.IsAudioMuted,
                settings.IsMasterLimiterEnabled,
                settings.AudioBackend,
                settings.VirtualResolution);
        }

        public void Restore()
        {
            var settings = GameSettings.ProjectSettings;
            settings.WindowTitle = WindowTitle;
            settings.ProjectName = ProjectName;
            settings.FirstScreenName = FirstScreenName;
            settings.FirstWorldLoaded = FirstWorldLoaded;
            settings.GameplayDllName = GameplayDllName;
            settings.GameplayCsprojName = GameplayCsprojName;
            settings.DialogueScreenAsset = DialogueScreenAsset;
            settings.AudioMixerAsset = AudioMixerAsset;
            settings.IsAudioMuted = IsAudioMuted;
            settings.IsMasterLimiterEnabled = IsMasterLimiterEnabled;
            settings.AudioBackend = AudioBackend;
            settings.VirtualResolution = VirtualResolution;
        }
    }
}
