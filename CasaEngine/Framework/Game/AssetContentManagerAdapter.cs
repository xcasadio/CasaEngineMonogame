using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.GUI.Neoforce.Skins;

namespace CasaEngine.Framework.Game;

public class AssetContentManagerAdapter : IArchiveManager
{
    private readonly AssetContentManager _assetContentManager;

    public AssetContentManagerAdapter(AssetContentManager assetContentManager)
    {
        _assetContentManager = assetContentManager;
    }

    public string RootDirectory { get; set; }
    public bool UseArchive { get; set; }

    public string[] GetDirectories(string folder)
    {
        return null;
    }

    public void Unload()
    {

    }

    public void Dispose()
    {
        Unload();
    }

    public T Load<T>(string asset)
    {
        var assetRelativePath = asset
            .Replace(EngineEnvironment.ProjectPath, string.Empty)
            .TrimStart('\\');
        //var assetInfo = AssetCatalog.Get(assetRelativePath);
        return _assetContentManager.LoadDirectly<T>(assetRelativePath);
    }
}