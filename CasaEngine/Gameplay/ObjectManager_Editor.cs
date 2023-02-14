using CasaEngineCommon.Design;
using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngine.Game;
using CasaEngineCommon.Logger;
using CasaEngine.Editor.Assets;
using CasaEngine.Project;
using CasaEngine.Audio;
using CasaEngine.Gameplay.Actor;

namespace CasaEngine.Gameplay
{
    partial class ObjectManager
    {

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



        partial class ObjectContainer
        {
            private static int _version = 1;
            private static int _freeId = 1;


            public bool MustBeSaved
            {
                get;
                internal set;
            }


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
                    XmlNode otherNode = Engine.Instance.ProjectManager.LastXmlDocument.SelectSingleNode(XPath);
                    XmlNode importNode = el_.OwnerDocument.ImportNode(otherNode, true);
                    el_.AppendChild(importNode);
                }*/
            }


        } // end class ObjectContainer



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

        public void Add(string path, BaseObject @object)
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

        public void Replace(string path, BaseObject @object)
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
                        var assetPath = Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar +
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
                        var assetPath = Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar;
                        assetPath += ProjectManager.AssetDirPath + Path.DirectorySeparatorChar + fileName;
                        var destAssetFile = assetPath.Replace(Path.GetDirectoryName(srcPath), destPath);

                        if (File.Exists(destAssetFile))
                        {
                            File.Delete(destAssetFile);
                        }

                        File.Move(assetPath, destAssetFile);
                        newAssets.Add(destAssetFile.Replace(Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar +
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
            return Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar
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

        private void SaveInSourceControlXml(SaveOption option)
        {
            //source control
            /*if (SourceControlManager.Instance.SourceControl.IsValidConnection() == true)
            {
                string workspaceDir = SourceControlManager.Instance.CWD + System.IO.Path.DirectorySeparatorChar + "Asset" + System.IO.Path.DirectorySeparatorChar;

                foreach (var pair in _Objects)
                {
                    if (pair.Value.IsLoaded == true)
                    {
                        //fichier
                        bool saveFile = false, addFile = false;
                        string filePath = workspaceDir + pair.Value.Path + ".xml";
                        Dictionary<string, Dictionary<SourceControlKeyWord, string>> fileStatus = null;

                        if (File.Exists(filePath) == false)
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                            addFile = true;
                            saveFile = true;
                        }
                        else
                        {
                            fileStatus = SourceControlManager.Instance.SourceControl.FileStatus(new string[] { filePath });

                            if (fileStatus == null)
                            {
                                saveFile = true;
                            }
                            else
                            {
                                if (fileStatus.Count == 0
                                    || fileStatus[filePath].ContainsKey(SourceControlKeyWord.Action))
                                {
                                    if (fileStatus[filePath][SourceControlKeyWord.Action].Equals("add") == false
                                        && fileStatus[filePath][SourceControlKeyWord.Action].Equals("delete") == false)
                                    {
                                        saveFile = true;
                                    }
                                }
                                else
                                {
                                    saveFile = true;
                                }
                            }
                        }

                        if (saveFile == true)
                        {
                            if (addFile == false)
                            {
                                SourceControlManager.Instance.SourceControl.CheckOut(filePath);   
                            }                   
                            
                            //TODO : savoir dans quel etat on est : checkout, add, delete, lock
                            //fileStatus[filePath].ContainsKey(SourceControlKeyWord.Action)
                            XmlDocument xmlDoc = new XmlDocument();
                            XmlElement rootNode = xmlDoc.AddRootNode("Root");
                            pair.Value.Save(rootNode, option_);    
                            xmlDoc.Save(filePath);

                            if (addFile)
                            {
                                SourceControlManager.Instance.SourceControl.MarkFileForAdd(filePath);   
                            }                                                     
                        }
                        
                        //si asset
                        if (pair.Value.Object is IAssetable)
                        {
                            IAssetable asset = pair.Value.Object as IAssetable;
                            if (string.IsNullOrWhiteSpace(asset.AssetFileName) == false)
                            {
                                string path = Engine.Instance.ProjectManager.ProjectPath + System.IO.Path.DirectorySeparatorChar + ProjectManager.AssetDirPath;
                                path += System.IO.Path.DirectorySeparatorChar + asset.AssetFileName;

                                if (File.Exists(path) == true)
                                {
                                    string wrkspcFileName = workspaceDir + Path.GetDirectoryName(pair.Key) + System.IO.Path.DirectorySeparatorChar + asset.AssetFileName;
                                    fileStatus = SourceControlManager.Instance.SourceControl.FileStatus(new string[] { wrkspcFileName });

                                    if (File.Exists(wrkspcFileName) == true)
                                    {
                                        //file different from workspace ?
                                        FileInfo fi1 = new FileInfo(wrkspcFileName);
                                        FileInfo fi2 = new FileInfo(path);

                                        if (fi1.Length != fi2.Length
                                            || fi1.LastWriteTime.CompareTo(fi2.LastWriteTime) != 0)
                                        {
                                            SourceControlManager.Instance.SourceControl.CheckOut(wrkspcFileName);
                                            File.Copy(path, wrkspcFileName, true);                                            
                                        }
                                    }
                                    else
                                    {
                                        File.Copy(path, wrkspcFileName, true);
                                        SourceControlManager.Instance.SourceControl.MarkFileForAdd(wrkspcFileName);
                                    }
                                }
                                else
                                {
                                    LogManager.Instance.WriteLineError("Save object '" + pair.Value.Path + "' : can't find the asset '" + path + "'");
                                }
                            }
                        }
                    }                    
                }
            }*/
        }

        public void Save(BinaryWriter bw, SaveOption option)
        {
            throw new NotImplementedException();

            if (AllObjectSaved != null)
            {
                AllObjectSaved.Invoke(this, null);
            }
        }


    }
}
