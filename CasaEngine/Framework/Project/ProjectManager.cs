using CasaEngine.Framework.Game;

namespace CasaEngine.Framework.Project;

public class ProjectManager
{
    public string? ProjectPath
    {
        get
        {
#if EDITOR
            return Path.GetDirectoryName(ProjectFileOpened);
#else
            return Environment.CurrentDirectory;
#endif
        }
    }

    public void Load(string fileName)
    {
#if EDITOR
        Clear();
        ProjectFileOpened = fileName;
#endif

        GameSettings.ProjectSettings.Load(fileName);

        if (!string.IsNullOrWhiteSpace(GameSettings.ProjectSettings.GameplayDllName))
        {
            GameSettings.PluginManager.Load(GameSettings.ProjectSettings.GameplayDllName);
        }

#if EDITOR
        GameSettings.ExternalToolManager.Initialize(GameSettings.ProjectSettings.ExternalToolsDirectory);

        ProjectLoaded?.Invoke(this, EventArgs.Empty);
#endif
    }


#if EDITOR
    public event EventHandler? ProjectLoaded;
    public event EventHandler? ProjectClosed;

    public string? ProjectFileOpened { get; private set; }

    public void Clear()
    {
        GameSettings.ExternalToolManager.Clear();
        ProjectFileOpened = null;

        ProjectClosed?.Invoke(this, EventArgs.Empty);
    }

    public void CreateProject(string projectName, string path)
    {
#if !DEBUG
        try
        {
#endif

        var fullFileName = Path.Combine(path, projectName + ".json");

        Clear();

        ProjectFileOpened = fullFileName;
        CreateProjectDirectoryHierarchy(path);
        CreateDefaultItem(projectName, fullFileName);
        Save(fullFileName);

        ProjectLoaded?.Invoke(this, EventArgs.Empty);

#if !DEBUG
        }
        catch (System.Exception e)
        {

        }
#endif
    }

    private void CreateDefaultItem(string projectName, string fullFileName)
    {
        var projectPath = Path.GetDirectoryName(fullFileName);
        var world = new World.World { Name = "DefaultWorld" };
        world.FileName = world.Name;
        world.Save(projectPath);
        GameSettings.ProjectSettings.ProjectName = projectName;
        GameSettings.ProjectSettings.FirstWorldLoaded = world.FileName;
    }

    private void CreateProjectDirectoryHierarchy(string path)
    {
        //Directory.CreateDirectory(path + Path.DirectorySeparatorChar + AssetDirPath);
    }

    public bool Save(string fileName)
    {
        GameSettings.ProjectSettings.Save(fileName);
        return true;
    }

#endif
}