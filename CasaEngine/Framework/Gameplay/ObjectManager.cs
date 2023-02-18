using System.Xml;
using CasaEngine.Editor.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Core.Extension;
using CasaEngine.Framework.Entities;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Project;
using CasaEngine.Core.Design;

namespace CasaEngine.Framework.Gameplay
{
    public sealed class ObjectManager : ISaveLoad
    {
        class ObjectContainer
        {
            Entity _entity;
            private Type _itemType;

            public int Id
            {
                get;
                private set;
            }

            public bool IsLoaded => _entity != null;

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

            internal Entity Object
            {
                get
                {
                    if (_entity == null)
                    {
                        try
                        {
                            object[] args = null;

                            //if (XML)
                            XmlElement el;
                            string projectFile;

#if EDITOR
                            projectFile = Game.Engine.Instance.ProjectManager.ProjectPath + System.IO.Path.DirectorySeparatorChar + ProjectManager.AssetDirPath;
                            projectFile += System.IO.Path.DirectorySeparatorChar + Path + ".xml";
#else
                            projectFile = CasaEngine.Game.Engine.Instance.AssetContentManager.RootDirectory + System.IO.Path.DirectorySeparatorChar + this.Path + ".xml";
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

                            _entity = (Entity)Activator.CreateInstance(ItemType, args);
                            ClassName = _entity.GetType().FullName;
                        }
                        catch (Exception ex)
                        {
                            LogManager.Instance.WriteException(ex);
                        }
                    }

                    return _entity;
                }
                set
                {
                    _entity = value;
                    ClassName = _entity.GetType().FullName;
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
#if EDITOR
            private static int _version = 1;
            private static int _freeId = 1;

            public bool MustBeSaved { get; internal set; }

            public ObjectContainer(string path)
            {
                MustBeSaved = true;
                Id = _freeId++;
                Path = path;
                XPath = string.Empty;
                FilePosition = -1;
            }

            public void Rename(string newpath)
            {
                Path = newpath;
                MustBeSaved = true;
            }

            public void Save(BinaryWriter bw, SaveOption option)
            {
                throw new NotImplementedException();
            }

            public XmlElement Save(XmlElement el, SaveOption option)
            {
                //if (IsLoaded == true)
                //{
                var node = el.OwnerDocument.CreateElement("Object");
                el.AppendChild(node);

                el.OwnerDocument.AddAttribute(node, "id", Id.ToString());
                el.OwnerDocument.AddAttribute(node, "classname", ClassName);
                el.OwnerDocument.AddAttribute(node, "path", Path);
                el.OwnerDocument.AddAttribute(node, "version", _version.ToString());

                return node;
                //_BaseObject.Save(node, option_);
                //}
                /*else
                {
                    XmlNode otherNode = CasaEngine.Game.Engine.Instance.ProjectManager.LastXmlDocument.SelectSingleNode(XPath);
                    XmlNode importNode = el_.OwnerDocument.ImportNode(otherNode, true);
                    el_.AppendChild(importNode);
                }*/
            }
#endif
        }

        //full path, object container
        readonly Dictionary<string, ObjectContainer> _objects = new(100);

        public ObjectManager()
        {

        }

        public Entity GetObjectByPath(string fullpath)
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

        public Entity GetObjectById(int id)
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
            /*string assetPath = CasaEngine.Game.Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.AssetDirPath;

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
                /*Entity obj = oc.Object;

                if (obj is CasaEngine.Editor.Assets.IAssetable)
                {
                    CasaEngine.Editor.Assets.IAssetable ass = obj as CasaEngine.Editor.Assets.IAssetable;

                    foreach (string asset in ass.AssetFileNames)
                    {
                        string assetPath = CasaEngine.Game.Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar;
                        assetPath += ProjectManager.AssetDirPath + Path.DirectorySeparatorChar + asset;

                        string assetName = Path.GetDirectoryName(asset) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(asset);

                        assetManager.AddAssetToBuild(assetPath, assetName, CasaEngine.Editor.Assets.AssetType.Texture);
                    }
                }*/
            }

            //assetManager.BuildAll(CasaEngine.Game.Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.AssetDirPath);
        }

        public void Load(BinaryReader br, SaveOption option)
        {
            throw new NotImplementedException();
        }

#if EDITOR
        public struct ObjectData
        {
            public string Name { get; private set; }
            public string ClassName { get; private set; }
            public bool MustBeSaved { get; private set; }

            public ObjectData(string path, string className, bool mustBeSaved)
                : this()
            {
                Name = path;
                ClassName = className;
                MustBeSaved = mustBeSaved;
            }
        }

        private static int _version = 1;

        public event EventHandler ObjectAdded;
        public event EventHandler ObjectModified;
        public event EventHandler ObjectRenamed;
        public event EventHandler ObjectMoved;
        public event EventHandler ObjectRemoved;

        public event EventHandler FolderCreated;
        public event EventHandler FolderDeleted;

