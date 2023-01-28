using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using CasaEngine.Gameplay.Actor;
using CasaEngine;
using CasaEngineCommon.Design;
using CasaEngine.Game;
using System.IO;

namespace CasaEngine.Project
{
    /// <summary>
    /// 
    /// </summary>
    public
#if EDITOR
    partial
#endif
    class ProjectManager
    {


        static public readonly string NodeRootName = "Project";
        static public readonly string NodeWorldListName = "WorldList";
        static public readonly string NodeWorldName = "World";
        static public readonly string NodeScreenListName = "ScreenList";
        static public readonly string NodeScreenName = "Screen";
        static public readonly string NodeAsset2DName = "Asset2D";
        static public readonly string NodeSprite2DListName = "Sprite2DList";
        static public readonly string NodeSprite2DName = "Sprite2D";
        static public readonly string NodeAnimation2DListName = "Animation2DList";
        static public readonly string NodeAnimation2DName = "Animation2D";
        static public readonly string NodeAsset3DName = "Asset3D";
        static public readonly string NodeAnimation3DListName = "Animation3DList";
        static public readonly string NodeAnimation3DName = "Animation3D";
        static public readonly string NodeAssetListName = "AssetList";
        static public readonly string NodeAssetName = "Asset";
        static public readonly string NodeObjectRegistryName = "ObjectRegistry";
        static public readonly string NodeObjectListName = "Objects";
        static public readonly string NodeObjectName = "Object";
        static public readonly string NodeConfigName = "Config";




        //Dir name
        //static public readonly string AnimationDirName = "Animation";
        static public readonly string AssetDirName = "Asset";
        //static public readonly string EffectDirName = "Effect";
        static public readonly string ExternalToolsDirName = "ExternTools";
        //static public readonly string ModelDirName = "Model";
        //static public readonly string XNBDirName = "XNB";
        //static public readonly string SoundDirName = "Sound";
        //static public readonly string ImageDirName = "Images";
        //static public readonly string VideoDirName = "Video";
        //static readonly string WorldDirName = "Worlds";

        //dir path
        static public readonly string AssetDirPath = AssetDirName;
        //asset
        //static public readonly string AnimationDirPath = AssetDirPath + Path.DirectorySeparatorChar + AnimationDirName;
        //static public readonly string EffectDirPath = AssetDirPath + Path.DirectorySeparatorChar + EffectDirName;
        //static public readonly string ModelDirPath = AssetDirPath + Path.DirectorySeparatorChar + ModelDirName;
        //static public readonly string XNBDirPath = AssetDirPath + Path.DirectorySeparatorChar + XNBDirName;
        //static public readonly string SoundDirPath = AssetDirPath + Path.DirectorySeparatorChar + SoundDirName;
        //static public readonly string ImageDirPath = AssetDirPath + Path.DirectorySeparatorChar + ImageDirName;
        //static public readonly string VideoDirPath = AssetDirPath + Path.DirectorySeparatorChar + VideoDirName;
        //editor
        static public readonly string ExternalToolsDirPath = ExternalToolsDirName;
        static public readonly string GameDirPath = "Game";
        static public readonly string ConfigDirPath = "Config";
        //static public readonly string PackageDirPath = "Packages";



        /// <summary>
        /// Gets the project full path (ends with directory separator)
        /// </summary>
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


        /// <summary>
        /// Load project from file
        /// </summary>
        /// <param name="fileName_"></param>
        public void Load(string fileName_)
        {
#if EDITOR
            Clear();
            ProjectFileOpened = fileName_;
#endif

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(fileName_);

            XmlElement projectNode = (XmlElement)xmlDoc.SelectSingleNode(NodeRootName);

            XmlElement configNode = (XmlElement)projectNode.SelectSingleNode(NodeConfigName);
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
