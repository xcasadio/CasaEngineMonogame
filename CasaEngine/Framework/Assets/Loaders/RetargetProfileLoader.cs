using CasaEngine.Core.Logging;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Assets.Animations;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Loaders;

public sealed class RetargetProfileLoader : IAssetLoader
{
    public bool IsFileSupported(string fileName)
        => Path.GetExtension(fileName).Equals(Constants.FileNameExtensions.RetargetProfile, StringComparison.OrdinalIgnoreCase);

    public object? LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            var jsonDocument = JObject.Parse(File.ReadAllText(fileName));
            var retargetProfileAsset = new RetargetProfileAsset();
            retargetProfileAsset.Load(jsonDocument);

            if (retargetProfileAsset.SourceSkeletonAssetId == Guid.Empty)
            {
                throw new InvalidOperationException($"Retarget profile asset '{fileName}' has no source skeleton reference.");
            }

            if (retargetProfileAsset.TargetSkeletonAssetId == Guid.Empty)
            {
                throw new InvalidOperationException($"Retarget profile asset '{fileName}' has no target skeleton reference.");
            }

            var sourceSkeleton = assetContentManager.Load<SkeletonDefinition>(retargetProfileAsset.SourceSkeletonAssetId);
            var targetSkeleton = assetContentManager.Load<SkeletonDefinition>(retargetProfileAsset.TargetSkeletonAssetId);
            return RetargetProfileAssetDataConverter.CreateRetargetProfile(retargetProfileAsset, sourceSkeleton, targetSkeleton);
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"[RetargetProfileLoader] Cannot load retarget profile asset '{fileName}'", exception));
            return null;
        }
    }
}