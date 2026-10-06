using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio.Mixing;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Loaders;

/// <summary>Loads a <c>.audioMixer</c> authoring document.</summary>
public sealed class AudioMixerAssetLoader : IAssetLoader
{
    public bool IsFileSupported(string fileName)
        => Path.GetExtension(fileName).Equals(Constants.FileNameExtensions.AudioMixer, StringComparison.OrdinalIgnoreCase);

    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            var jsonDocument = JObject.Parse(File.ReadAllText(fileName));
            var asset = new AudioMixerAsset();
            asset.Load(jsonDocument);
            return asset;
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"[AudioMixerAssetLoader] Cannot load audio mixer asset '{fileName}'", exception));
            return null;
        }
    }
}