        public event EventHandler AllObjectSaved;

        public string[] Directories
        {
            get
            {
                var res = new List<string>();

                foreach (var pair in _objects)
                {
                    var str = Path.GetFileName(pair.Key);

                    if (res.Contains(str) == false)
                    {
                        res.Add(str);
                    }
                }

                return res.ToArray();
            }
        }

        public void Refresh()
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool IsValidObjectPath(string path)
        {
            return !_objects.ContainsKey(path);
        }

        public void Add(string path, Entity @object)
        {
            try
            {
                var newFolder = false;
                var folder = "";
                int index;

                var objC = new ObjectContainer(path);
                objC.Object = @object;

                index = path.LastIndexOf('\\');
                if (index != -1)
                {
                    folder = path.Substring(0, index);
                    newFolder = IsNewFolder(folder);
                }

                _objects.Add(objC.Path, objC);

                //TODO : source control

                if (ObjectAdded != null)
                {
                    ObjectAdded(path, EventArgs.Empty);
                }

                if (newFolder
                    && FolderCreated != null)
                {
                    FolderCreated(folder, EventArgs.Empty);
                }
            }
            catch (Exception e)
            {
                LogManager.Instance.WriteException(e);
            }
        }

        public void Replace(string path, Entity @object)
        {
            try
            {
                var objC = _objects[path];
                objC.MustBeSaved = true;
                objC.Object = @object;
                _objects[path] = objC;

                //TODO : source control

                if (ObjectModified != null)
                {
                    ObjectModified(path, EventArgs.Empty);
                }
            }
            catch (Exception e)
            {
                LogManager.Instance.WriteException(e);
            }
        }

        public void Remove(string path, bool removeAssetFile = true)
        {
            try
            {
                var objC = _objects[path];

                var baseObj = objC.Object;

                if (baseObj is IAssetable
                    && removeAssetFile)
                {
                    var asset = (IAssetable)baseObj;

                    foreach (var assetFileName in asset.AssetFileNames)
                    {
                        var assetPath = Game.Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar +
                                        ProjectManager.AssetDirPath + Path.DirectorySeparatorChar + assetFileName;

                        if (File.Exists(assetPath))
                        {
                            File.Delete(assetPath);

                            //TODO : Source Control
                        }
                    }
                }

                var objFile = GetFileName(objC);

                if (File.Exists(objFile))
                {
                    File.Delete(objFile);

                    //TODO : source control
                }

                _objects.Remove(path);

                if (ObjectRemoved != null)
                {
                    ObjectRemoved(path, EventArgs.Empty);
                }
            }
            catch (Exception e)
            {
                LogManager.Instance.WriteException(e);
            }
        }

        public void Rename(string srcPath, string newName)
        {
            try
            {
                var objC = _objects[srcPath];
                var path = Path.GetDirectoryName(srcPath);
                var newPath = path + Path.DirectorySeparatorChar + newName;

                //case new file or file has been modified
                if (newPath.EndsWith("*"))
                {
                    newPath = newPath.Substring(0, newPath.Length - 1);
                }

                var filePath = GetFileName(objC);
                var obj = objC.Object; // load object

                _objects.Remove(srcPath);
                objC.Rename(newPath);
                _objects.Add(objC.Path, objC);

                if (File.Exists(filePath))
                {
                    File.Move(filePath, GetFileName(objC));
                    objC.MustBeSaved = false;

                    //TODO : source control
                }
                else
                {
                    //SaveObjectXML(GetFileName(objC), objC, SaveOption.Editor); // save to create the new file                    
                }

                //TODO : source control

                if (ObjectRenamed != null)
                {
                    ObjectRenamed(srcPath, EventArgs.Empty);
                }
            }
            catch (Exception e)
            {
                LogManager.Instance.WriteException(e);
            }
        }

        public void Move(string srcPath, string destPath)
        {
            try
            {
                var objC = _objects[srcPath];
                var baseObj = objC.Object;

                if (baseObj is IAssetable)
                {
                    var asset = (IAssetable)baseObj;
                    var newAssets = new List<string>();

                    foreach (var fileName in asset.AssetFileNames)
                    {
                        var assetPath = Game.Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar;
                        assetPath += ProjectManager.AssetDirPath + Path.DirectorySeparatorChar + fileName;
                        var destAssetFile = assetPath.Replace(Path.GetDirectoryName(srcPath), destPath);

                        if (File.Exists(destAssetFile))
                        {
                            File.Delete(destAssetFile);
                        }

                        File.Move(assetPath, destAssetFile);
                        newAssets.Add(destAssetFile.Replace(Game.Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar +
                            ProjectManager.AssetDirPath + Path.DirectorySeparatorChar, ""));

                        //TODO : Source Control
                    }

                    asset.AssetFileNames.Clear();
                    asset.AssetFileNames.AddRange(newAssets);
                }

                var oldFile = GetFileName(objC);

                if (File.Exists(oldFile) == false)
                {
                    oldFile = string.Empty;
                    //File.Delete(GetFileName(objC));

                    //TODO : source control
                }

                _objects.Remove(objC.Path);
                var newFolder = IsNewFolder(destPath);

                objC.Rename(destPath + Path.DirectorySeparatorChar + objC.Name);
                _objects.Add(objC.Path, objC);

                var newFile = GetFileName(objC);

                //move old file
                if (string.IsNullOrEmpty(oldFile) == false)
                {
                    if (File.Exists(newFile))
                    {
                        File.Delete(newFile);
                    }

                    File.Move(oldFile, newFile);
                }
                else
                {
                    SaveObjectXml(newFile, objC, SaveOption.Editor);
                }

                objC.MustBeSaved = false;

                //TODO : source control

                if (newFolder
                    && FolderCreated != null)
                {
                    FolderCreated(destPath, EventArgs.Empty);
                }

                if (ObjectMoved != null)
                {
                    ObjectMoved(srcPath, EventArgs.Empty);
                }
            }
            catch (Exception e)
            {
                LogManager.Instance.WriteException(e);
            }
        }

