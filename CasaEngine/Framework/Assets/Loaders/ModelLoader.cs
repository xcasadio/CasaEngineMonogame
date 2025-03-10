﻿using Assimp;
using CasaEngine.Core.Log;
using CasaEngine.Engine.Animations;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.Loaders;

public class ModelLoader : IAssetLoader
{
    private readonly AssimpContext _assimpContext = new();

    public bool IsFileSupported(string fileName)
    {
        return _assimpContext.GetSupportedImportFormats().Contains(Path.GetExtension(fileName).ToLower());
    }

    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            Effect defaultEffect = null; //assetContentManager.LoadDirectly<Effect>("Shaders\\skinEffect.mgfxc");
            var riggedModelLoader = new RiggedModelLoader(assetContentManager, defaultEffect);
            return riggedModelLoader.LoadAsset(fileName, assetContentManager);
        }
        catch (Exception e)
        {
            Logs.WriteException(e);
        }

        return null;
    }
}