using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Rendering.Models;

public class SkinnedMesh : ObjectBase
{
    private readonly List<Guid> _animationClipAssetIds = new();

    public RiggedModel RiggedModel { get; private set; }
    public Guid RiggedModelAssetId { get; set; } = Guid.Empty;
    public Guid SkeletonAssetId { get; private set; } = Guid.Empty;
    public Guid DefaultAnimationClipAssetId { get; private set; } = Guid.Empty;
    public IReadOnlyList<Guid> AnimationClipAssetIds => _animationClipAssetIds;

    public void Initialize(AssetContentManager assetContentManager)
    {
        if (_isInitialized)
        {
            return;
        }

        if (RiggedModelAssetId != Guid.Empty)
        {
            RiggedModel = assetContentManager.Load<RiggedModel>(RiggedModelAssetId);
        }

        ApplySeparatedAnimationAssets(assetContentManager);

        _isInitialized = true;
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        var skinnedMeshAsset = new SkinnedMeshAsset();
        skinnedMeshAsset.Load(element);

        RiggedModelAssetId = skinnedMeshAsset.GeometryAssetId;
        SkeletonAssetId = skinnedMeshAsset.SkeletonAssetId;
        DefaultAnimationClipAssetId = skinnedMeshAsset.DefaultAnimationClipAssetId;

        _animationClipAssetIds.Clear();
        for (var clipIndex = 0; clipIndex < skinnedMeshAsset.AnimationClipAssetIds.Count; clipIndex++)
        {
            _animationClipAssetIds.Add(skinnedMeshAsset.AnimationClipAssetIds[clipIndex]);
        }
    }

    private bool _isInitialized;

    public void SetRiggedModel(RiggedModel riggedModel)
    {
        RiggedModel = riggedModel;
    }

    private void ApplySeparatedAnimationAssets(AssetContentManager assetContentManager)
    {
        if (RiggedModel == null
            || SkeletonAssetId == Guid.Empty
            || (DefaultAnimationClipAssetId == Guid.Empty && _animationClipAssetIds.Count == 0))
        {
            return;
        }

        var skeletonDefinition = assetContentManager.Load<SkeletonDefinition>(SkeletonAssetId);
        var animationClips = new List<AnimationClip>();
        var loadedAnimationClipAssetIds = new List<Guid>();

        if (DefaultAnimationClipAssetId != Guid.Empty)
        {
            AddAnimationClipAsset(assetContentManager, animationClips, loadedAnimationClipAssetIds, DefaultAnimationClipAssetId);
        }

        for (var clipIndex = 0; clipIndex < _animationClipAssetIds.Count; clipIndex++)
        {
            AddAnimationClipAsset(assetContentManager, animationClips, loadedAnimationClipAssetIds, _animationClipAssetIds[clipIndex]);
        }

        if (animationClips.Count > 0)
        {
            RiggedModel.OverrideRuntimeAnimationAssets(skeletonDefinition, animationClips);
        }
    }

    private static void AddAnimationClipAsset(
        AssetContentManager assetContentManager,
        List<AnimationClip> animationClips,
        List<Guid> loadedAnimationClipAssetIds,
        Guid animationClipAssetId)
    {
        for (var clipIndex = 0; clipIndex < loadedAnimationClipAssetIds.Count; clipIndex++)
        {
            if (loadedAnimationClipAssetIds[clipIndex] == animationClipAssetId)
            {
                return;
            }
        }

        var animationClip = assetContentManager.Load<AnimationClip>(animationClipAssetId);
        animationClips.Add(animationClip);
        loadedAnimationClipAssetIds.Add(animationClipAssetId);
    }
}