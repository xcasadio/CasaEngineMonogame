using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Loaders;

/// <summary>Loads a <c>.sound</c> authoring document.</summary>
public sealed class SoundAssetLoader : IAssetLoader
{
    public bool IsFileSupported(string fileName)
        => Path.GetExtension(fileName).Equals(Constants.FileNameExtensions.Sound, StringComparison.OrdinalIgnoreCase);

    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            var jsonDocument = JObject.Parse(File.ReadAllText(fileName));
            var asset = new SoundAsset();
            asset.Load(jsonDocument);
            return asset;
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"[SoundAssetLoader] Cannot load sound asset '{fileName}'", exception));
            return null;
        }
    }
}
