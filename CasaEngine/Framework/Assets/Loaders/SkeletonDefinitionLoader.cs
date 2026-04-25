using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets.Animations;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Loaders;

public sealed class SkeletonDefinitionLoader : IAssetLoader
{
    public bool IsFileSupported(string fileName)
        => Path.GetExtension(fileName).Equals(Constants.FileNameExtensions.Skeleton, StringComparison.OrdinalIgnoreCase);

    public object? LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            var jsonDocument = JObject.Parse(File.ReadAllText(fileName));
            var skeletonAsset = new SkeletonAsset();
            skeletonAsset.Load(jsonDocument);
            return AnimationAssetDataConverter.CreateSkeletonDefinition(skeletonAsset);
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"[SkeletonDefinitionLoader] Cannot load skeleton asset '{fileName}'", exception));
            return null;
        }
    }
}