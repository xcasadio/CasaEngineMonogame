namespace CasaEngine.Framework.Project;

public class ProjectManager
{
    public static readonly string NodeRootName = "Project";
    public static readonly string NodeWorldListName = "WorldList";
    public static readonly string NodeWorldName = "World";
    public static readonly string NodeScreenListName = "ScreenList";
    public static readonly string NodeScreenName = "Screen";
    public static readonly string NodeAsset2DName = "Asset2D";
    public static readonly string NodeSprite2DListName = "Sprite2DList";
    public static readonly string NodeSprite2DName = "Sprite2D";
    public static readonly string NodeAnimation2DListName = "Animation2DList";
    public static readonly string NodeAnimation2DName = "Animation2D";
    public static readonly string NodeAsset3DName = "Asset3D";
    public static readonly string NodeAnimation3DListName = "Animation3DList";
    public static readonly string NodeAnimation3DName = "Animation3D";
    public static readonly string NodeAssetListName = "AssetList";
    public static readonly string NodeAssetName = "Asset";
    public static readonly string NodeObjectRegistryName = "ObjectRegistry";
    public static readonly string NodeObjectListName = "Objects";
    public static readonly string NodeObjectName = "Object";
    public static readonly string NodeConfigName = "Config";

    //Dir name
    //static public readonly string AnimationDirName = "Animation";
    public static readonly string AssetDirName = "Asset";
    //static public readonly string EffectDirName = "Effect";
    public static readonly string ExternalToolsDirName = "ExternTools";
    //static public readonly string ModelDirName = "Model";
    //static public readonly string XNBDirName = "XNB";
    //static public readonly string SoundDirName = "Sound";
    //static public readonly string ImageDirName = "Images";
    //static public readonly string VideoDirName = "Video";
    //static readonly string WorldDirName = "Worlds";

    //dir path
    public static readonly string AssetDirPath = AssetDirName;
    //asset
    //static public readonly string AnimationDirPath = AssetDirPath + Path.DirectorySeparatorChar + AnimationDirName;
    //static public readonly string EffectDirPath = AssetDirPath + Path.DirectorySeparatorChar + EffectDirName;
    //static public readonly string ModelDirPath = AssetDirPath + Path.DirectorySeparatorChar + ModelDirName;
    //static public readonly string XNBDirPath = AssetDirPath + Path.DirectorySeparatorChar + XNBDirName;
    //static public readonly string SoundDirPath = AssetDirPath + Path.DirectorySeparatorChar + SoundDirName;
    //static public readonly string ImageDirPath = AssetDirPath + Path.DirectorySeparatorChar + ImageDirName;
    //static public readonly string VideoDirPath = AssetDirPath + Path.DirectorySeparatorChar + VideoDirName;
    //editor
    public static readonly string ExternalToolsDirPath = ExternalToolsDirName;
    public static readonly string GameDirPath = "Game";
    public static readonly string ConfigDirPath = "Config";
    //static public readonly string PackageDirPath = "Packages";

    public string ProjectPath
    {
        get
        {
#if EDITOR
            return Path.GetDirectoryName(ProjectFileOpened);
#else
                return Environment.CurrentDirectory;
#endif
        }
    }

    public void Load(string fileName)
    {
#if EDITOR
        Clear();
        ProjectFileOpened = fileName;
#endif

        //var xmlDoc = new XmlDocument();
        //xmlDoc.Load(fileName);
        //
        //var projectNode = (XmlElement)xmlDoc.SelectSingleNode(NodeRootName);
        //
        //var configNode = (XmlElement)projectNode.SelectSingleNode(NodeConfigName);
        //CasaEngine.Game.EngineComponents.ProjectSettings.Load(configNode, SaveOption.Editor);

        /*XmlElement asset2DNode = (XmlElement)projectNode.SelectSingleNode(NodeAsset2DName);
        GameInfo.Instance.Asset2DManager.Load(asset2DNode, SaveOption.Editor);

#if EDITOR
        XmlElement assetNode = (XmlElement)projectNode.SelectSingleNode(NodeAssetListName);
        GameInfo.Instance.AssetManager.Load(assetNode, SaveOption.Editor);
#endif

        XmlElement objectRegistryNode = (XmlElement)projectNode.SelectSingleNode(NodeObjectRegistryName);
        CasaEngine.Game.EngineComponents.ObjectManager.Load(objectRegistryNode, SaveOption.Editor);*/

        //CasaEngine.Game.EngineComponents.ObjectManager.Load(projectNode, SaveOption.Editor);

#if EDITOR
        EngineComponents.ExternalToolManager.Initialize();

        ProjectLoaded?.Invoke(this, EventArgs.Empty);
#endif
    }


#if EDITOR
    private static readonly uint Version = 1;

    public event EventHandler? ProjectLoaded;
    public event EventHandler? ProjectClosed;

