using CasaEngineCommon.Design;
using System.Xml;
using CasaEngine.Game;
using CasaEngineCommon.Logger;
using System.Reflection;
using CasaEngine.Gameplay.Actor;
using CasaEngine.Project;

namespace CasaEngine.Gameplay
{
    public sealed
#if EDITOR
    partial
#endif
    class ObjectManager : ISaveLoad
    {
#if EDITOR
        partial
#endif
        class ObjectContainer
        {
            Actor.BaseObject _baseObject;
            private Type _itemType;


            public int Id
            {
                get;
                private set;
            }

            public bool IsLoaded => _baseObject != null;

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

            internal Actor.BaseObject Object
            {
                get
                {
                    if (_baseObject == null)
                    {
                        try
                        {
                            object[] args = null;

                            //if (XML)
                            XmlElement el;
                            string projectFile;


#if EDITOR
                            projectFile = Engine.Instance.ProjectManager.ProjectPath + System.IO.Path.DirectorySeparatorChar + ProjectManager.AssetDirPath;
                            projectFile += System.IO.Path.DirectorySeparatorChar + Path + ".xml";
#else
                            projectFile = Engine.Instance.AssetContentManager.RootDirectory + System.IO.Path.DirectorySeparatorChar + this.Path + ".xml";
#endif

                            var xmlDoc = new XmlDocument();
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

                            _baseObject = (Actor.BaseObject)Activator.CreateInstance(ItemType, args);
                            ClassName = _baseObject.GetType().FullName;
                        }
                        catch (Exception ex)
                        {
                            LogManager.Instance.WriteException(ex);
                        }
                    }

                    return _baseObject;
                }
                set
                {
                    _baseObject = value;
                    ClassName = _baseObject.GetType().FullName;
                }
            }

            public Type ItemType
            {
                get
                {
                    if (_itemType == null)
                    {
                        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                        foreach (var a in assemblies)
                        {
                            _itemType = a.GetType(ClassName, false, true);

                            if (_itemType != null)
                            {
                                break;
                            }
                        }

                        if (_itemType == null)
                        {
                            throw new Exception("Can't retrieve the type " + ClassName);
                        }
                    }

                    return _itemType;
                }
            }


            public ObjectContainer()
            {
#if EDITOR
                MustBeSaved = true;
#endif
            }

            public ObjectContainer(XmlElement el, SaveOption option)
            {
#if EDITOR
                MustBeSaved = false;
#endif
                Load(el, option);
            }

            public ObjectContainer(BinaryReader br, SaveOption option)
            {
#if EDITOR
                MustBeSaved = true;
#endif
                Load(br, option);
            }


            public void Load(BinaryReader br, SaveOption option)
            {
                throw new NotImplementedException();
            }

            public void Load(XmlElement el, SaveOption option)
            {
                var version = int.Parse(el.Attributes["version"].Value);

                Id = int.Parse(el.Attributes["id"].Value);

#if EDITOR
                if (_freeId <= Id)
                {
                    _freeId = Id + 1;
                }
#endif

                ClassName = el.Attributes["classname"].Value;
                Path = el.Attributes["path"].Value;

                XPath = "Project/Objects/Object[@id='" + Id + "']";
            }

        }



        //full path, object container
        readonly Dictionary<string, ObjectContainer> _objects = new(100);





        public ObjectManager()
        {

        }




        public Actor.BaseObject GetObjectByPath(string fullpath)
        {
            if (_objects.ContainsKey(fullpath) == false)
            {
                return null;
            }

            //_Objects[fullpath_].Object return a copy!
            var obj = _objects[fullpath];
            var res = obj.Object;
            res.Id = obj.Id;
            _objects[fullpath] = obj;
            return res;
        }

        public Actor.BaseObject GetObjectById(int id)
        {
            //_Objects[fullpath_].Object return a copy!
            ObjectContainer obj = null;

            foreach (var pair in _objects)
            {
                if (pair.Value.Id == id)
                {
                    obj = pair.Value;
                    break;
                }
            }

            if (obj != null)
            {
                var res = obj.Object;
                res.Id = obj.Id;
                //_Objects[obj.Path] = obj;
                return res;
            }

            return null;
        }



        public void Load(XmlElement el, SaveOption option)
        {
            /*string assetPath = Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.AssetDirPath;

            foreach (string file in Directory.GetFiles(assetPath, "*.xml", SearchOption.AllDirectories))
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(file);
                ObjectContainer oc = new ObjectContainer((XmlElement)xmlDoc.SelectSingleNode("Root/Object"), option_);
                _Objects.Add(oc.Path, oc);
            }*/

            //CasaEngine.Editor.Assets.AssetManager assetManager = new CasaEngine.Editor.Assets.AssetManager();

            var node = el.SelectSingleNode("Objects");

            _objects.Clear();

            foreach (XmlNode child in node.ChildNodes)
            {
                var oc = new ObjectContainer((XmlElement)child, option);
                _objects.Add(oc.Path, oc);

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

        public void Load(BinaryReader br, SaveOption option)
        {
            throw new NotImplementedException();
        }


    }
}
