using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.AI;
using CasaEngineCommon.Design;
using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngine.Gameplay.Actor.Object;
using CasaEngine.Game;
using System.IO;
using CasaEngineCommon.Logger;
using System.Reflection;
using CasaEngine.Project;

namespace CasaEngine.Gameplay
{
    /// <summary>
    /// 
    /// </summary>
    public sealed
#if EDITOR
    partial
#endif
    class ObjectManager
        : ISaveLoad
    {

        /// <summary>
        /// 
        /// </summary>
#if EDITOR
        partial
#endif
        class ObjectContainer
        {
            BaseObject m_BaseObject;
            private Type m_ItemType;


            /// <summary>
            /// 
            /// </summary>
            public int ID
            {
                get;
                private set;
            }

            /// <summary>
            /// 
            /// </summary>
            public bool IsLoaded
            {
                get { return m_BaseObject != null; }
            }

            /// <summary>
            /// Gets
            /// </summary>
            public string ClassName
            {
                get;
                private set;
            }

            /// <summary>
            /// 
            /// </summary>
            public string Name
            {
                get { return System.IO.Path.GetFileName(Path); }
            }

            /// <summary>
            /// 
            /// </summary>
            public string Path
            {
                get;
                private set;
            }

            /// <summary>
            /// 
            /// </summary>
            internal int FilePosition
            {
                get;
                set;
            }

            /// <summary>
            /// 
            /// </summary>
            internal string XPath
            {
                get;
                set;
            }

            /// <summary>
            /// 
            /// </summary>
            internal BaseObject Object
            {
                get
                {
                    if (m_BaseObject == null)
                    {
                        try
                        {
                            object[] args = null;

                            //if (XML)
                            XmlElement el;
                            string projectFile;


#if EDITOR
                            projectFile = Engine.Instance.ProjectManager.ProjectPath + System.IO.Path.DirectorySeparatorChar + ProjectManager.AssetDirPath;
                            projectFile += System.IO.Path.DirectorySeparatorChar + this.Path + ".xml";
#else
                            projectFile = Engine.Instance.AssetContentManager.RootDirectory + System.IO.Path.DirectorySeparatorChar + this.Path + ".xml";
#endif

                            XmlDocument xmlDoc = new XmlDocument();
                            xmlDoc.Load(projectFile);
                            el = (XmlElement)xmlDoc.SelectSingleNode("Root/Object");

                            if (el == null)
                            {
                                LogManager.Instance.WriteLineError("ObjectContainer.Object : can't find the xpath '" + XPath + "' (file :'" + projectFile + "'");
                                return null;
                            }
                            else
                            {
                                args = new object[] { el, SaveOption.Editor };
                            }
                            //else (Binary)

                            m_BaseObject = (BaseObject)Activator.CreateInstance(ItemType, args);
                            ClassName = m_BaseObject.GetType().FullName;
                        }
                        catch (System.Exception ex)
                        {
                            LogManager.Instance.WriteException(ex);
                        }
                    }

                    return m_BaseObject;
                }
                set
                {
                    m_BaseObject = value;
                    ClassName = m_BaseObject.GetType().FullName;
                }
            }

            /// <summary>
            /// Gets
            /// </summary>
            public Type ItemType
            {
                get
                {
                    if (m_ItemType == null)
                    {
                        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

                        foreach (Assembly a in assemblies)
                        {
                            m_ItemType = a.GetType(ClassName, false, true);

                            if (m_ItemType != null)
                            {
                                break;
                            }
                        }

                        if (m_ItemType == null)
                        {
                            throw new Exception("Can't retrieve the type " + ClassName);
                        }
                    }

                    return m_ItemType;
                }
            }


            /// <summary>
            /// 
            /// </summary>
            public ObjectContainer()
            {
#if EDITOR
                MustBeSaved = true;
#endif
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="el_"></param>
            /// <param name="option_"></param>
            public ObjectContainer(XmlElement el_, SaveOption option_)
            {
#if EDITOR
                MustBeSaved = false;
#endif
                Load(el_, option_);
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="br_"></param>
            /// <param name="option_"></param>
            public ObjectContainer(BinaryReader br_, SaveOption option_)
            {
#if EDITOR
                MustBeSaved = true;
#endif
                Load(br_, option_);
            }


            /// <summary>
            /// 
            /// </summary>
            /// <param name="br_"></param>
            /// <param name="option_"></param>
            public void Load(System.IO.BinaryReader br_, SaveOption option_)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="el_"></param>
            /// <param name="option_"></param>
            public void Load(XmlElement el_, SaveOption option_)
            {
                int version = int.Parse(el_.Attributes["version"].Value);

                ID = int.Parse(el_.Attributes["id"].Value);

#if EDITOR
                if (m_FreeID <= ID)
                {
                    m_FreeID = ID + 1;
                }
#endif

                ClassName = el_.Attributes["classname"].Value;
                Path = el_.Attributes["path"].Value;

                XPath = "Project/Objects/Object[@id='" + ID + "']";
            }

        }



        //full path, object container
        Dictionary<string, ObjectContainer> m_Objects = new Dictionary<string, ObjectContainer>(100);





        /// <summary>
        /// 
        /// </summary>
        public ObjectManager()
        {

        }




        /// <summary>
        /// 
        /// </summary>
        /// <param name="fullpath_"></param>
        public BaseObject GetObjectByPath(string fullpath_)
        {
            if (m_Objects.ContainsKey(fullpath_) == false)
            {
                return null;
            }

            //m_Objects[fullpath_].Object return a copy!
            ObjectContainer obj = m_Objects[fullpath_];
            BaseObject res = obj.Object;
            res.ID = obj.ID;
            m_Objects[fullpath_] = obj;
            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id_"></param>
        public BaseObject GetObjectByID(int id_)
        {
            //m_Objects[fullpath_].Object return a copy!
            ObjectContainer obj = null;

            foreach (var pair in m_Objects)
            {
                if (pair.Value.ID == id_)
                {
                    obj = pair.Value;
                    break;
                }
            }

            if (obj != null)
            {
                BaseObject res = obj.Object;
                res.ID = obj.ID;
                //m_Objects[obj.Path] = obj;
                return res;
            }

            return null;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public void Load(XmlElement el_, SaveOption option_)
        {
            /*string assetPath = Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.AssetDirPath;

            foreach (string file in Directory.GetFiles(assetPath, "*.xml", SearchOption.AllDirectories))
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(file);
                ObjectContainer oc = new ObjectContainer((XmlElement)xmlDoc.SelectSingleNode("Root/Object"), option_);
                m_Objects.Add(oc.Path, oc);
            }*/

            //CasaEngine.Editor.Assets.AssetManager assetManager = new CasaEngine.Editor.Assets.AssetManager();

            XmlNode node = el_.SelectSingleNode("Objects");

            m_Objects.Clear();

            foreach (XmlNode child in node.ChildNodes)
            {
                ObjectContainer oc = new ObjectContainer((XmlElement)child, option_);
                m_Objects.Add(oc.Path, oc);

                //to build missing asset
                /*BaseObject obj = oc.Object;

                if (obj is CasaEngine.Editor.Assets.IAssetable)
                {
                    CasaEngine.Editor.Assets.IAssetable ass = obj as CasaEngine.Editor.Assets.IAssetable;

                    foreach (string asset in ass.AssetFileNames)
                    {
                        string assetPath = Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar;
                        assetPath += ProjectManager.AssetDirPath + Path.DirectorySeparatorChar + asset;

                        string assetName = Path.GetDirectoryName(asset) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(asset);

                        assetManager.AddAssetToBuild(assetPath, assetName, CasaEngine.Editor.Assets.AssetType.Texture);
                    }
                }*/
            }

            //assetManager.BuildAll(Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.AssetDirPath);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="br_"></param>
        /// <param name="option_"></param>
        public void Load(System.IO.BinaryReader br_, SaveOption option_)
        {
            throw new NotImplementedException();
        }


    }
}
