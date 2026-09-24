using CasaEngine.Engine;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Configuration.Project;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Configuration;

/// <summary><see cref="ProjectSettings.DialogueScreenAsset"/> is optional: a project file without it (every file
/// written before it existed) loads with the built-in dialogue box, and it is written only when set.</summary>
// Mutates the global project settings and engine environment: serialized collection.
[Collection(ProjectEnvironmentCollection.Name)]
public class ProjectSettingsDialogueScreenTests
{
    [Theory]
    [InlineData("")]
    [InlineData("0f4e3c1a-3a5b-4c55-9d1e-2b7f6a8c9d01")]
    public void Save_WritesTheFieldOnlyWhenSet_AndLoadsItBack(string value)
    {
        string directory = Path.Combine(Path.GetTempPath(), "casa-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string fileName = Path.Combine(directory, "Project.json");

        string previousProjectPath = EngineEnvironment.ProjectPath;
        string previousDialogue = GameSettings.ProjectSettings.DialogueScreenAsset;
        string previousDll = GameSettings.ProjectSettings.GameplayDllName;
        try
        {
            GameSettings.ProjectSettings.DialogueScreenAsset = value;
            GameSettings.ProjectSettings.GameplayDllName = string.Empty;
            ProjectSettingsHelper.Save(fileName);

            var document = JObject.Parse(File.ReadAllText(fileName));
            Assert.Equal(value.Length == 0 ? null : value, (string)document["DialogueScreenAsset"]);

            GameSettings.ProjectSettings.DialogueScreenAsset = "stale";
            ProjectSettingsHelper.Load(fileName);
            Assert.Equal(value, GameSettings.ProjectSettings.DialogueScreenAsset);
        }
        finally
        {
            GameSettings.ProjectSettings.DialogueScreenAsset = previousDialogue;
            GameSettings.ProjectSettings.GameplayDllName = previousDll;
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(directory, recursive: true);
        }
    }
}
