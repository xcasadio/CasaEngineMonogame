using CasaEngine.Core.Logging;
using CasaEngine.EditorServices.Scaffolding;
using CasaEngine.EditorServices.Scripting;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Scene.World;

namespace CasaEngine.EditorServices;

public static class EditorProjectAuthoringService
{
    public static event EventHandler? ProjectLoaded;
    public static event EventHandler? ProjectClosed;

    public static void LoadProject(string fileName, EngineRuntimeContext? runtimeContext = null)
    {
        ClearProject();
        ProjectSettingsHelper.Load(fileName, runtimeContext);
        EditorProjectSession.CurrentProjectFilePath = fileName;

        var loadedProjectSettings = runtimeContext?.ProjectSettings ?? GameSettings.ProjectSettings;
        EnsureGameplayEnginePathProps(loadedProjectSettings);

        ProjectLoaded?.Invoke(GameSettings.ProjectSettings, EventArgs.Empty);
    }

    /// <summary>
    /// Regenerates the machine-local <c>CasaEngine.EnginePath.props</c> next to the gameplay csproj
    /// referenced by <paramref name="projectSettings"/> when the csproj exists on disk (see
    /// docs/editor/gameplay-csproj-scaffolding.md). This is what makes a scaffolded project portable
    /// from one machine to another: the props is the only file the editor regenerates. Fail-soft — a
    /// project without a gameplay csproj (assets-only project) or any error here must not prevent the
    /// project from opening.
    /// </summary>
    private static void EnsureGameplayEnginePathProps(ProjectSettings projectSettings)
    {
        if (string.IsNullOrWhiteSpace(projectSettings.GameplayCsprojName))
        {
            return;
        }

        try
        {
            string csprojPath = Path.GetFullPath(Path.Combine(EngineEnvironment.ProjectPath, projectSettings.GameplayCsprojName));
            if (!File.Exists(csprojPath))
            {
                return;
            }

            string gameplayDirectory = Path.GetDirectoryName(csprojPath)!;
            EditorGameplayProjectScaffolder.EnsureEnginePathProps(gameplayDirectory, AppContext.BaseDirectory);
        }
        catch (Exception ex)
        {
            Logs.WriteWarning($"Could not ensure the gameplay engine path props for project '{projectSettings.ProjectName}': {ex.Message}");
        }
    }

    public static void ClearProject()
    {
        EditorProjectSession.CurrentProjectFilePath = null;
        EditorAssetCatalogService.Clear();
        ProjectClosed?.Invoke(GameSettings.ProjectSettings, EventArgs.Empty);
    }

    /// <param name="buildGameplayProject">
    /// When true (default), runs a first fail-soft build of the scaffolded gameplay project
    /// right after creation so the canonical gameplay dll exists on disk immediately (see
    /// docs/editor/gameplay-csproj-scaffolding.md). Pass false to skip the build — the
    /// scaffolding itself always runs — e.g. in unit tests where the ~10-30s build is unwanted.
    /// </param>
    public static void CreateProject(string projectName, string path, EngineRuntimeContext? runtimeContext = null, bool buildGameplayProject = true)
    {
        var context = runtimeContext ?? GameSettings.CreateRuntimeContext();
        var projectSettings = context.ProjectSettings;

#if !DEBUG
        try
        {
#endif
            ClearProject();

            context.ProjectPath = path;
            EngineEnvironment.ProjectPath = path;

            var fullFileName = Path.Combine(path, projectName + Constants.FileNameExtensions.Project);
            projectSettings.WindowTitle = projectName;
            projectSettings.ProjectName = projectName;
            EditorProjectSession.CurrentProjectFilePath = fullFileName;

            var worldName = "DefaultWorld";
            var worldFileName = worldName + Constants.FileNameExtensions.World;
            projectSettings.FirstWorldLoaded = worldFileName;

            var world = new World
            {
                Name = worldName,
                FileName = worldFileName,
            };

            EditorWorldWriter.SaveWorld(world);
            EditorAssetCatalogService.Add(world);

            var scaffoldResult = EditorGameplayProjectScaffolder.Scaffold(path, projectName, AppContext.BaseDirectory);
            projectSettings.GameplayCsprojName = scaffoldResult.GameplayCsprojRelativePath;
            projectSettings.GameplayDllName = scaffoldResult.GameplayDllName;

            // CreateProject may run against an explicit runtimeContext whose ProjectSettings
            // differs from the process-wide GameSettings.ProjectSettings; SaveProject() below
            // (and the build below it) always persist/read GameSettings.ProjectSettings, so keep
            // it in sync regardless of which instance was used to author the project.
            if (!ReferenceEquals(projectSettings, GameSettings.ProjectSettings))
            {
                GameSettings.ProjectSettings.GameplayCsprojName = scaffoldResult.GameplayCsprojRelativePath;
                GameSettings.ProjectSettings.GameplayDllName = scaffoldResult.GameplayDllName;
            }

            SaveProject();
            EditorAssetCatalogService.Save();

            if (buildGameplayProject)
            {
                try
                {
                    // Build-only, no load: at this point in project creation, the editor's
                    // shadow-copy AssemblyManager.AssemblyLoader is not guaranteed to be wired
                    // (and never is for callers outside a running GameEditor), so loading here
                    // would risk locking the canonical dll on disk. Fail-soft: the project stays
                    // usable and the next Play retries the build (see design doc).
                    if (!EditorScriptReloadCoordinator.TryBuildCanonicalDll())
                    {
                        Logs.WriteWarning($"Initial gameplay build failed for project '{projectName}'; the project remains usable, Play will retry the build.");
                    }
                }
                catch (Exception ex)
                {
                    Logs.WriteWarning($"Initial gameplay build threw for project '{projectName}': {ex.Message}");
                }
            }

            ProjectLoaded?.Invoke(projectSettings, EventArgs.Empty);
#if !DEBUG
        }
        catch (Exception)
        {
        }
#endif
    }

    public static void SaveProject(World? world = null)
    {
        if (string.IsNullOrWhiteSpace(EditorProjectSession.CurrentProjectFilePath))
        {
            throw new InvalidOperationException("No editor project is currently open.");
        }

        if (world != null)
        {
            string worldFileName = world.FileName;
            if (string.IsNullOrWhiteSpace(worldFileName))
            {
                worldFileName = GameSettings.ProjectSettings.FirstWorldLoaded;
            }

            if (string.IsNullOrWhiteSpace(worldFileName))
            {
                throw new InvalidOperationException("The current world does not have a file name and cannot be saved.");
            }

            world.FileName = worldFileName;
            EditorWorldWriter.SaveWorld(world);
        }

        ProjectSettingsHelper.Save(EditorProjectSession.CurrentProjectFilePath, GameSettings.ProjectSettings);
    }
}