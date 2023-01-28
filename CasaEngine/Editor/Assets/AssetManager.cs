using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Content;
using System.Xml;
using CasaEngineCommon.Extension;
using System.IO;
using CasaEngine.Game;
using CasaEngineCommon.Logger;
using Microsoft.Xna.Framework;
using CasaEngine;
using CasaEngine.Editor.Builder;
using Microsoft.Build.Evaluation;
using CasaEngine.SourceControl;
using CasaEngineCommon.Design;
using CasaEngine.Project;

namespace CasaEngine.Editor.Assets
{
    /// <summary>
    /// Handle all assets and build
    /// </summary>
    public class AssetManager
    {

        static private readonly uint m_Version = 1;

        private ContentBuilder m_ContentBuilder = null;
        private ContentBuilder m_ContentBuilderTempFiles = null;

        private List<AssetInfo> m_Assets = new List<AssetInfo>();
        private List<AssetBuildInfo> m_AssetBuildInfo = new List<AssetBuildInfo>();

        //for temporary task
        private FileSystemWatcher m_FileWatcher;
        private List<string> m_AssetToCopy = new List<string>();



        /// <summary>
        /// 
        /// </summary>
        public AssetInfo[] Assets
        {
            get { return m_Assets.ToArray(); }
        }



        /// <summary>
        /// 
        /// </summary>
        public AssetManager()
        {
            m_ContentBuilder = new ContentBuilder();
            m_ContentBuilderTempFiles = new ContentBuilder();

            m_FileWatcher = new FileSystemWatcher(m_ContentBuilder.OutputDirectory.Replace("bin/Content", ""), "*.xnb");
            m_FileWatcher.IncludeSubdirectories = true;
            m_FileWatcher.EnableRaisingEvents = true;
            m_FileWatcher.Created += new FileSystemEventHandler(m_FileWatcher_Created);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void m_FileWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Created)
            {
                m_AssetToCopy.Add(e.FullPath);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        /// <param name="destPath_"></param>
        /// <param name="assetType_"></param>
        /// <returns></returns>
        public void AddAssetToBuild(string fileName_, string assetName_, AssetType assetType_)
        {
            ProjectItem item;

            // Build this new model data.
            switch (assetType_)
            {
                case AssetType.Audio:
                    item = m_ContentBuilder.Add(fileName_, assetName_, "WavImporter", "SoundEffectProcessor");
                    break;

                case AssetType.Texture:
                    item = m_ContentBuilder.Add(fileName_, assetName_, "TextureImporter", "TextureProcessor");
                    break;

                case AssetType.None:
                    item = m_ContentBuilder.Add(fileName_, assetName_);
                    break;

                case AssetType.All:
                    item = m_ContentBuilder.Add(fileName_, assetName_);
                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="destPath_"></param>
        public void BuildAll(string destPath_)
        {
            string buildError = m_ContentBuilder.Build();

            if (string.IsNullOrEmpty(buildError) == false)
            {
                LogManager.Instance.WriteLineError(buildError);
                return;
            }

            string contentDir = m_ContentBuilder.OutputDirectory.Replace("/", "\\");

            foreach (string file in m_AssetToCopy)
            {
                string fileDest = file.Replace(contentDir, file);
                File.Copy(
                    file,
                    fileDest,
                    true);
            }

            m_AssetToCopy.Clear();
            m_ContentBuilder.Clear();
        }




        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        /// <param name="info_"></param>
        /// <param name="copy"></param>
        /// <returns></returns>
        public bool BuildAsset(string fileName_, AssetInfo info_, bool copy = true)
        {
            ProjectItem item;

            m_ContentBuilder.Clear();

            // Build this new model data.
            switch (info_.Type)
            {
                case AssetType.Audio:
                    item = m_ContentBuilder.Add(fileName_, info_.Name, "WavImporter", "SoundEffectProcessor");
                    break;

                case AssetType.Texture:
                    item = m_ContentBuilder.Add(fileName_, info_.Name, "TextureImporter", "TextureProcessor");
                    break;

                case AssetType.None:
                    item = m_ContentBuilder.Add(fileName_, info_.Name);
                    break;

                case AssetType.All:
                    item = m_ContentBuilder.Add(fileName_, info_.Name);
                    break;
            }

            string buildError = m_ContentBuilder.Build();

            if (string.IsNullOrEmpty(buildError) == false)
            {
#if DEBUG
                throw new Exception(buildError);
#else
                LogManager.Instance.WriteLineError(buildError);
                return false;
#endif
            }

            foreach (string file in m_AssetToCopy)
            {
                File.Copy(
                    file,
                    /*GameInfo.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.XNBDirPath + Path.DirectorySeparatorChar + Path.GetFileName(file)*/
                    Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.GameDirPath + "\\Content\\" + Path.GetFileName(file),
                    true);
            }

            m_AssetToCopy.Clear();
            m_ContentBuilder.Clear();

            SetBuildSucceed(info_);

            LogManager.Instance.WriteLine("Build asset '" + info_.Name + "' successfull");

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="info_"></param>
        /// <returns></returns>
        public bool RebuildAsset(AssetInfo info_)
        {
            m_Assets.Remove(info_);

            string file = GetAssetXNBFullPath(info_);
            File.Delete(file);

            string fullpath = GetAssetFullPath(info_);

            //FileInfo fileInfo = new FileInfo(fullpath);
            //info_.ModificationDate = fileInfo.LastWriteTime;

            m_Assets.Add(info_);

            return BuildAsset(fullpath, info_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="info_"></param>
        private void SetBuildSucceed(AssetInfo info_)
        {
            FileInfo fi = new FileInfo(GetAssetFullPath(info_));
            int i;

            for (i = 0; i < m_AssetBuildInfo.Count; i++)
            {
                if (m_AssetBuildInfo[i].ID == info_.ID)
                {
                    break;
                }
            }

            if (i == m_AssetBuildInfo.Count)
            {
                AssetBuildInfo b1 = new AssetBuildInfo();
                b1.ID = info_.ID;
                b1.ModificationTime = fi.LastAccessTime;
                b1.Params = info_.Params;
                m_AssetBuildInfo.Add(b1);
            }
            else
            {
                AssetBuildInfo b = m_AssetBuildInfo[i];
                b.ModificationTime = fi.LastAccessTime;
                b.Params = info_.Params;
                m_AssetBuildInfo[i] = b;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="info_"></param>
        /// <returns></returns>
        public bool AssetNeedToBeRebuild(AssetInfo info_)
        {
            FileInfo fi = new FileInfo(GetAssetFullPath(info_));
            int i;

            for (i = 0; i < m_AssetBuildInfo.Count; i++)
            {
                if (m_AssetBuildInfo[i].ID == info_.ID)
                {
                    break;
                }
            }

            if (i < m_AssetBuildInfo.Count)
            {
                return fi.LastAccessTime > m_AssetBuildInfo[i].ModificationTime
                    && AssetBuildParamCollection.Compare(m_AssetBuildInfo[i].Params, info_.Params) != 0;
            }

            return true;
        }

        /// <summary>
        /// get the file name of the asset
        /// </summary>
        /// <returns></returns>
        public string GetAssetFullPath(AssetInfo info_)
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

            fullpath += info_.FileName;

            return fullpath;
        }

        /// <summary>
        /// get the XNB file name of the asset
        /// </summary>
        /// <param name="info_"></param>
        /// <returns></returns>
        public string GetAssetXNBFullPath(AssetInfo info_)
        {
            return Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.GameDirPath + "\\Content\\" + info_.Name + ".xnb";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type_"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="?"></param>
        /// <param name="?"></param>
        /// <returns></returns>
        public string GetProcessorName(AssetType type_)
        {
            switch (type_)
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



        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        /// <returns></returns>
        public AssetInfo? GetAssetByName(string name_)
        {
            if (string.IsNullOrEmpty(name_) == true)
            {
                throw new ArgumentNullException("AssetManager.GetAssetByName() : the name is null or empty");
            }

            foreach (AssetInfo info in m_Assets)
            {
                if (info.Name.Equals(name_) == true)
                {
                    return info;
                }
            }

            return null;
        }

        /// <summary>
        /// Add the asset in the project.
        /// Copy the original in the project directory.
        /// Build the asset in xnb format
        /// </summary>
        /// <param name="fileName_">Full path of the file to add</param>
        /// <param name="name_">the asset name</param>
        /// <param name="type_">Type of the asset</param>
        /// <param name="assetFileName_">Set the full path in the project directory</param>
        /// <returns>return true if the asset is correctly built or if asset already exist</returns>
        public bool AddAsset(string fileName_, string name_, AssetType type_, ref string assetFileName_)
        {
            AssetInfo info = new AssetInfo(0, name_, type_, Path.GetFileName(fileName_));
            info.GetNewID();
            AddBuildParams(ref info);

            foreach (AssetInfo i in m_Assets)
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

                m_Assets.Add(info);
            }
            else
            {
                return false;
            }*/

            string assetFile = GetPathFromType(type_);
            assetFile += Path.DirectorySeparatorChar + Path.GetFileName(fileName_);
            assetFileName_ = assetFile;

            File.Copy(fileName_, assetFile, true);
            LogManager.Instance.WriteLineDebug("Asset copied : " + fileName_ + " -> " + assetFile);
            AddFileInSourceControl(assetFileName_);

            m_Assets.Add(info);

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type_"></param>
        /// <returns></returns>
        public string GetPathFromType(AssetType type_)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        /// <param name="type_"></param>
        public void DeleteAsset(string name_, AssetType type_)
        {
            foreach (AssetInfo info in m_Assets)
            {
                if (info.Name.Equals(name_)
                    && info.Type == type_)
                {
                    DeleteAsset(info);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="info_"></param>
        public void DeleteAsset(AssetInfo info_)
        {
            m_Assets.Remove(info_);
            LogManager.Instance.WriteLineDebug("Delete asset " + info_.Name + "(" + info_.FileName + ")");
        }

        /// <summary>
        /// 
        /// </summary>
        public void Clear()
        {
            m_Assets.Clear();
            LogManager.Instance.WriteLineDebug("Asset.Clear()");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        public void AddFileInSourceControl(string fileName_)
        {
            if (SourceControlManager.Instance.SourceControl.IsValidConnection() == true)
            {
                SourceControlManager.Instance.SourceControl.MarkFileForAdd(fileName_);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        public void DeleteFileInSourceControl(string fileName_)
        {
            if (SourceControlManager.Instance.SourceControl.IsValidConnection() == true)
            {
                Dictionary<string, Dictionary<SourceControlKeyWord, string>> dic = SourceControlManager.Instance.SourceControl.FileStatus(new string[] { fileName_ });

                if (dic.ContainsKey(fileName_) == true)
                {
                    if (dic[fileName_].ContainsKey(SourceControlKeyWord.Action) == true)
                    {
                        if (dic[fileName_][SourceControlKeyWord.Action].ToLower().Equals("add") == true)
                        {
                            SourceControlManager.Instance.SourceControl.RevertFile(fileName_);
                            File.Delete(fileName_);
                        }
                        else if (dic[fileName_][SourceControlKeyWord.Action].ToLower().Equals("edit") == true)
                        {
                            SourceControlManager.Instance.SourceControl.RevertFile(fileName_);
                            SourceControlManager.Instance.SourceControl.MarkFileForDelete(fileName_);
                        }
                    }
                    else
                    {
                        SourceControlManager.Instance.SourceControl.MarkFileForDelete(fileName_);
                    }
                }
                else
                {
                    File.Delete(fileName_);
                }
            }
            else
            {
                File.Delete(fileName_);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type_"></param>
        /// <returns></returns>
        public string[] GetAllAssetByType(AssetType type_)
        {
            List<string> res = new List<string>();

            foreach (AssetInfo info in m_Assets)
            {
                if (info.Type == type_)
                {
                    res.Add(info.Name);
                }
            }

            return res.ToArray();
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="?"></param>
        /// <returns></returns>
        public bool Save(XmlElement el_, SaveOption option_)
        {
            el_.OwnerDocument.AddAttribute(el_, "version", m_Version.ToString());
            XmlElement xmlEl;

            foreach (AssetInfo info in m_Assets)
            {
                xmlEl = el_.OwnerDocument.CreateElement(ProjectManager.NodeAssetName);
                el_.OwnerDocument.AddAttribute(xmlEl, "id", info.ID.ToString());
                el_.OwnerDocument.AddAttribute(xmlEl, "name", info.Name);
                el_.OwnerDocument.AddAttribute(xmlEl, "fileName", info.FileName);
                //el_.OwnerDocument.AddAttribute(xmlEl, "modificationDate", info.ModificationDate.ToString());
                el_.OwnerDocument.AddAttribute(xmlEl, "type", ((int)info.Type).ToString());

                XmlElement paramNodes = el_.OwnerDocument.CreateElement("BuildParameterList");
                xmlEl.AppendChild(paramNodes);

                foreach (AssetBuildParam param in info.Params)
                {
                    XmlElement buildNode = el_.OwnerDocument.CreateElement("BuildParameter");
                    param.Save(buildNode);
                    paramNodes.AppendChild(buildNode);
                }

                el_.AppendChild(xmlEl);
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        /// <returns></returns>
        public bool Load(XmlElement el_, SaveOption option_)
        {
            uint version = uint.Parse(el_.Attributes["version"].Value);

            m_Assets.Clear();

            foreach (XmlNode node in el_.SelectNodes(ProjectManager.NodeAssetName))
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
                        info.SetID(int.Parse(node.Attributes["id"].Value));
                    }
                    else
                    {
                        info.GetNewID();
                    }
                }
                else
                {
                    AddBuildParams(ref info);
                    info.GetNewID();
                }

                m_Assets.Add(info);

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

                m_AssetBuildInfo.Clear();

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

        /// <summary>
        /// 
        /// </summary>
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
                info.ID = int.Parse(node.Attributes["id"].Value);
                info.ModificationTime = DateTime.Parse(node.Attributes["date"].Value);
                info.Params = new AssetBuildParamCollection();

                foreach (XmlNode n in node.SelectSingleNode("BuildParameterList").SelectNodes("BuildParameter"))
                {
                    info.Params.Add(AssetBuildParamFactory.Load((XmlElement)n));
                }

                m_AssetBuildInfo.Add(info);
            }
        }

        /// <summary>
        /// 
        /// </summary>
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

            foreach (AssetBuildInfo info in m_AssetBuildInfo)
            {
                XmlElement node = xmlDoc.CreateElement("BuildInfo");
                elRoot.AppendChild(node);

                xmlDoc.AddAttribute(node, "id", info.ID.ToString());
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


        /// <summary>
        /// 
        /// </summary>
        /// <param name="assetInfo_"></param>
        private void AddBuildParams(ref AssetInfo assetInfo_)
        {
            switch (assetInfo_.Type)
            {
                case AssetType.Audio:
                    AssetBuildParamFactory.SetBuildParams(ref assetInfo_.Params, AssetBuildParamFactory.AssetBuildParamType.Audio);
                    break;

                case AssetType.Effect:
                    AssetBuildParamFactory.SetBuildParams(ref assetInfo_.Params, AssetBuildParamFactory.AssetBuildParamType.Effect);
                    break;

                case AssetType.SkinnedMesh:
                case AssetType.StaticMesh:
                    AssetBuildParamFactory.SetBuildParams(ref assetInfo_.Params, AssetBuildParamFactory.AssetBuildParamType.Model);
                    break;

                case AssetType.Texture:
                    AssetBuildParamFactory.SetBuildParams(ref assetInfo_.Params, AssetBuildParamFactory.AssetBuildParamType.Texture);
                    break;

                case AssetType.Video:
                    AssetBuildParamFactory.SetBuildParams(ref assetInfo_.Params, AssetBuildParamFactory.AssetBuildParamType.Video);
                    break;

                default:
                    throw new NotImplementedException("AssetManager.Load() : the type '" + Enum.GetName(typeof(AssetType), assetInfo_.Type) + "' is not supported.");
            }
        }

    }
}
