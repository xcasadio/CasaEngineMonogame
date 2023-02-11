using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngine.Game;
using CasaEngineCommon.Logger;
using CasaEngine.Editor.Builder;
using Microsoft.Build.Evaluation;
using CasaEngine.SourceControl;
using CasaEngineCommon.Design;
using CasaEngine.Project;

namespace CasaEngine.Editor.Assets
{
    public class AssetManager
    {

        static private readonly uint Version = 1;

        private readonly ContentBuilder _contentBuilder = null;
        private ContentBuilder _contentBuilderTempFiles = null;

        private readonly List<AssetInfo> _assets = new List<AssetInfo>();
        private readonly List<AssetBuildInfo> _assetBuildInfo = new List<AssetBuildInfo>();

        //for temporary task
        private readonly FileSystemWatcher _fileWatcher;
        private readonly List<string> _assetToCopy = new List<string>();



        public AssetInfo[] Assets => _assets.ToArray();


        public AssetManager()
        {
            _contentBuilder = new ContentBuilder();
            _contentBuilderTempFiles = new ContentBuilder();

            _fileWatcher = new FileSystemWatcher(_contentBuilder.OutputDirectory.Replace("bin/Content", ""), "*.xnb");
            _fileWatcher.IncludeSubdirectories = true;
            _fileWatcher.EnableRaisingEvents = true;
            _fileWatcher.Created += new FileSystemEventHandler(_FileWatcher_Created);
        }