        public bool IsNewFolder(string path)
        {
            foreach (var pair in _objects)
            {
                if (pair.Key.Contains(path))
                {
                    return false;
                }
            }

            return true;
        }

        public ObjectData[] GetAllItemsFromPath(string path, bool recursive = false)
        {
            var res = new List<ObjectData>();

            foreach (var pair in _objects)
            {
                if (recursive)
                {
                    if (pair.Key.Contains(path))
                    {
                        res.Add(new ObjectData(Path.GetFileName(pair.Key), pair.Value.ClassName, pair.Value.MustBeSaved));
                    }
                }
                else
                {
                    if (Path.GetDirectoryName(pair.Key).Equals(path))
                    {
                        res.Add(new ObjectData(Path.GetFileName(pair.Key), pair.Value.ClassName, pair.Value.MustBeSaved));
                    }
                }
            }

            return res.ToArray();
        }

        public string[] GetAllAssetByType(AssetType assetType)
        {
            var res = new List<string>();

            Type type = null;

            switch (assetType)
            {
                case AssetType.Audio:
                    type = typeof(Sound);
                    break;

                case AssetType.Video:

                    break;

                default:
                    throw new InvalidOperationException("ObjectManager.GetAllAssetByType() : AssetType is not supported");
            }

            foreach (var pair in _objects)
            {
                if (type.FullName.Equals(pair.Value.ClassName))
                {
                    res.Add(pair.Key);
                }
            }

            return res.ToArray();
        }

        public string[] GetAllDirectories()
        {
            var res = new List<string>();

            foreach (var pair in _objects)
            {
                var path = Path.GetDirectoryName(pair.Key);
                if (res.Contains(path) == false)
                {
                    res.Add(path);
                }
            }

            return res.ToArray();
        }

        public string GetObjectNameById(int id)
        {
            foreach (var pair in _objects)
            {
                if (pair.Value.Id == id)
                {
                    return pair.Value.Name;
                }
            }

            return string.Empty;
        }

        private string GetFileName(ObjectContainer objC)
        {
            var ext = ".xml";
            return Game.Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar
                                                                   + ProjectManager.AssetDirPath + Path.DirectorySeparatorChar + objC.Path + ext;
        }

        public void Save(XmlElement el, SaveOption option)
        {
            var node = el.OwnerDocument.CreateElement("Objects");
            el.AppendChild(node);

            node.OwnerDocument.AddAttribute(node, "version", _version.ToString());

            foreach (var pair in _objects)
            {
                //Save only info in project file
                pair.Value.Save(node, option);

                //Save the file object in the arborescence
                if (pair.Value.IsLoaded
                    && pair.Value.MustBeSaved)
                {
                    var filePath = GetFileName(pair.Value);

                    if (File.Exists(filePath) == false)
                    {
                        var di = Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    }
                    else
                    {
                        var fi = new FileInfo(filePath);

                        if (fi.IsReadOnly)
                        {
                            //Message
                            fi.IsReadOnly = false;
                        }
                    }

                    if (SaveObjectXml(filePath, pair.Value, option))
                    {
                        pair.Value.MustBeSaved = false;
                    }
                }
            }

            //SaveInSourceControlXML(option_);
            if (AllObjectSaved != null)
            {
                AllObjectSaved.Invoke(this, null);
            }
        }

        private bool SaveObjectXml(string filePath, ObjectContainer objC, SaveOption option)
        {
            try
            {
                var xmlDoc = new XmlDocument();
                var rootNode = xmlDoc.AddRootNode("Root");
                var objNode = objC.Save(rootNode, option);
                objC.Object.Save(objNode, option);
                xmlDoc.Save(filePath);
            }
            catch (Exception e)
            {
                LogManager.Instance.WriteException(e);
                return false;
            }

            return true;
        }
#endif
    }
}
