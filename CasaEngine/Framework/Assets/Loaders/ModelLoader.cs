using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets.Animations;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.Loaders;

public class ModelLoader : IAssetLoader
{
    public bool IsFileSupported(string fileName)
    {
        string extension = Path.GetExtension(fileName);
        return extension.Equals(".gltf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".glb", StringComparison.OrdinalIgnoreCase);
    }

    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            Effect defaultEffect = null; //assetContentManager.LoadDirectly<Effect>("Shaders\\skinEffect.mgfxc");
            var riggedModelReader = new GltfRiggedModelReader(assetContentManager, defaultEffect);
            return riggedModelReader.LoadAsset(fileName, assetContentManager);
        }
        catch (Exception e)
        {
            Logs.WriteException(e);
        }

        return null;
    }
}