using CasaEngine.Engine;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Configuration.Project;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Configuration;

/// <summary><see cref="ProjectSettings.UIInputMapping"/> is optional (ADR-0080): a project file without it keeps MGUI's
/// own input, and it is written only when set.</summary>
// Mutates the global project settings and engine environment: serialized collection.
[Collection(ProjectEnvironmentCollection.Name)]
public class ProjectSettingsUIInputMappingTests
{
    [Theory]
    [InlineData("")]
    [InlineData("ui-mapping")]
    [InlineData("15eedf05-d2ad-4a26-b7f2-1f95c482fb17")]
    public void Save_WritesTheFieldOnlyWhenSet_AndLoadsItBack(string value)
    {
        string directory = Path.Combine(Path.GetTempPath(), "casa-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string fileName = Path.Combine(directory, "Project.json");

        string previousProjectPath = EngineEnvironment.ProjectPath;
        string previousMapping = GameSettings.ProjectSettings.UIInputMapping;
        string previousDll = GameSettings.ProjectSettings.GameplayDllName;
        try
        {
            GameSettings.ProjectSettings.UIInputMapping = value;
            GameSettings.ProjectSettings.GameplayDllName = string.Empty;
            ProjectSettingsHelper.Save(fileName);

            var document = JObject.Parse(File.ReadAllText(fileName));
            Assert.Equal(value.Length == 0 ? null : value, (string?)document["UIInputMapping"]);

            GameSettings.ProjectSettings.UIInputMapping = "stale";
            ProjectSettingsHelper.Load(fileName);
            Assert.Equal(value, GameSettings.ProjectSettings.UIInputMapping);
        }
        finally
        {
            GameSettings.ProjectSettings.UIInputMapping = previousMapping;
            GameSettings.ProjectSettings.GameplayDllName = previousDll;
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(directory, recursive: true);
        }
    }
}
