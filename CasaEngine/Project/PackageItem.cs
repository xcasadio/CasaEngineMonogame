using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngineCommon.Design;
using System.IO;
using System.Xml;
using System.Reflection;

namespace CasaEngine.Project
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class PackageItem
        : ISaveLoad
    {
        #region Fields
        
        #endregion

        #region Properties

        /// <summary>
        /// Gets
        /// </summary>
        public Package Package
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public int ID
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public string FullPath
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public string DirectoryPath
        {
            get { return System.IO.Path.GetDirectoryName(FullPath); }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public string Name
        {
            get { return System.IO.Path.GetFileName(FullPath); }
        }

        /// <summary>
        /// 
        /// </summary>
        public List<string> Tags { get; internal set; }

        /// <summary>
        /// Gets
        /// </summary>
        public DateTime DateTime { get; internal set; }

        #endregion

        #region Constructor

        /// <summary>
        /// 
        /// </summary>
        /// <param name="package_"></param>
        /// <param name="id_"></param>
        /// <param name="path_">Path of the item : subdirectory/.../filename</param>
        protected PackageItem(Package package_, int id_, string path_)
        {
            Package = package_;
            ID = id_;
            FullPath = path_;
        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        public abstract T LoadItem<T>();        

        #region ISaveLoad

#if EDITOR
        public void Save(BinaryWriter bw_, SaveOption option_)
        {

        }

        public void Save(XmlElement el_, SaveOption option_)
        {

        }
#endif
        public void Load(BinaryReader br_, SaveOption option_)
        {

        }
        public void Load(XmlElement el_, SaveOption option_)
        {
            
        }

        #endregion

        #endregion
    }
}
