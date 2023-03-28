using System.Diagnostics;

namespace CasaEngine.Editor.ContentBuilder;

public class ContentBuilder : IDisposable
{
    // Temporary directories used by the content build.
    private string _buildDirectory;
    private string _processDirectory;
    private string _baseDirectory;

    // Generate unique directory names if there is more than one ContentBuilder.
    private static int _directorySalt;

    // Have we been disposed?
    private bool _isDisposed;

    //private ComboItemCollection Importers;

    public string OutputDirectory => Path.Combine(_buildDirectory, "bin/Content");

    public ContentBuilder()
    {
        CreateTempDirectory();
        CreateBuildProject();
    }

    ~ContentBuilder()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);

        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            _isDisposed = true;

            DeleteTempDirectory();
        }
    }

    private void CreateBuildProject()
    {
        //string projectPath = Path.Combine(buildDirectory, "content.contentproj");
        //string outputPath = Path.Combine(buildDirectory, "bin");
        //
        //projectRootElement = ProjectRootElement.Create(projectPath);
        //
        //projectRootElement.AddImport("$(MSBuildExtensionsPath)\\Microsoft\\XNA Game Studio\\" +
        //                             "v4.0\\Microsoft.Xna.GameStudio.ContentPipeline.targets");
        //
        //buildProject = new Microsoft.Build.Evaluation.Project(projectRootElement);
        //
        //buildProject.SetProperty("XnaPlatform", "Windows");
        //buildProject.SetProperty("XnaProfile", "Reach");
        //buildProject.SetProperty("XnaFrameworkVersion", "v4.0");
        //buildProject.SetProperty("Configuration", "Release");
        //buildProject.SetProperty("OutputPath", outputPath);
        //
        //foreach (string pipelineAssembly in pipelineAssemblies)
        //{
        //    buildProject.AddItem("Reference", pipelineAssembly);
        //}
        //
        //errorLogger = new ErrorLogger();
        //
        //buildParameters = new BuildParameters(ProjectCollection.GlobalProjectCollection);
        //buildParameters.Loggers = new ILogger[] { errorLogger };
    }

    /*public void Add(ComboItem item)
    {
        ComboItem importer = Importers.FindByName(System.IO.Path.GetExtension(item.Value).ToLower());
        this.Add(item.Value, System.IO.Path.GetFileNameWithoutExtension(item.Name), importer.Value, importer.Other);
    }*/

    private void CreateTempDirectory()
    {
        // Start with a standard base name:
        //
        //  %temp%\WinFormsContentLoading.ContentBuilder

        _baseDirectory = Path.Combine(Path.GetTempPath(), GetType().FullName);

        // Include our process ID, in case there is more than
        // one copy of the program running at the same time:
        //
        //  %temp%\WinFormsContentLoading.ContentBuilder\<ProcessId>

        var processId = Process.GetCurrentProcess().Id;

        _processDirectory = Path.Combine(_baseDirectory, processId.ToString());

        // Include a salt value, in case the program
        // creates more than one ContentBuilder instance:
        //
        //  %temp%\WinFormsContentLoading.ContentBuilder\<ProcessId>\<Salt>

        _directorySalt++;

        _buildDirectory = Path.Combine(_processDirectory, _directorySalt.ToString());

        // Create our temporary directory.
        Directory.CreateDirectory(_buildDirectory);

        PurgeStaleTempDirectories();
    }

    private void DeleteTempDirectory()
    {
        Directory.Delete(_buildDirectory, true);

        // If there are no other instances of ContentBuilder still using their
        // own temp directories, we can delete the process directory as well.
        if (Directory.GetDirectories(_processDirectory).Length == 0)
        {
            Directory.Delete(_processDirectory);

            // If there are no other copies of the program still using their
            // own temp directories, we can delete the base directory as well.
            if (Directory.GetDirectories(_baseDirectory).Length == 0)
            {
                Directory.Delete(_baseDirectory);
            }
        }
    }

    private void PurgeStaleTempDirectories()
    {
        // Check all subdirectories of our base location.
        foreach (var directory in Directory.GetDirectories(_baseDirectory))
        {
            // The subdirectory name is the ID of the process which created it.
            int processId;

            if (int.TryParse(Path.GetFileName(directory), out processId))
            {
                try
                {
                    // Is the creator process still running?
                    Process.GetProcessById(processId);
                }
                catch (ArgumentException)
                {
                    // If the process is gone, we can delete its temp directory.
                    Directory.Delete(directory, true);
                }
            }
        }
    }

}