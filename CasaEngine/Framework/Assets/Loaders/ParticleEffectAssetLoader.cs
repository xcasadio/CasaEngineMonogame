using CasaEngine.Core.Logging;
using CasaEngine.Framework.Particles.Authoring;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Loaders;

public sealed class ParticleEffectAssetLoader : IAssetLoader
{
    public bool IsFileSupported(string fileName)
        => Path.GetExtension(fileName).Equals(Constants.FileNameExtensions.Particle, StringComparison.OrdinalIgnoreCase);

    public object? LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            var jsonDocument = JObject.Parse(File.ReadAllText(fileName));
            var asset = new ParticleEffectAsset();
            asset.Load(jsonDocument);
            return asset;
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"[ParticleEffectAssetLoader] Cannot load particle effect asset '{fileName}'", exception));
            return null;
        }
    }
}