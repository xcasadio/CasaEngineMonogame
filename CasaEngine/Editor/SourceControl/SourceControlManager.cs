using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Game;
using System.IO;
using CasaEngineCommon.Logger;
using System.ComponentModel;
using System.Xml;
using CasaEngineCommon.Config;

namespace CasaEngine.SourceControl
{
    /// <summary>
    /// 
    /// </summary>
    public class SourceControlManager
    {

        static private SourceControlManager m_Instance;
        private const int m_Version = 1;
        private readonly string m_File = "SourceControl.ini";
        private ISourceControl m_SourceControl;
        private Dictionary<string, Dictionary<SourceControlKeyWord, string>> m_FilesStatus = new Dictionary<string, Dictionary<SourceControlKeyWord, string>>();




        /// <summary>
        /// Gets/Sets
        /// </summary>
        [Category("Data Source Control")]
        public string Server
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        [Category("Data Source Control")]
        public string User
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        [Browsable(false)]
        public string Password
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        [Category("Data Source Control")]
        public string Workspace
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        [Category("Data Source Control")]
        public string CWD
        {
            get;
            set;
        }


        /// <summary>
        /// Gets
        /// </summary>
        public Dictionary<string, Dictionary<SourceControlKeyWord, string>> FilesStatus
        {
            get { return m_FilesStatus; }
        }

        /// <summary>
        /// 
        /// </summary>
        public ISourceControl SourceControl
        {
            get { return m_SourceControl; }
            set { m_SourceControl = value; }
        }

        /// <summary>
        /// Gets
        /// </summary>
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



        /// <summary>
        /// 
        /// </summary>
        private SourceControlManager()
        {
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="control_"></param>
        public void Initialize(ISourceControl control_)
        {
            if (control_ == null)
            {
                throw new ArgumentNullException("SourceControlManager.Initialize() : ISourceControl is null");
            }

            m_SourceControl = control_;
        }

        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path_"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path_"></param>
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
