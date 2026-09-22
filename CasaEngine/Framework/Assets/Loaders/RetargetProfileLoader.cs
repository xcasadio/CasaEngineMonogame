using CasaEngine.Core.Logging;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Assets.Animations;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Loaders;

public sealed class RetargetProfileLoader : IAssetLoader
{
    public bool IsFileSupported(string fileName)
        => Path.GetExtension(fileName).Equals(Constants.FileNameExtensions.RetargetProfile, StringComparison.OrdinalIgnoreCase);

    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
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

            // ADR-0037: the profile keeps both skeletons, so it holds them and gives them back when it is freed.
            var sourceHold = assetContentManager.Acquire<SkeletonDefinition>(retargetProfileAsset.SourceSkeletonAssetId);
            AssetHandle<SkeletonDefinition> targetHold = null;
            try
            {
                targetHold = assetContentManager.Acquire<SkeletonDefinition>(retargetProfileAsset.TargetSkeletonAssetId);
                var retargetProfile = RetargetProfileAssetDataConverter.CreateRetargetProfile(
                    retargetProfileAsset, sourceHold.Asset, targetHold.Asset);
                retargetProfile.HoldSkeletons(sourceHold, targetHold);
                return retargetProfile;
            }
            catch
            {
                targetHold?.Dispose();
                sourceHold.Dispose();
                throw;
            }
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"[RetargetProfileLoader] Cannot load retarget profile asset '{fileName}'", exception));
            return null;
        }
    }
}