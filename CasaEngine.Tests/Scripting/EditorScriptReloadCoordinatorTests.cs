using CasaEngine.EditorServices.Scripting;
using CasaEngine.Framework.Application;
using Xunit;

namespace CasaEngine.Tests.Scripting;

// Reads the global project settings: serialized collection.
[Collection(ProjectEnvironmentCollection.Name)]
public class EditorScriptReloadCoordinatorTests
{
    [Fact]
    public void WithoutConfiguredCsproj_NothingIsOutOfDate()
    {
        string previous = GameSettings.ProjectSettings.GameplayCsprojName;
        try
        {
            GameSettings.ProjectSettings.GameplayCsprojName = string.Empty;

            Assert.False(EditorScriptReloadCoordinator.IsRebuildConfigured);
            Assert.False(EditorScriptReloadCoordinator.AreScriptsOutOfDate());
            Assert.False(EditorScriptReloadCoordinator.TryRebuildAndReload(null));
        }
        finally
        {
            GameSettings.ProjectSettings.GameplayCsprojName = previous;
        }
    }

    [Fact]
    public void WithConfiguredCsprojAndNoLoadedAssembly_ScriptsAreOutOfDate()
    {
        string previous = GameSettings.ProjectSettings.GameplayCsprojName;
        try
        {
            EditorScriptAssemblyService.Unload();
            GameSettings.ProjectSettings.GameplayCsprojName = "Scripts/Game.csproj";

            Assert.True(EditorScriptReloadCoordinator.IsRebuildConfigured);
            Assert.True(EditorScriptReloadCoordinator.AreScriptsOutOfDate());
        }
        finally
        {
            GameSettings.ProjectSettings.GameplayCsprojName = previous;
        }
    }
}
