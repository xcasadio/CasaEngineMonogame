using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngineCommon.Design;
using System.Xml;
using System.IO;
using CasaEngineCommon.Logger;
using CasaEngine.Game;
using CasaEngine.Graphics2D;

namespace CasaEngine.Project
{
    /// <summary>
    /// 
    /// </summary>
    public class Package
    {
        #region Fields

        #endregion

        #region Properties

        /// <summary>
        /// Gets
        /// </summary>
        public Package Parent
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public List<Package> Children
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public List<PackageItem> Items
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public string PathFromProjectPath
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public string PackageFileName
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public string[] SubDirectories
        {
            get
            {
                List<string> res = new List<string>();

                foreach (PackageItem item in Items)
                {
                    if (res.Contains(item.DirectoryPath) == false
                        && string.IsNullOrWhiteSpace(item.DirectoryPath) == false)
                    {
                        res.Add(item.DirectoryPath);
                    }
                }

                return res.ToArray();
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        /// <param name="parent_"></param>
        public Package(string name_, Package parent_ = null)
        {
            Parent = parent_;
            Name = name_;
            Items = new List<PackageItem>();
            Children = new List<Package>();
        }

        #endregion

        #region Methods

        #region Item

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item_"></param>
        /// <param name="subDirectory_"></param>
        public void AddItem(PackageItem item_, string subDirectory_ = "")
        {
            Items.Add(item_);        
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_">group name + name</param>
        /// <param name="sprite2D"></param>
        public T GetItem<T>(string name_) where T : class
        {
            foreach (PackageItem item in Items)
            {
                if (item.Name.Equals(name_) == true)
                {
                    return item as T;
                }
            }

            return null;
        }

        #endregion

        #region Save/Load

        #region XML

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file_"></param>
        /// <param name="opt_"></param>
        public void LoadXml(string file_, SaveOption opt_)
        {
            if (File.Exists(file_) == false)
            {
                LogManager.Instance.WriteLineError("Package.Load() : Can't open the package file " + file_);
                return;
            }

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(file_);

            PackageFileName = file_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file_"></param>
        /// <param name="opt_"></param>
        public void SaveXml(string file_, SaveOption opt_)
        {
            XmlDocument xmlDoc = new XmlDocument();


            //add in workspace
            /*if (SourceControl.SourceControlManager.Instance.SourceControl.IsValidConnection() == true)
            {
                string wrkspc = SourceControl.SourceControlManager.Instance.Workspace;
                Directory.CreateDirectory(wrkspc + Path.DirectorySeparatorChar + item_.DirectoryPath);
             
                string fullPath = GameInfo.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.PackageDirPath + Path.DirectorySeparatorChar;
                fullPath += path_.Replace('.', Path.DirectorySeparatorChar);
                Directory.CreateDirectory(Path.GetFullPath(fullPath));
            }*/

            xmlDoc.Save(file_);

            PackageFileName = file_;
        }

        #endregion

        #region Binary

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file_"></param>
        /// <param name="opt_"></param>
        public void LoadBinary(string file_, SaveOption opt_)
        {
            if (File.Exists(file_) == false)
            {
                LogManager.Instance.WriteLineError("Package.Load() : Can't open the package file " + file_);
                return;
            }

            BinaryReader br = new BinaryReader(File.Open(file_, FileMode.Open));

            br.Close();

            PackageFileName = file_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file_"></param>
        /// <param name="opt_"></param>
        public void SaveBinary(string file_, SaveOption opt_)
        {
            BinaryWriter bw = new BinaryWriter(File.Open(file_, FileMode.Create));

            bw.Close();

            PackageFileName = file_;
        }

        #endregion

        #endregion

        #endregion
    }
}