    public string ProjectFileOpened { get; set; }

    public void Clear()
    {
        EngineComponents.AssetManager.Clear();
        EngineComponents.Asset2DManager.Clear();
        EngineComponents.ExternalToolManager.Clear();
        ProjectFileOpened = null;

        ProjectClosed?.Invoke(this, EventArgs.Empty);
    }

    public void CreateProject(string fileName)
    {
#if !DEBUG
            try
            {
#endif

        Clear();
        CreateProjectDirectoryHierarchy(Path.GetDirectoryName(fileName));
        Save(fileName);

#if !DEBUG
            }
            catch (System.Exception e)
            {

            }
#endif
    }

    private void CreateProjectDirectoryHierarchy(string path)
    {
        Directory.CreateDirectory(path + Path.DirectorySeparatorChar + AssetDirPath);
        //System.IO.Directory.CreateDirectory(path_ + Path.DirectorySeparatorChar + ImageDirPath);
        Directory.CreateDirectory(path + Path.DirectorySeparatorChar + ExternalToolsDirPath);
        //System.IO.Directory.CreateDirectory(path_ + Path.DirectorySeparatorChar + SoundDirPath);
        //model
        //video
        //System.IO.Directory.CreateDirectory(path_ + Path.DirectorySeparatorChar + PackageDirPath);
        Directory.CreateDirectory(path + Path.DirectorySeparatorChar + GameDirPath);
        Directory.CreateDirectory(path + Path.DirectorySeparatorChar + GameDirPath + "\\Content");
        Directory.CreateDirectory(path + Path.DirectorySeparatorChar + ConfigDirPath);
    }

    public bool Save(string fileName)
    {
        ProjectFileOpened = fileName;

        //nouveau fichier
        //var xmlDoc = new XmlDocument();
        //var projectNode = xmlDoc.AddRootNode(NodeRootName);
        //xmlDoc.AddAttribute(projectNode, "version", Version.ToString());
        //
        //var configNode = xmlDoc.CreateElement(NodeConfigName);
        //projectNode.AppendChild(configNode);
        //CasaEngine.Game.EngineComponents.ProjectSettings.Save(configNode, SaveOption.Editor);

        //liste des mondes
        /*XmlElement worldListNode = xmlDoc.CreateElement("WorldList");
        projectNode.AppendChild(worldListNode);
        foreach (string str in _WorldList)
        {
            XmlElement worldNode = xmlDoc.CreateElementWithText("World", str);
            worldListNode.AppendChild(worldNode);
        }

        //liste screen
        XmlElement screendListNode = xmlDoc.CreateElement("ScreenList");
        projectNode.AppendChild(screendListNode);
        foreach (KeyValuePair<string, string> pair in _ScreenList)
        {
            XmlElement screenNode = xmlDoc.CreateElement("Screen");
            xmlDoc.AddAttribute(screenNode, "key", pair.Key);
            xmlDoc.AddAttribute(screenNode, "value", pair.Value);
            screendListNode.AppendChild(screenNode);
        }*/

        /*XmlElement assetNode = xmlDoc.CreateElement(NodeAsset2DName);
        projectNode.AppendChild(assetNode);
        GameInfo.Instance.Asset2DManager.Save(assetNode, SaveOption.Editor);*/

        /*XmlElement asset3DNode = xmlDoc.CreateElement("Asset3D");
        projectNode.AppendChild(asset3DNode);
        GameInfo.Instance.Asset3DManager.Save(asset3DNode);*/

        /*XmlElement assetListNode = xmlDoc.CreateElement(NodeAssetListName);
        projectNode.AppendChild(assetListNode);
        GameInfo.Instance.AssetManager.Save(assetListNode, SaveOption.Editor);*/

        /*XmlElement objectRegistryNode = xmlDoc.CreateElement(NodeObjectRegistryName);
        projectNode.AppendChild(objectRegistryNode);
        GameInfo.Instance.ObjectRegistry.Save(objectRegistryNode, SaveOption.Editor);*/

        //XmlElement objectManagerNode = xmlDoc.CreateElement(NodeObjectListName);
        //projectNode.AppendChild(objectManagerNode);
        //CasaEngine.Game.EngineComponents.ObjectManager.Save(projectNode, SaveOption.Editor);

        //xmlDoc.Save(fileName);

        //LastXmlDocument = xmlDoc;

        //Monde
        //on ne peut sauvegarder que le monde courant
        //donc a chaque fois on sauve le projet puis on sauve le monde
        //bool res = SaveCurrentWorld();

        //il faut mixer les deux fichiers
        //non necessaire ??
        /*if (xmlDocLastFile != null)
        {
            res = MixOldProjectFileWithNewProjectFile(xmlDocLastFile, xmlDoc);
        }*/

        return true;
    }

#endif
}