using CasaEngine.Core.Logging;
using CasaEngine.Framework.Rendering.Environment;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Loaders;

public sealed class EnvironmentAssetLoader : IAssetLoader
{
    public bool IsFileSupported(string fileName)
        => Path.GetExtension(fileName).Equals(Constants.FileNameExtensions.Environment, StringComparison.OrdinalIgnoreCase);

    public object? LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            var jsonDocument = JObject.Parse(File.ReadAllText(fileName));
            var environmentAsset = new EnvironmentAsset();
            environmentAsset.Load(jsonDocument);
            return environmentAsset;
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"[EnvironmentAssetLoader] Cannot load environment asset '{fileName}'", exception));
            return null;
        }
    }
}