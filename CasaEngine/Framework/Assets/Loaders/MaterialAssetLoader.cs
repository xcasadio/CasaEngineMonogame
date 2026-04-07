using CasaEngine.Core.Logging;

using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Loaders;

public sealed class MaterialAssetLoader : IAssetLoader
{
    public bool IsFileSupported(string fileName)
        => Path.GetExtension(fileName).Equals(Constants.FileNameExtensions.Material, StringComparison.OrdinalIgnoreCase);

    public object? LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            var jsonDocument = JObject.Parse(File.ReadAllText(fileName));
            var materialAsset = new MaterialAsset();
            materialAsset.Load(jsonDocument);
            return materialAsset;
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"[MaterialAssetLoader] Cannot load material asset '{fileName}'", exception));
            return null;
        }
    }
}