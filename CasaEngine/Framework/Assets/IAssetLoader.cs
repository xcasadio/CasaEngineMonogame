namespace CasaEngine.Framework.Assets;

public interface IAssetLoader
{
    object LoadAsset(string fileName, AssetContentManager assetContentManager);
    bool IsFileSupported(string fileName);
}