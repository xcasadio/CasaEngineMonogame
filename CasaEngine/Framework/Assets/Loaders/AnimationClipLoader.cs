using CasaEngine.Core.Logging;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Assets.Animations;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Loaders;

public sealed class AnimationClipLoader : IAssetLoader
{
    public bool IsFileSupported(string fileName)
        => Path.GetExtension(fileName).Equals(Constants.FileNameExtensions.SkeletonAnimation, StringComparison.OrdinalIgnoreCase);

    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            var jsonDocument = JObject.Parse(File.ReadAllText(fileName));
            var animationClipAsset = new AnimationClipAsset();
            animationClipAsset.Load(jsonDocument);

            if (animationClipAsset.SkeletonAssetId == Guid.Empty)
            {
                throw new InvalidOperationException($"Animation clip asset '{fileName}' has no skeleton reference.");
            }

            // ADR-0037: the clip keeps its skeleton, so it holds it and gives it back when it is freed.
            var skeletonHold = assetContentManager.Acquire<SkeletonDefinition>(animationClipAsset.SkeletonAssetId);
            try
            {
                var animationClip = AnimationAssetDataConverter.CreateAnimationClip(animationClipAsset, skeletonHold.Asset);
                animationClip.HoldSkeleton(skeletonHold);
                return animationClip;
            }
            catch
            {
                skeletonHold.Dispose();
                throw;
            }
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"[AnimationClipLoader] Cannot load animation clip asset '{fileName}'", exception));
            return null;
        }
    }
}