        void _FileWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Created)
            {
                _assetToCopy.Add(e.FullPath);
            }
        }


        public void AddAssetToBuild(string fileName, string assetName, AssetType assetType)
        {
            ProjectItem item;

            // Build this new model data.
            switch (assetType)
            {
                case AssetType.Audio:
                    item = _contentBuilder.Add(fileName, assetName, "WavImporter", "SoundEffectProcessor");
                    break;

                case AssetType.Texture:
                    item = _contentBuilder.Add(fileName, assetName, "TextureImporter", "TextureProcessor");
                    break;

                case AssetType.None:
                    item = _contentBuilder.Add(fileName, assetName);
                    break;

                case AssetType.All:
                    item = _contentBuilder.Add(fileName, assetName);
                    break;
            }
        }

        public void BuildAll(string destPath)
        {
            string buildError = _contentBuilder.Build();

            if (string.IsNullOrEmpty(buildError) == false)
            {
                LogManager.Instance.WriteLineError(buildError);
                return;
            }

            string contentDir = _contentBuilder.OutputDirectory.Replace("/", "\\");

            foreach (string file in _assetToCopy)
            {
                string fileDest = file.Replace(contentDir, file);
                File.Copy(
                    file,
                    fileDest,
                    true);
            }

            _assetToCopy.Clear();
            _contentBuilder.Clear();
        }




        public bool BuildAsset(string fileName, AssetInfo info, bool copy = true)
        {
            ProjectItem item;

            _contentBuilder.Clear();

            // Build this new model data.
            switch (info.Type)
            {
                case AssetType.Audio:
                    item = _contentBuilder.Add(fileName, info.Name, "WavImporter", "SoundEffectProcessor");
                    break;

                case AssetType.Texture:
                    item = _contentBuilder.Add(fileName, info.Name, "TextureImporter", "TextureProcessor");
                    break;

                case AssetType.None:
                    item = _contentBuilder.Add(fileName, info.Name);
                    break;

                case AssetType.All:
                    item = _contentBuilder.Add(fileName, info.Name);
                    break;
            }

            string buildError = _contentBuilder.Build();

            if (string.IsNullOrEmpty(buildError) == false)
            {
#if DEBUG
                throw new Exception(buildError);
#else
                LogManager.Instance.WriteLineError(buildError);
                return false;
#endif
            }

            foreach (string file in _assetToCopy)
            {
                File.Copy(
                    file,
                    /*GameInfo.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.XNBDirPath + Path.DirectorySeparatorChar + Path.GetFileName(file)*/
                    Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.GameDirPath + "\\Content\\" + Path.GetFileName(file),
                    true);
            }

            _assetToCopy.Clear();
            _contentBuilder.Clear();

            SetBuildSucceed(info);

            LogManager.Instance.WriteLine("Build asset '" + info.Name + "' successfull");

            return true;
        }

        public bool RebuildAsset(AssetInfo info)
        {
            _assets.Remove(info);

            string file = GetAssetXnbFullPath(info);
            File.Delete(file);

            string fullpath = GetAssetFullPath(info);

            //FileInfo fileInfo = new FileInfo(fullpath);
            //info_.ModificationDate = fileInfo.LastWriteTime;

            _assets.Add(info);

            return BuildAsset(fullpath, info);
        }

        private void SetBuildSucceed(AssetInfo info)
        {
            FileInfo fi = new FileInfo(GetAssetFullPath(info));
            int i;

            for (i = 0; i < _assetBuildInfo.Count; i++)
            {
                if (_assetBuildInfo[i].Id == info.Id)
                {
                    break;
                }
            }

            if (i == _assetBuildInfo.Count)
            {
                AssetBuildInfo b1 = new AssetBuildInfo();
                b1.Id = info.Id;
                b1.ModificationTime = fi.LastAccessTime;
                b1.Params = info.Params;
                _assetBuildInfo.Add(b1);
            }
            else
            {
                AssetBuildInfo b = _assetBuildInfo[i];
                b.ModificationTime = fi.LastAccessTime;
                b.Params = info.Params;
                _assetBuildInfo[i] = b;
            }
        }

        public bool AssetNeedToBeRebuild(AssetInfo info)
        {
            FileInfo fi = new FileInfo(GetAssetFullPath(info));
            int i;

            for (i = 0; i < _assetBuildInfo.Count; i++)
            {
                if (_assetBuildInfo[i].Id == info.Id)
                {
                    break;
                }
            }

            if (i < _assetBuildInfo.Count)
            {
                return fi.LastAccessTime > _assetBuildInfo[i].ModificationTime
                    && AssetBuildParamCollection.Compare(_assetBuildInfo[i].Params, info.Params) != 0;
            }

            return true;
        }

        public string GetAssetFullPath(AssetInfo info)
        {
            string fullpath = Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar;

            fullpath += ProjectManager.AssetDirPath + Path.DirectorySeparatorChar;

            /*switch (info_.Type)
            {
                case AssetType.Animation:
                    fullpath += ProjectManager.AnimationDirPath + Path.DirectorySeparatorChar;
                    break;

                case AssetType.Audio:
                    fullpath += ProjectManager.SoundDirPath + Path.DirectorySeparatorChar;
                    break;

                case AssetType.Effect:
                    fullpath += ProjectManager.EffectDirPath + Path.DirectorySeparatorChar;
                    break;

                case AssetType.SkinnedMesh:
                case AssetType.StaticMesh:
                    fullpath += ProjectManager.ModelDirPath + Path.DirectorySeparatorChar;
                    break;

                case AssetType.Texture:
                    fullpath += ProjectManager.ImageDirPath + Path.DirectorySeparatorChar;
                    break;

                case AssetType.Video:
                    fullpath += ProjectManager.VideoDirPath + Path.DirectorySeparatorChar;
                    break;

                default:
                    throw new InvalidOperationException("RebuildAsset() : The AssetType " + Enum.GetName(typeof(AssetType), info_.Type) + " is not handle");
            }*/

            fullpath += info.FileName;

            return fullpath;
        }

        public string GetAssetXnbFullPath(AssetInfo info)
        {
            return Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.GameDirPath + "\\Content\\" + info.Name + ".xnb";
        }

        /*private string GetPrefix(AssetType type_)
        {
            switch (type_)
            {
                case AssetType.Animation:
                    return "ANM_";

                case AssetType.Audio:
                    return "SND_";

                case AssetType.Effect:
                    return "FX_";

                case AssetType.SkinnedMesh:
                case AssetType.StaticMesh:
                    return "MSH_";

                case AssetType.Texture:
                    return "TEX_";

                case AssetType.Video:
                    return "VID_";
            }

            throw new ArgumentException("AssetManager.GetPrefix() : wrong AssetType");
        }*/

        public string GetProcessorName(AssetType type)
        {
            switch (type)
            {
                case AssetType.Animation:
                    return "AnimationProcessor";//"SkinnedModelProcessor";

                case AssetType.Effect:
                    return null; //default

                case AssetType.StaticMesh:
                    return "ModelProcessor";

                case AssetType.SkinnedMesh:
                    return "AnimationProcessor";//"SkinnedModelProcessor";

                case AssetType.Audio:
                    return null; //default

                case AssetType.Texture:
                    return null; //default

                case AssetType.Video:
                    return null; //default

                default:
                    throw new ArgumentException("No processor for asset undefined type");
                    //return null;
            }
        }



        public AssetInfo? GetAssetByName(string name)
        {
            if (string.IsNullOrEmpty(name) == true)
            {
                throw new ArgumentNullException("AssetManager.GetAssetByName() : the name is null or empty");
            }

            foreach (AssetInfo info in _assets)
            {
                if (info.Name.Equals(name) == true)
                {
                    return info;
                }
            }

            return null;
        }

        public bool AddAsset(string fileName, string name, AssetType type, ref string assetFileName)
        {
            AssetInfo info = new AssetInfo(0, name, type, Path.GetFileName(fileName));
            info.GetNewId();
            AddBuildParams(ref info);

            foreach (AssetInfo i in _assets)
            {
                if (i.Equals(info) == true)
                {
                    //replace asset
                    /*assetFileName_ = GetPathFromType(type_) + Path.DirectorySeparatorChar + Path.GetFileName(fileName_);

                    if (RebuildAsset(i) == true)
                    {
                        File.Copy(fileName_, assetFileName_, true);
                        LogManager.Instance.WriteLineDebug("Asset copied : " + fileName_ + " -> " + assetFileName_);
                        //AddFileInSourceControl(assetFileName_);
                        return true;
                    }*/
                    return true;
                    //return false;
                }
            }

            /*if (BuildAsset(fileName_, info) == true)
            {
                //copy to project directory
                string assetFile = GetPathFromType(type_);
                assetFile += Path.DirectorySeparatorChar + Path.GetFileName(fileName_);
                assetFileName_ = assetFile;

                File.Copy(fileName_, assetFile, true);
                LogManager.Instance.WriteLineDebug("Asset copied : " + fileName_ + " -> " + assetFile);
                AddFileInSourceControl(assetFileName_);

                _Assets.Add(info);
            }
            else
            {
                return false;
            }*/

            string assetFile = GetPathFromType(type);
            assetFile += Path.DirectorySeparatorChar + Path.GetFileName(fileName);
            assetFileName = assetFile;

            File.Copy(fileName, assetFile, true);
            LogManager.Instance.WriteLineDebug("Asset copied : " + fileName + " -> " + assetFile);
            AddFileInSourceControl(assetFileName);

            _assets.Add(info);

            return true;
        }

        public string GetPathFromType(AssetType type)
        {
            string assetFile = Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar;

            assetFile += ProjectManager.AssetDirPath;

            /*switch (type_)
            {
                case AssetType.Animation:
                    assetFile += ProjectManager.AnimationDirPath;
                    break;

                case AssetType.Audio:
                    assetFile += ProjectManager.SoundDirPath;
                    break;

                case AssetType.Effect:
                    assetFile += ProjectManager.EffectDirPath;
                    break;

                case AssetType.SkinnedMesh:
                    assetFile += ProjectManager.ModelDirPath;
                    break;

                case AssetType.StaticMesh:
                    assetFile += ProjectManager.ModelDirPath;
                    break;

                case AssetType.Texture:
                    assetFile += ProjectManager.ImageDirPath;
                    break;

                case AssetType.Video:
                    assetFile += ProjectManager.VideoDirPath;
                    break;

                default:
                    throw new InvalidOperationException("AssetManager.AddSet() : can't copy the asset, the type " + Enum.GetName(typeof(AssetType), type_) + " isn't handle");
            }*/

            return assetFile;
        }

        public void DeleteAsset(string name, AssetType type)
        {
            foreach (AssetInfo info in _assets)
            {
                if (info.Name.Equals(name)
                    && info.Type == type)
                {
                    DeleteAsset(info);
                }
            }
        }

        public void DeleteAsset(AssetInfo info)
        {
            _assets.Remove(info);
            LogManager.Instance.WriteLineDebug("Delete asset " + info.Name + "(" + info.FileName + ")");
        }

        public void Clear()
        {
            _assets.Clear();
            LogManager.Instance.WriteLineDebug("Asset.Clear()");
        }

        public void AddFileInSourceControl(string fileName)
        {
            if (SourceControlManager.Instance.SourceControl.IsValidConnection() == true)
            {
                SourceControlManager.Instance.SourceControl.MarkFileForAdd(fileName);
            }
        }

        public void DeleteFileInSourceControl(string fileName)
        {
            if (SourceControlManager.Instance.SourceControl.IsValidConnection() == true)
            {
                Dictionary<string, Dictionary<SourceControlKeyWord, string>> dic = SourceControlManager.Instance.SourceControl.FileStatus(new string[] { fileName });

                if (dic.ContainsKey(fileName) == true)
                {
                    if (dic[fileName].ContainsKey(SourceControlKeyWord.Action) == true)
                    {
                        if (dic[fileName][SourceControlKeyWord.Action].ToLower().Equals("add") == true)
                        {
                            SourceControlManager.Instance.SourceControl.RevertFile(fileName);
                            File.Delete(fileName);
                        }
                        else if (dic[fileName][SourceControlKeyWord.Action].ToLower().Equals("edit") == true)
                        {
                            SourceControlManager.Instance.SourceControl.RevertFile(fileName);
                            SourceControlManager.Instance.SourceControl.MarkFileForDelete(fileName);
                        }
                    }
                    else
                    {
                        SourceControlManager.Instance.SourceControl.MarkFileForDelete(fileName);
                    }
                }
                else
                {
                    File.Delete(fileName);
                }
            }
            else
            {
                File.Delete(fileName);
            }
        }

        public string[] GetAllAssetByType(AssetType type)
        {
            List<string> res = new List<string>();

            foreach (AssetInfo info in _assets)
            {
                if (info.Type == type)
                {
                    res.Add(info.Name);
                }
            }

            return res.ToArray();
        }



        public bool Save(XmlElement el, SaveOption option)
        {
            el.OwnerDocument.AddAttribute(el, "version", Version.ToString());
            XmlElement xmlEl;

            foreach (AssetInfo info in _assets)
            {
                xmlEl = el.OwnerDocument.CreateElement(ProjectManager.NodeAssetName);
                el.OwnerDocument.AddAttribute(xmlEl, "id", info.Id.ToString());
                el.OwnerDocument.AddAttribute(xmlEl, "name", info.Name);
                el.OwnerDocument.AddAttribute(xmlEl, "fileName", info.FileName);
                //el_.OwnerDocument.AddAttribute(xmlEl, "modificationDate", info.ModificationDate.ToString());
                el.OwnerDocument.AddAttribute(xmlEl, "type", ((int)info.Type).ToString());

                XmlElement paramNodes = el.OwnerDocument.CreateElement("BuildParameterList");
                xmlEl.AppendChild(paramNodes);

                foreach (AssetBuildParam param in info.Params)
                {
                    XmlElement buildNode = el.OwnerDocument.CreateElement("BuildParameter");
                    param.Save(buildNode);
                    paramNodes.AppendChild(buildNode);
                }

                el.AppendChild(xmlEl);
            }

#if !DEBUG
            try
            {
#endif

            SaveAssetBuildInfo();

#if !DEBUG
            }
            catch { }
#endif

            return true;
        }

        public bool Load(XmlElement el, SaveOption option)
        {
            uint version = uint.Parse(el.Attributes["version"].Value);

            _assets.Clear();

            foreach (XmlNode node in el.SelectNodes(ProjectManager.NodeAssetName))
            {
                AssetInfo info = new AssetInfo(
                    0,
                    node.Attributes["name"].Value,
                    (AssetType)Enum.Parse(typeof(AssetType), node.Attributes["type"].Value),
                    node.Attributes["fileName"].Value);

                if (version > 1)
                {
                    foreach (XmlNode n in node.SelectSingleNode("BuildParameterList").SelectNodes("BuildParameter"))
                    {
                        info.Params.Add(AssetBuildParamFactory.Load((XmlElement)n));
                    }

                    if (version > 2)
                    {
                        info.SetId(int.Parse(node.Attributes["id"].Value));
                    }
                    else
                    {
                        info.GetNewId();
                    }
                }
                else
                {
                    AddBuildParams(ref info);
                    info.GetNewId();
                }

                _assets.Add(info);

                /*try
                {
                    string fullpath = GetAssetFullPath(info);
                    FileInfo fileInfo = new FileInfo(fullpath);

                    if (fileInfo.LastAccessTime > info.ModificationDate)
                    {
                        RebuildAsset(info);
                    }
                }
                catch (System.Exception e)
                {
                    LogManager.Instance.WriteLineWarning("Can't get info from the asset " + info.FileName + "\n" + e.Message);
                }*/

                _assetBuildInfo.Clear();

#if !DEBUG
                try
                {
#endif

                LoadAssetBuildInfo();

#if !DEBUG
                }
                catch { }
#endif
            }

            return true;
        }

        private void LoadAssetBuildInfo()
        {
            string buildFile = Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.ConfigDirPath + Path.DirectorySeparatorChar + "AssetBuildInfo.xml";

            if (File.Exists(buildFile) == false)
            {
                LogManager.Instance.WriteLine("No AssetBuildInfo file found.");
                return;
            }

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(buildFile);

            XmlElement elRoot = (XmlElement)xmlDoc.SelectSingleNode("AssetBuildInfo");

            foreach (XmlNode node in elRoot.SelectNodes("BuildInfo"))
            {
                AssetBuildInfo info = new AssetBuildInfo();
                info.Id = int.Parse(node.Attributes["id"].Value);
                info.ModificationTime = DateTime.Parse(node.Attributes["date"].Value);
                info.Params = new AssetBuildParamCollection();

                foreach (XmlNode n in node.SelectSingleNode("BuildParameterList").SelectNodes("BuildParameter"))
                {
                    info.Params.Add(AssetBuildParamFactory.Load((XmlElement)n));
                }

                _assetBuildInfo.Add(info);
            }
        }

        public void SaveAssetBuildInfo()
        {
#if !DEBUG
            try
            {
#endif
            string buildFile = Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.ConfigDirPath + Path.DirectorySeparatorChar + "AssetBuildInfo.xml";

            if (Directory.Exists(Path.GetDirectoryName(buildFile)) == false)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(buildFile));
                }
                catch (Exception e)
                {
                    LogManager.Instance.WriteException(e);
                    return;
                }
            }

            XmlDocument xmlDoc = new XmlDocument();
            XmlElement elRoot = xmlDoc.AddRootNode("AssetBuildInfo");

            foreach (AssetBuildInfo info in _assetBuildInfo)
            {
                XmlElement node = xmlDoc.CreateElement("BuildInfo");
                elRoot.AppendChild(node);

                xmlDoc.AddAttribute(node, "id", info.Id.ToString());
                xmlDoc.AddAttribute(node, "date", info.ModificationTime.ToString());

                XmlElement paramNodes = xmlDoc.CreateElement("BuildParameterList");
                node.AppendChild(paramNodes);

                foreach (AssetBuildParam param in info.Params)
                {
                    XmlElement buildNode = xmlDoc.CreateElement("BuildParameter");
                    param.Save(buildNode);
                    paramNodes.AppendChild(buildNode);
                }
            }

            xmlDoc.Save(buildFile);
