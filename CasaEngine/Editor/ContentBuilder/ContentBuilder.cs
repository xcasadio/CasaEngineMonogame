using System.Diagnostics;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace CasaEngine.Editor.ContentBuilder
{
    public class ContentBuilder
        : IDisposable
    {


        // What importers or processors should we load?
        const string XnaVersion = ", Version=4.0.0.0, PublicKeyToken=842cf8be1de50553";

        static string[] _pipelineAssemblies =
        {
            "Microsoft.Xna.Framework.Content.Pipeline.FBXImporter" + XnaVersion,
            "Microsoft.Xna.Framework.Content.Pipeline.XImporter" + XnaVersion,
            "Microsoft.Xna.Framework.Content.Pipeline.TextureImporter" + XnaVersion,
            "Microsoft.Xna.Framework.Content.Pipeline.EffectImporter" + XnaVersion,
            "Microsoft.Xna.Framework.Content.Pipeline.AudioImporters" + XnaVersion,
            "Microsoft.Xna.Framework.Content.Pipeline.VideoImporters" + XnaVersion,

            // If you want to use custom importers or processors from
            // a Content Pipeline Extension Library, add them here.
            //
            // If your extension DLL is installed in the GAC, you should refer to it by assembly
            // name, eg. "MyPipelineExtension, Version=1.0.0.0, PublicKeyToken=1234567812345678".
            //
            // If the extension DLL is not in the GAC, you should refer to it by
            // file path, eg. "c:/MyProject/bin/MyPipelineExtension.dll".
        };
        // MSBuild objects used to dynamically build content.
        Microsoft.Build.Evaluation.Project _buildProject;
        ProjectRootElement _projectRootElement;
        BuildParameters _buildParameters;
        readonly List<ProjectItem> _projectItems = new();
        ErrorLogger _errorLogger;


        // Temporary directories used by the content build.
        string _buildDirectory;
        string _processDirectory;
        string _baseDirectory;


        // Generate unique directory names if there is more than one ContentBuilder.
        static int _directorySalt;

        // Have we been disposed?
        bool _isDisposed;

        //private ComboItemCollection Importers;


        public string OutputDirectory => Path.Combine(_buildDirectory, "bin/Content");


        public ContentBuilder()
        {
            CreateTempDirectory();
            CreateBuildProject();

            //Importers = new ComboItemCollection();
            //Seguindo a Ordem: Extensão, Importer, Processor

            /*Importers.Add(new ComboItem(".mp3", "Mp3Importer", "SongProcessor"));
            Importers.Add(new ComboItem(".wav", "WavImporter", "SoundEffectProcessor"));
            Importers.Add(new ComboItem(".wma", "WmaImporter", "SongProcessor"));

            Importers.Add(new ComboItem(".bmp", "TextureImporter", "TextureProcessor"));
            Importers.Add(new ComboItem(".jpg", "TextureImporter", "TextureProcessor"));
            Importers.Add(new ComboItem(".png", "TextureImporter", "TextureProcessor"));
            Importers.Add(new ComboItem(".tga", "TextureImporter", "TextureProcessor"));
            Importers.Add(new ComboItem(".dds", "TextureImporter", "TextureProcessor"));

            Importers.Add(new ComboItem(".spritefont", "FontDescriptionImporter", "FontDescriptionProcessor"));*/
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



        void CreateBuildProject()
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

        public ProjectItem Add(string filename, string name)
        {
            return Add(filename, name, null, null);
        }
        public ProjectItem Add(string filename, string name, string importer, string processor)
        {
            var item = _buildProject.AddItem("Compile", filename)[0];

            item.SetMetadataValue("Link", Path.GetFileName(filename));
            item.SetMetadataValue("Name", name);

            if (!string.IsNullOrEmpty(importer))
            {
                item.SetMetadataValue("Importer", importer);
            }

            if (!string.IsNullOrEmpty(processor))
            {
                item.SetMetadataValue("Processor", processor);
            }

            _projectItems.Add(item);

            return item;
        }


        public void Clear()
        {
            _buildProject.RemoveItems(_projectItems);
            _projectItems.Clear();
        }


        public string Build()
        {
            // Clear any previous errors.
            _errorLogger.Errors.Clear();

            //BuildManager.DefaultBuildManager.BeginBuild(buildParameters);
            var request = new BuildRequestData(_buildProject.CreateProjectInstance(), new string[0]);
            //BuildSubmission submission = BuildManager.DefaultBuildManager.PendBuildRequest(request);
            //BuildResult br = submission.Execute();
            //BuildManager.DefaultBuildManager.EndBuild();

            var br = BuildManager.DefaultBuildManager.Build(_buildParameters, request);
            if (br.OverallResult == BuildResultCode.Failure)
            {
                return string.Join("\n", _errorLogger.Errors.ToArray());
            }

            // If the build failed, return an error string.
            /*if (submission.BuildResult.OverallResult == BuildResultCode.Failure)
            {
                return string.Join("\n", errorLogger.Errors.ToArray());
            }*/

            return null;
        }

        private void BuildSubmissionCompleteCallback(BuildSubmission submission)
        {
        }





        void CreateTempDirectory()
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


        void DeleteTempDirectory()
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


        void PurgeStaleTempDirectories()
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
}
