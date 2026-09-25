using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Configuration.Project;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>
/// Single proof of the editor wiring (ADR-0040): <see cref="EditorProjectAudioMuteSync"/> applies
/// the loaded project's <see cref="ProjectSettings.IsAudioMuted"/> to its mixer's Master bus on
/// every real <see cref="EditorProjectAuthoringService.LoadProject"/>, and stops once disposed.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class EditorProjectAudioMuteSyncTests
{
    [Fact]
    public void ProjectLoaded_AppliesTheProjectMuteToTheMixer_AndStopsAfterDispose()
    {
        string tempDirectory = CreateTempDirectory();
        string mutedProjectFilePath = Path.Combine(tempDirectory, "MutedProject.json");
        string plainProjectFilePath = Path.Combine(tempDirectory, "PlainProject.json");

        string? previousProjectPath = EngineEnvironment.ProjectPath;
        var snapshot = ProjectSettingsSnapshot.Capture();

        try
        {
            var mixer = AudioBusNames.CreateDefaultMixer();
            var sync = new EditorProjectAudioMuteSync(mixer);

            try
            {
                ConfigureProjectSettings(mutedProjectFilePath);
                GameSettings.ProjectSettings.IsAudioMuted = true;
                ProjectSettingsHelper.Save(mutedProjectFilePath);

                ConfigureProjectSettings(plainProjectFilePath);
                GameSettings.ProjectSettings.IsAudioMuted = false;
                ProjectSettingsHelper.Save(plainProjectFilePath);

                EditorProjectAuthoringService.LoadProject(mutedProjectFilePath);
                Assert.True(mixer.GetBus(AudioBusNames.Master).IsMuted);

                EditorProjectAuthoringService.LoadProject(plainProjectFilePath);
                Assert.False(mixer.GetBus(AudioBusNames.Master).IsMuted);

                sync.Dispose();
                sync.Dispose(); // idempotent

                EditorProjectAuthoringService.LoadProject(mutedProjectFilePath);
                Assert.False(mixer.GetBus(AudioBusNames.Master).IsMuted);
            }
            finally
            {
                sync.Dispose();
            }
        }
        finally
        {
            EditorProjectAuthoringService.ClearProject();
            RestoreProjectSettings(snapshot);
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
        GameSettings.ProjectSettings.IsAudioMuted = snapshot.IsAudioMuted;
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
        bool AllowUserResizing,
        bool IsFixedTimeStep,
        bool IsMouseVisible,
        string FirstWorldLoaded,
        string GameplayDllName,
        bool DebugIsFullScreen,
        bool VSyncEnabled,
        int DebugWidth,
        int DebugHeight,
        string ExternalToolsDirectory,
        bool IsAudioMuted)
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
                GameSettings.ProjectSettings.ExternalToolsDirectory,
                GameSettings.ProjectSettings.IsAudioMuted);
        }
    }
}
