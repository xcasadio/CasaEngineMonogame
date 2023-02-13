using System.ComponentModel;
using CasaEngine.Game;
using CasaEngineCommon.Config;
using CasaEngineCommon.Logger;

namespace CasaEngine.Editor.SourceControl
{
    public class SourceControlManager
    {

        private static SourceControlManager _instance;
        private const int Version = 1;
        private readonly string _file = "SourceControl.ini";
        private ISourceControl _sourceControl;
        private Dictionary<string, Dictionary<SourceControlKeyWord, string>> _filesStatus = new();




        [Category("Data Source Control")]
        public string Server
        {
            get;
            set;
        }

        [Category("Data Source Control")]
        public string User
        {
            get;
            set;
        }

        [Browsable(false)]
        public string Password
        {
            get;
            set;
        }

        [Category("Data Source Control")]
        public string Workspace
        {
            get;
            set;
        }

        [Category("Data Source Control")]
        public string Cwd
        {
            get;
            set;
        }


        public Dictionary<string, Dictionary<SourceControlKeyWord, string>> FilesStatus => _filesStatus;

        public ISourceControl SourceControl
        {
            get => _sourceControl;
            set => _sourceControl = value;
        }

        public static SourceControlManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SourceControlManager();
                    _instance.Server = "Server";
                    _instance.User = "User";
                    _instance.Password = "Password";
                    _instance.Workspace = "Workspace";
                }

                return _instance;
            }
        }



        private SourceControlManager()
        {
        }



        public void Initialize(ISourceControl control)
        {
            if (control == null)
            {
                throw new ArgumentNullException("SourceControlManager.Initialize() : ISourceControl is null");
            }

            _sourceControl = control;
        }

        public void CheckProjectFiles()
        {
            _filesStatus.Clear();

            if (string.IsNullOrWhiteSpace(Engine.Instance.ProjectManager.ProjectPath) == false)
            {
                var projectPath = Engine.Instance.ProjectManager.ProjectPath;

                try
                {
                    var files = Directory.GetFiles(projectPath, "*.*", SearchOption.AllDirectories);
                    _filesStatus = _sourceControl.FileStatus(files);
                }
                catch (Exception e)
                {
                    LogManager.Instance.WriteException(e, false);
                }
            }
        }

        public void LoadConfig(string path)
        {
            var filePath = Path.Combine(path, _file);

            if (File.Exists(filePath))
            {
                var ini = new IniFile(filePath);

                var version = int.Parse(ini.GetValue("Config", "Version"));
                Server = ini.GetValue("Config", "Server");
                User = ini.GetValue("Config", "User");
                Workspace = ini.GetValue("Config", "Workspace");
                Password = ini.GetValue("Config", "Password");
                Cwd = ini.GetValue("Config", "Directory");
            }
        }

        public void SaveConfig(string path)
        {
            var ini = new IniFile();
            ini.AddSection("Config", "Version", Version.ToString());
            ini.AddSection("Config", "Server", Server);
            ini.AddSection("Config", "User", User);
            ini.AddSection("Config", "Workspace", Workspace);
            ini.AddSection("Config", "Directory", Cwd);
            ini.Save(Path.Combine(path, _file));
        }

    }
}
