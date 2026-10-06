using CasaEngine.Engine;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Configuration.Project;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Configuration;

/// <summary><see cref="ProjectSettings.AudioMixerAsset"/> is optional: a project file without it (every file written before
/// it existed) loads with the default mixer, and it is written only when set.</summary>
// Mutates the global project settings and engine environment: serialized collection.
[Collection(ProjectEnvironmentCollection.Name)]
public class ProjectSettingsAudioMixerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0f4e3c1a-3a5b-4c55-9d1e-2b7f6a8c9d02")]
    [InlineData("Project mixer")]
    public void Save_WritesTheFieldOnlyWhenSet_AndLoadsItBack(string value)
    {
        string directory = CreateDirectory();
        string fileName = Path.Combine(directory, "Project.json");

        string previousProjectPath = EngineEnvironment.ProjectPath;
        string previousMixer = GameSettings.ProjectSettings.AudioMixerAsset;
        string previousDll = GameSettings.ProjectSettings.GameplayDllName;
        try
        {
            GameSettings.ProjectSettings.AudioMixerAsset = value;
            GameSettings.ProjectSettings.GameplayDllName = string.Empty;
            ProjectSettingsHelper.Save(fileName);

            var document = JObject.Parse(File.ReadAllText(fileName));
            Assert.Equal(string.IsNullOrWhiteSpace(value) ? null : value, (string)document["AudioMixerAsset"]);

            GameSettings.ProjectSettings.AudioMixerAsset = "stale";
            ProjectSettingsHelper.Load(fileName);
            Assert.Equal(string.IsNullOrWhiteSpace(value) ? string.Empty : value, GameSettings.ProjectSettings.AudioMixerAsset);
        }
        finally
        {
            GameSettings.ProjectSettings.AudioMixerAsset = previousMixer;
            GameSettings.ProjectSettings.GameplayDllName = previousDll;
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_AFileWithoutTheSetting_ResetsAStaleValue_AndSavesUnchanged()
    {
        string directory = CreateDirectory();
        string fileName = Path.Combine(directory, "Project.json");
        string resavedFileName = Path.Combine(directory, "Resaved.json");

        string previousProjectPath = EngineEnvironment.ProjectPath;
        string previousMixer = GameSettings.ProjectSettings.AudioMixerAsset;
        string previousDll = GameSettings.ProjectSettings.GameplayDllName;
        try
        {
            GameSettings.ProjectSettings.AudioMixerAsset = string.Empty;
            GameSettings.ProjectSettings.GameplayDllName = string.Empty;
            ProjectSettingsHelper.Save(fileName);
            Assert.DoesNotContain("AudioMixerAsset", File.ReadAllText(fileName));

            GameSettings.ProjectSettings.AudioMixerAsset = "previous project's mixer";
            ProjectSettingsHelper.Load(fileName);
            Assert.Equal(string.Empty, GameSettings.ProjectSettings.AudioMixerAsset);

            ProjectSettingsHelper.Save(resavedFileName);
            Assert.Equal(File.ReadAllText(fileName), File.ReadAllText(resavedFileName));
        }
        finally
        {
            GameSettings.ProjectSettings.AudioMixerAsset = previousMixer;
            GameSettings.ProjectSettings.GameplayDllName = previousDll;
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "casa-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
