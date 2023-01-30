using System.Diagnostics;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace CasaEngine.Editor.Builder
{
    public class ContentBuilder
        : IDisposable
    {


        // What importers or processors should we load?
        const string xnaVersion = ", Version=4.0.0.0, PublicKeyToken=842cf8be1de50553";

        static string[] pipelineAssemblies =
        {
            "Microsoft.Xna.Framework.Content.Pipeline.FBXImporter" + xnaVersion,
            "Microsoft.Xna.Framework.Content.Pipeline.XImporter" + xnaVersion,
            "Microsoft.Xna.Framework.Content.Pipeline.TextureImporter" + xnaVersion,
            "Microsoft.Xna.Framework.Content.Pipeline.EffectImporter" + xnaVersion,
            "Microsoft.Xna.Framework.Content.Pipeline.AudioImporters" + xnaVersion,
            "Microsoft.Xna.Framework.Content.Pipeline.VideoImporters" + xnaVersion,

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
        Microsoft.Build.Evaluation.Project buildProject;
        ProjectRootElement projectRootElement;
        BuildParameters buildParameters;
        readonly List<ProjectItem> projectItems = new List<ProjectItem>();
        ErrorLogger errorLogger;


        // Temporary directories used by the content build.
        string buildDirectory;
        string processDirectory;
        string baseDirectory;


        // Generate unique directory names if there is more than one ContentBuilder.
        static int directorySalt;

        // Have we been disposed?
        bool isDisposed;

        //private ComboItemCollection Importers;


        public string OutputDirectory => Path.Combine(buildDirectory, "bin/Content");


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
            if (!isDisposed)
            {
                isDisposed = true;

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
            return this.Add(filename, name, null, null);
        }
        public ProjectItem Add(string filename, string name, string importer, string processor)
        {
            ProjectItem item = buildProject.AddItem("Compile", filename)[0];

            item.SetMetadataValue("Link", Path.GetFileName(filename));
            item.SetMetadataValue("Name", name);

            if (!string.IsNullOrEmpty(importer))
                item.SetMetadataValue("Importer", importer);

            if (!string.IsNullOrEmpty(processor))
                item.SetMetadataValue("Processor", processor);

            projectItems.Add(item);

            return item;
        }


        public void Clear()
        {
            buildProject.RemoveItems(projectItems);
            projectItems.Clear();
        }


        public string Build()
        {
            // Clear any previous errors.
            errorLogger.Errors.Clear();

            //BuildManager.DefaultBuildManager.BeginBuild(buildParameters);
            BuildRequestData request = new BuildRequestData(buildProject.CreateProjectInstance(), new string[0]);
            //BuildSubmission submission = BuildManager.DefaultBuildManager.PendBuildRequest(request);
            //BuildResult br = submission.Execute();
            //BuildManager.DefaultBuildManager.EndBuild();

            BuildResult br = BuildManager.DefaultBuildManager.Build(buildParameters, request);
            if (br.OverallResult == BuildResultCode.Failure)
            {
                return string.Join("\n", errorLogger.Errors.ToArray());
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

            baseDirectory = Path.Combine(Path.GetTempPath(), GetType().FullName);

            // Include our process ID, in case there is more than
            // one copy of the program running at the same time:
            //
            //  %temp%\WinFormsContentLoading.ContentBuilder\<ProcessId>

            int processId = Process.GetCurrentProcess().Id;

            processDirectory = Path.Combine(baseDirectory, processId.ToString());

            // Include a salt value, in case the program
            // creates more than one ContentBuilder instance:
            //
            //  %temp%\WinFormsContentLoading.ContentBuilder\<ProcessId>\<Salt>

            directorySalt++;

            buildDirectory = Path.Combine(processDirectory, directorySalt.ToString());

            // Create our temporary directory.
            Directory.CreateDirectory(buildDirectory);

            PurgeStaleTempDirectories();
        }


        void DeleteTempDirectory()
        {
            Directory.Delete(buildDirectory, true);

            // If there are no other instances of ContentBuilder still using their
            // own temp directories, we can delete the process directory as well.
            if (Directory.GetDirectories(processDirectory).Length == 0)
            {
                Directory.Delete(processDirectory);

                // If there are no other copies of the program still using their
                // own temp directories, we can delete the base directory as well.
                if (Directory.GetDirectories(baseDirectory).Length == 0)
                {
                    Directory.Delete(baseDirectory);
                }
            }
        }


        void PurgeStaleTempDirectories()
        {
            // Check all subdirectories of our base location.
            foreach (string directory in Directory.GetDirectories(baseDirectory))
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
