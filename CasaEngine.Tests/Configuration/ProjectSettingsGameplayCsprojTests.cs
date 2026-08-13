using CasaEngine.Engine;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Configuration.Project;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Configuration;

// Mutates the global project settings and engine environment: serialized collection.
[Collection(ProjectEnvironmentCollection.Name)]
public class ProjectSettingsGameplayCsprojTests
{
    [Fact]
    public void Save_WithoutCsproj_OmitsTheFieldAndLoadsBack()
    {
        string directory = Path.Combine(Path.GetTempPath(), "casa-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string fileName = Path.Combine(directory, "Project.json");

        string previousProjectPath = EngineEnvironment.ProjectPath;
        string previousCsproj = GameSettings.ProjectSettings.GameplayCsprojName;
        string previousDll = GameSettings.ProjectSettings.GameplayDllName;
        try
        {
            GameSettings.ProjectSettings.GameplayCsprojName = string.Empty;
            GameSettings.ProjectSettings.GameplayDllName = string.Empty;
            ProjectSettingsHelper.Save(fileName);

            var document = JObject.Parse(File.ReadAllText(fileName));
            Assert.Null(document["GameplayCsprojName"]);

            // A project file without the field (old format) loads without error.
            GameSettings.ProjectSettings.GameplayCsprojName = "stale.csproj";
            ProjectSettingsHelper.Load(fileName);
            Assert.Equal(string.Empty, GameSettings.ProjectSettings.GameplayCsprojName);
        }
        finally
        {
            GameSettings.ProjectSettings.GameplayCsprojName = previousCsproj;
            GameSettings.ProjectSettings.GameplayDllName = previousDll;
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_WithCsproj_RoundTrips()
    {
        string directory = Path.Combine(Path.GetTempPath(), "casa-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string fileName = Path.Combine(directory, "Project.json");

        string previousProjectPath = EngineEnvironment.ProjectPath;
        string previousCsproj = GameSettings.ProjectSettings.GameplayCsprojName;
        string previousDll = GameSettings.ProjectSettings.GameplayDllName;
        try
        {
            GameSettings.ProjectSettings.GameplayCsprojName = "Scripts/Game.csproj";
            GameSettings.ProjectSettings.GameplayDllName = string.Empty;
            ProjectSettingsHelper.Save(fileName);

            GameSettings.ProjectSettings.GameplayCsprojName = string.Empty;
            ProjectSettingsHelper.Load(fileName);

            Assert.Equal("Scripts/Game.csproj", GameSettings.ProjectSettings.GameplayCsprojName);
        }
        finally
        {
            GameSettings.ProjectSettings.GameplayCsprojName = previousCsproj;
            GameSettings.ProjectSettings.GameplayDllName = previousDll;
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(directory, recursive: true);
        }
    }
}
