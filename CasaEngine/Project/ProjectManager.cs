using System.Xml;
using CasaEngineCommon.Design;
using CasaEngine.Game;

namespace CasaEngine.Project
{
    public
#if EDITOR
    partial
#endif
    class ProjectManager
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

            var xmlDoc = new XmlDocument();
            xmlDoc.Load(fileName);

            var projectNode = (XmlElement)xmlDoc.SelectSingleNode(NodeRootName);

            var configNode = (XmlElement)projectNode.SelectSingleNode(NodeConfigName);
            Engine.Instance.ProjectConfig.Load(configNode, SaveOption.Editor);

            /*XmlElement asset2DNode = (XmlElement)projectNode.SelectSingleNode(NodeAsset2DName);
            GameInfo.Instance.Asset2DManager.Load(asset2DNode, SaveOption.Editor);

#if EDITOR
            XmlElement assetNode = (XmlElement)projectNode.SelectSingleNode(NodeAssetListName);
            GameInfo.Instance.AssetManager.Load(assetNode, SaveOption.Editor);
#endif

            XmlElement objectRegistryNode = (XmlElement)projectNode.SelectSingleNode(NodeObjectRegistryName);
            Engine.Instance.ObjectManager.Load(objectRegistryNode, SaveOption.Editor);*/

            Engine.Instance.ObjectManager.Load(projectNode, SaveOption.Editor);

#if EDITOR
            Engine.Instance.ExternalToolManager.Initialize();

            LastXmlDocument = xmlDoc;

            if (ProjectLoaded != null)
            {
                ProjectLoaded(this, EventArgs.Empty);
            }
#endif
        }
    }
}