#if !DEBUG
            }
            catch (System.Exception e)
            {
                LogManager.Instance.WriteLineError(e.Message);
            }
#endif
        }


        private void AddBuildParams(ref AssetInfo assetInfo)
        {
            switch (assetInfo.Type)
            {
                case AssetType.Audio:
                    AssetBuildParamFactory.SetBuildParams(ref assetInfo.Params, AssetBuildParamFactory.AssetBuildParamType.Audio);
                    break;

                case AssetType.Effect:
                    AssetBuildParamFactory.SetBuildParams(ref assetInfo.Params, AssetBuildParamFactory.AssetBuildParamType.Effect);
                    break;

                case AssetType.SkinnedMesh:
                case AssetType.StaticMesh:
                    AssetBuildParamFactory.SetBuildParams(ref assetInfo.Params, AssetBuildParamFactory.AssetBuildParamType.Model);
                    break;

                case AssetType.Texture:
                    AssetBuildParamFactory.SetBuildParams(ref assetInfo.Params, AssetBuildParamFactory.AssetBuildParamType.Texture);
                    break;

                case AssetType.Video:
                    AssetBuildParamFactory.SetBuildParams(ref assetInfo.Params, AssetBuildParamFactory.AssetBuildParamType.Video);
                    break;

                default:
                    throw new NotImplementedException("AssetManager.Load() : the type '" + Enum.GetName(typeof(AssetType), assetInfo.Type) + "' is not supported.");
            }
        }

    }
}
