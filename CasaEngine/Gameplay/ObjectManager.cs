using CasaEngineCommon.Design;
using System.Xml;
using CasaEngine.Gameplay.Actor.Object;
using CasaEngine.Game;
using CasaEngineCommon.Logger;
using System.Reflection;
using CasaEngine.Project;

namespace CasaEngine.Gameplay
{
    public sealed
#if EDITOR
    partial
#endif
    class ObjectManager
        : ISaveLoad
    {

#if EDITOR
        partial
#endif
        class ObjectContainer
        {
            BaseObject m_BaseObject;
            private Type m_ItemType;


            public int ID
            {
                get;
                private set;
            }

            public bool IsLoaded => m_BaseObject != null;

            public string ClassName
            {
                get;
                private set;
            }

            public string Name => System.IO.Path.GetFileName(Path);

            public string Path
            {
                get;
                private set;
            }

            internal int FilePosition
            {
                get;
                set;
            }

            internal string XPath
            {
                get;
                set;
            }

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


            public ObjectContainer()
            {
#if EDITOR
                MustBeSaved = true;
#endif
            }

            public ObjectContainer(XmlElement el_, SaveOption option_)
            {
#if EDITOR
                MustBeSaved = false;
#endif
                Load(el_, option_);
            }

            public ObjectContainer(BinaryReader br_, SaveOption option_)
            {
#if EDITOR
                MustBeSaved = true;
#endif
                Load(br_, option_);
            }


            public void Load(System.IO.BinaryReader br_, SaveOption option_)
            {
                throw new NotImplementedException();
            }

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
        readonly Dictionary<string, ObjectContainer> m_Objects = new Dictionary<string, ObjectContainer>(100);





        public ObjectManager()
        {

        }




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

        public void Load(System.IO.BinaryReader br_, SaveOption option_)
        {
            throw new NotImplementedException();
        }


    }
}
