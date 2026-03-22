using CasaEngine.Engine;
using CasaEngine.Framework;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Project;
using CasaEngine.Framework.World;
using Newtonsoft.Json;

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
        ProjectLoaded?.Invoke(GameSettings.ProjectSettings, EventArgs.Empty);
    }

    public static void ClearProject()
    {
        EditorProjectSession.CurrentProjectFilePath = null;
        EditorAssetCatalogService.Clear();
        ProjectClosed?.Invoke(GameSettings.ProjectSettings, EventArgs.Empty);
    }

    public static void CreateProject(string projectName, string path, EngineRuntimeContext? runtimeContext = null)
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

            SaveProject();
            EditorAssetCatalogService.Save();

            ProjectLoaded?.Invoke(projectSettings, EventArgs.Empty);
#if !DEBUG
        }
        catch (Exception)
        {
        }
#endif
    }

    public static void SaveProject()
    {
        if (string.IsNullOrWhiteSpace(EditorProjectSession.CurrentProjectFilePath))
        {
            throw new InvalidOperationException("No editor project is currently open.");
        }

        using StreamWriter file = File.CreateText(EditorProjectSession.CurrentProjectFilePath);
        using JsonTextWriter writer = new(file) { Formatting = Formatting.Indented };
        var jsonSerializer = new JsonSerializer();
        jsonSerializer.Serialize(writer, GameSettings.ProjectSettings);
    }
}