using System.Xml;
using CasaEngine.Core.Extension;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Project;
using CasaEngine.Core.Design;
using CasaEngine.Framework;

namespace CasaEngine.Editor.Assets;

public class AssetManager
{

    private static readonly uint Version = 1;

    private readonly ContentBuilder.ContentBuilder _contentBuilder;
    private ContentBuilder.ContentBuilder _contentBuilderTempFiles;

    private readonly List<AssetInfo> _assets = new();

    //for temporary task
    private readonly FileSystemWatcher _fileWatcher;
    private readonly List<string> _assetToCopy = new();



    public AssetInfo[] Assets => _assets.ToArray();


    public AssetManager()
    {
        _contentBuilder = new ContentBuilder.ContentBuilder();
        _contentBuilderTempFiles = new ContentBuilder.ContentBuilder();

        _fileWatcher = new FileSystemWatcher(_contentBuilder.OutputDirectory.Replace("bin/Content", ""), "*.xnb");
        _fileWatcher.IncludeSubdirectories = true;
        _fileWatcher.EnableRaisingEvents = true;
        _fileWatcher.Created += _FileWatcher_Created;
    }


    private void _FileWatcher_Created(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType == WatcherChangeTypes.Created)
        {
            _assetToCopy.Add(e.FullPath);
        }
    }


    public void AddAssetToBuild(string fileName, string assetName, AssetType assetType)
    {
        //ProjectItem item;
        //
        //// Build this new model data.
        //switch (assetType)
        //{
        //    case AssetType.Audio:
        //        item = _contentBuilder.Add(fileName, assetName, "WavImporter", "SoundEffectProcessor");
        //        break;
        //
        //    case AssetType.Texture:
        //        item = _contentBuilder.Add(fileName, assetName, "TextureImporter", "TextureProcessor");
        //        break;
        //
        //    case AssetType.None:
        //        item = _contentBuilder.Add(fileName, assetName);
        //        break;
        //
        //    case AssetType.All:
        //        item = _contentBuilder.Add(fileName, assetName);
        //        break;
        //}
    }

    public void BuildAll(string destPath)
    {
        //var buildError = _contentBuilder.Build();
        //
        //if (string.IsNullOrEmpty(buildError) == false)
        //{
        //    LogManager.Instance.WriteLineError(buildError);
        //    return;
        //}
        //
        //var contentDir = _contentBuilder.OutputDirectory.Replace("/", "\\");
        //
        //foreach (var file in _assetToCopy)
        //{
        //    var fileDest = file.Replace(contentDir, file);
        //    File.Copy(
        //        file,
        //        fileDest,
        //        true);
        //}
        //
        //_assetToCopy.Clear();
        //_contentBuilder.Clear();
    }




    public bool BuildAsset(string fileName, AssetInfo info, bool copy = true)
    {
        //ProjectItem item;
        //
        //_contentBuilder.Clear();
        //
        //// Build this new model data.
        //switch (info.Type)
        //{
        //    case AssetType.Audio:
        //        item = _contentBuilder.Add(fileName, info.Name, "WavImporter", "SoundEffectProcessor");
        //        break;
        //
        //    case AssetType.Texture:
        //        item = _contentBuilder.Add(fileName, info.Name, "TextureImporter", "TextureProcessor");
        //        break;
        //
        //    case AssetType.None:
        //        item = _contentBuilder.Add(fileName, info.Name);
        //        break;
        //
        //    case AssetType.All:
        //        item = _contentBuilder.Add(fileName, info.Name);
        //        break;
        //}

        //var buildError = _contentBuilder.Build();

        foreach (var file in _assetToCopy)
        {
            File.Copy(
                file,
                /*GameInfo.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.XNBDirPath + Path.DirectorySeparatorChar + Path.GetFileName(file)*/
                EngineComponents.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.GameDirPath + "\\Content\\" + Path.GetFileName(file),
                true);
        }

        _assetToCopy.Clear();

        LogManager.Instance.WriteLine("Build asset '" + info.Name + "' successfull");

        return true;
    }

    public bool RebuildAsset(AssetInfo info)
    {
        _assets.Remove(info);

        var file = GetAssetXnbFullPath(info);
        File.Delete(file);

        var fullpath = GetAssetFullPath(info);

        //FileInfo fileInfo = new FileInfo(fullpath);
        //info_.ModificationDate = fileInfo.LastWriteTime;

        _assets.Add(info);

        return BuildAsset(fullpath, info);
    }



    public string GetAssetFullPath(AssetInfo info)
    {
        var fullpath = EngineComponents.ProjectManager.ProjectPath + Path.DirectorySeparatorChar;

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
        return EngineComponents.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.GameDirPath + "\\Content\\" + info.Name + ".xnb";
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
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentNullException("AssetManager.GetAssetByName() : the name is null or empty");
        }

        foreach (var info in _assets)
        {
            if (info.Name.Equals(name))
            {
                return info;
            }
        }

        return null;
    }

    public bool AddAsset(string fileName, string name, AssetType type, ref string assetFileName)
    {
        var info = new AssetInfo(0, name, type, Path.GetFileName(fileName));
        info.GetNewId();

        foreach (var i in _assets)
        {
            if (i.Equals(info))
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

        var assetFile = GetPathFromType(type);
        assetFile += Path.DirectorySeparatorChar + Path.GetFileName(fileName);
        assetFileName = assetFile;

        File.Copy(fileName, assetFile, true);
        LogManager.Instance.WriteLineDebug("Asset copied : " + fileName + " -> " + assetFile);

        _assets.Add(info);

        return true;
    }

    public string GetPathFromType(AssetType type)
    {
        var assetFile = EngineComponents.ProjectManager.ProjectPath + Path.DirectorySeparatorChar;

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
        foreach (var info in _assets)
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
        LogManager.Instance.WriteLineDebug("All asset was cleared.");
    }

    public string[] GetAllAssetByType(AssetType type)
    {
        var res = new List<string>();

        foreach (var info in _assets)
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

        foreach (var info in _assets)
        {
            xmlEl = el.OwnerDocument.CreateElement(ProjectManager.NodeAssetName);
            el.OwnerDocument.AddAttribute(xmlEl, "id", info.Id.ToString());
            el.OwnerDocument.AddAttribute(xmlEl, "name", info.Name);
            el.OwnerDocument.AddAttribute(xmlEl, "fileName", info.FileName);
            //el_.OwnerDocument.AddAttribute(xmlEl, "modificationDate", info.ModificationDate.ToString());
            el.OwnerDocument.AddAttribute(xmlEl, "type", ((int)info.Type).ToString());

            var paramNodes = el.OwnerDocument.CreateElement("BuildParameterList");
            xmlEl.AppendChild(paramNodes);


            el.AppendChild(xmlEl);
        }

        return true;
    }

    public bool Load(XmlElement el, SaveOption option)
    {
        var version = uint.Parse(el.Attributes["version"].Value);

        _assets.Clear();

        foreach (XmlNode node in el.SelectNodes(ProjectManager.NodeAssetName))
        {
            var info = new AssetInfo(
                0,
                node.Attributes["name"].Value,
                (AssetType)Enum.Parse(typeof(AssetType), node.Attributes["type"].Value),
                node.Attributes["fileName"].Value);

            if (version > 1)
            {
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
        }

        return true;
    }
}