using CasaEngine.Engine;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Configuration.Project;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Configuration;

/// <summary>
/// <see cref="ProjectSettings.IsAudioMuted"/> is optional (ADR-0040): a project file without it
/// (every file written before it existed, or a project that is not muted) loads as unmuted, and
/// the key is written only when the setting is true, so a non-muted project keeps a byte-identical
/// file.
/// </summary>
// Mutates the global project settings and engine environment: serialized collection.
[Collection(ProjectEnvironmentCollection.Name)]
public class ProjectSettingsAudioMuteTests
{
    [Fact]
    public void Save_ThenLoad_WithMutedProject_RoundTripsTrue()
    {
        string directory = Path.Combine(Path.GetTempPath(), "casa-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string fileName = Path.Combine(directory, "Project.json");

        string previousProjectPath = EngineEnvironment.ProjectPath;
        bool previousIsAudioMuted = GameSettings.ProjectSettings.IsAudioMuted;
        try
        {
            GameSettings.ProjectSettings.IsAudioMuted = true;
            ProjectSettingsHelper.Save(fileName);

            var document = JObject.Parse(File.ReadAllText(fileName));
            Assert.Equal(true, (bool?)document["IsAudioMuted"]);

            GameSettings.ProjectSettings.IsAudioMuted = false;
            ProjectSettingsHelper.Load(fileName);
            Assert.True(GameSettings.ProjectSettings.IsAudioMuted);
        }
        finally
        {
            GameSettings.ProjectSettings.IsAudioMuted = previousIsAudioMuted;
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_WithNonMutedProject_DoesNotWriteTheKey()
    {
        string directory = Path.Combine(Path.GetTempPath(), "casa-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string fileName = Path.Combine(directory, "Project.json");

        string previousProjectPath = EngineEnvironment.ProjectPath;
        bool previousIsAudioMuted = GameSettings.ProjectSettings.IsAudioMuted;
        try
        {
            GameSettings.ProjectSettings.IsAudioMuted = false;
            ProjectSettingsHelper.Save(fileName);

            var document = JObject.Parse(File.ReadAllText(fileName));
            Assert.Null(document["IsAudioMuted"]);
        }
        finally
        {
            GameSettings.ProjectSettings.IsAudioMuted = previousIsAudioMuted;
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_WithoutTheKey_IsFalse_EvenAfterLoadingAMutedProject()
    {
        string directory = Path.Combine(Path.GetTempPath(), "casa-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string mutedFileName = Path.Combine(directory, "Muted.json");
        string plainFileName = Path.Combine(directory, "Plain.json");

        string previousProjectPath = EngineEnvironment.ProjectPath;
        bool previousIsAudioMuted = GameSettings.ProjectSettings.IsAudioMuted;
        try
        {
            GameSettings.ProjectSettings.IsAudioMuted = true;
            ProjectSettingsHelper.Save(mutedFileName);

            GameSettings.ProjectSettings.IsAudioMuted = false;
            ProjectSettingsHelper.Save(plainFileName);

            var document = JObject.Parse(File.ReadAllText(plainFileName));
            Assert.Null(document["IsAudioMuted"]);

            // Load the muted project first so the field starts true, then load the project
            // without the key: absent must mean false, not "keep the previous value".
            ProjectSettingsHelper.Load(mutedFileName);
            Assert.True(GameSettings.ProjectSettings.IsAudioMuted);

            ProjectSettingsHelper.Load(plainFileName);
            Assert.False(GameSettings.ProjectSettings.IsAudioMuted);
        }
        finally
        {
            GameSettings.ProjectSettings.IsAudioMuted = previousIsAudioMuted;
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(directory, recursive: true);
        }
    }
}
