using CasaEngine.Game;
using CasaEngineCommon.Logger;
using System.ComponentModel;
using CasaEngineCommon.Config;

namespace CasaEngine.SourceControl
{
    public class SourceControlManager
    {

        static private SourceControlManager m_Instance;
        private const int m_Version = 1;
        private readonly string m_File = "SourceControl.ini";
        private ISourceControl m_SourceControl;
        private Dictionary<string, Dictionary<SourceControlKeyWord, string>> m_FilesStatus = new Dictionary<string, Dictionary<SourceControlKeyWord, string>>();




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
        public string CWD
        {
            get;
            set;
        }


        public Dictionary<string, Dictionary<SourceControlKeyWord, string>> FilesStatus => m_FilesStatus;

        public ISourceControl SourceControl
        {
            get => m_SourceControl;
            set => m_SourceControl = value;
        }

        static public SourceControlManager Instance
        {
            get
            {
                if (m_Instance == null)
                {
                    m_Instance = new SourceControlManager();
                    m_Instance.Server = "Server";
                    m_Instance.User = "User";
                    m_Instance.Password = "Password";
                    m_Instance.Workspace = "Workspace";
                }

                return m_Instance;
            }
        }



        private SourceControlManager()
        {
        }



        public void Initialize(ISourceControl control_)
        {
            if (control_ == null)
            {
                throw new ArgumentNullException("SourceControlManager.Initialize() : ISourceControl is null");
            }

            m_SourceControl = control_;
        }

        public void CheckProjectFiles()
        {
            m_FilesStatus.Clear();

            if (string.IsNullOrWhiteSpace(Engine.Instance.ProjectManager.ProjectPath) == false)
            {
                string projectPath = Engine.Instance.ProjectManager.ProjectPath;

                try
                {
                    string[] files = Directory.GetFiles(projectPath, "*.*", SearchOption.AllDirectories);
                    m_FilesStatus = m_SourceControl.FileStatus(files);
                }
                catch (System.Exception e)
                {
                    LogManager.Instance.WriteException(e, false);
                }
            }
        }

        public void LoadConfig(string path_)
        {
            string filePath = Path.Combine(path_, m_File);

            if (File.Exists(filePath) == true)
            {
                IniFile ini = new IniFile(filePath);

                int version = Int32.Parse(ini.GetValue("Config", "Version"));
                Server = ini.GetValue("Config", "Server");
                User = ini.GetValue("Config", "User");
                Workspace = ini.GetValue("Config", "Workspace");
                Password = ini.GetValue("Config", "Password");
                CWD = ini.GetValue("Config", "Directory");
            }
        }

        public void SaveConfig(string path_)
        {
            IniFile ini = new IniFile();
            ini.AddSection("Config", "Version", m_Version.ToString());
            ini.AddSection("Config", "Server", Server);
            ini.AddSection("Config", "User", User);
            ini.AddSection("Config", "Workspace", Workspace);
            ini.AddSection("Config", "Directory", CWD);
            ini.Save(Path.Combine(path_, m_File));
        }

    }
}
