using CasaEngine.Engine;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Configuration.Project;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Configuration;

/// <summary>
/// <see cref="ProjectSettings.AudioBackend"/> is optional and additive: absent means null (never
/// the previous project's value) and the key is written only when set.
/// </summary>
// Mutates the global project settings and engine environment: serialized collection.
[Collection(ProjectEnvironmentCollection.Name)]
public class ProjectSettingsAudioBackendTests
{
    [Fact]
    public void Save_WithoutSetting_DoesNotWriteTheKey_AndLoadsAsNull()
    {
        WithTempProject((directory, fileName) =>
        {
            GameSettings.ProjectSettings.AudioBackend = null;
            ProjectSettingsHelper.Save(fileName);

            Assert.Null(JObject.Parse(File.ReadAllText(fileName))["AudioBackend"]);

            GameSettings.ProjectSettings.AudioBackend = AudioBackendKind.Software;
            ProjectSettingsHelper.Load(fileName);
            Assert.Null(GameSettings.ProjectSettings.AudioBackend);
        });
    }

    [Theory]
    [InlineData(AudioBackendKind.Software, "Software")]
    [InlineData(AudioBackendKind.MonoGame, "MonoGame")]
    public void Save_ThenLoad_RoundTripsTheSetting(AudioBackendKind kind, string expectedText)
    {
        WithTempProject((directory, fileName) =>
        {
            GameSettings.ProjectSettings.AudioBackend = kind;
            ProjectSettingsHelper.Save(fileName);

            Assert.Equal(expectedText, (string)JObject.Parse(File.ReadAllText(fileName))["AudioBackend"]);

            GameSettings.ProjectSettings.AudioBackend = null;
            ProjectSettingsHelper.Load(fileName);
            Assert.Equal(kind, GameSettings.ProjectSettings.AudioBackend);
        });
    }

    [Theory]
    [InlineData("OpenAL")]
    [InlineData("7")]
    public void Load_WithUnknownValue_IsNull(string value)
    {
        WithTempProject((directory, fileName) =>
        {
            ProjectSettingsHelper.Save(fileName);
            var document = JObject.Parse(File.ReadAllText(fileName));
            document["AudioBackend"] = value;
            File.WriteAllText(fileName, document.ToString());

            GameSettings.ProjectSettings.AudioBackend = AudioBackendKind.Software;
            ProjectSettingsHelper.Load(fileName);
            Assert.Null(GameSettings.ProjectSettings.AudioBackend);
        });
    }

    [Fact]
    public void Load_WithoutTheKey_IsNull_EvenAfterLoadingAProjectThatSetsIt()
    {
        WithTempProject((directory, fileName) =>
        {
            string withKey = Path.Combine(directory, "With.json");
            GameSettings.ProjectSettings.AudioBackend = AudioBackendKind.Software;
            ProjectSettingsHelper.Save(withKey);

            GameSettings.ProjectSettings.AudioBackend = null;
            ProjectSettingsHelper.Save(fileName);

            ProjectSettingsHelper.Load(withKey);
            Assert.Equal(AudioBackendKind.Software, GameSettings.ProjectSettings.AudioBackend);

            ProjectSettingsHelper.Load(fileName);
            Assert.Null(GameSettings.ProjectSettings.AudioBackend);
        });
    }

    private static void WithTempProject(Action<string, string> body)
    {
        string directory = Path.Combine(Path.GetTempPath(), "casa-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string fileName = Path.Combine(directory, "Project.json");

        string previousProjectPath = EngineEnvironment.ProjectPath;
        var previousAudioBackend = GameSettings.ProjectSettings.AudioBackend;
        try
        {
            body(directory, fileName);
        }
        finally
        {
            GameSettings.ProjectSettings.AudioBackend = previousAudioBackend;
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(directory, recursive: true);
        }
    }
}
