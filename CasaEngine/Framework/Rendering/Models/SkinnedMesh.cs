using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Rendering.Models;

public class SkinnedMesh : ObjectBase, IDisposable
{
    private readonly List<Guid> _animationClipAssetIds = new();

    // ADR-0037: the rigged model, its skeleton and its animation clips are shared assets, held through
    // counted handles for as long as this SkinnedMesh references them. Given back in Dispose, called by
    // the asset manager when it frees this SkinnedMesh (P10). RiggedModel is freed by the collection as an
    // IAssetable once nobody holds it any more (T1.1).
    private AssetHandle<RiggedModel> _riggedModelHandle;
    private AssetHandle<SkeletonDefinition> _skeletonHandle;
    private readonly List<AssetHandle<AnimationClip>> _animationClipHandles = new();

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
            _riggedModelHandle = assetContentManager.Acquire<RiggedModel>(RiggedModelAssetId);
            RiggedModel = _riggedModelHandle.Asset;
        }

        ApplySeparatedAnimationAssets(assetContentManager);

        _isInitialized = true;
    }

    /// <summary>
    /// Gives back the handles held on the rigged model, its skeleton and its animation clips (ADR-0037).
    /// The asset manager calls it when it frees this SkinnedMesh (<see cref="IDisposable"/>).
    /// </summary>
    public void Dispose()
    {
        _riggedModelHandle?.Dispose();
        _riggedModelHandle = null;

        _skeletonHandle?.Dispose();
        _skeletonHandle = null;

        for (var index = 0; index < _animationClipHandles.Count; index++)
        {
            _animationClipHandles[index].Dispose();
        }

        _animationClipHandles.Clear();
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

        _skeletonHandle = assetContentManager.Acquire<SkeletonDefinition>(SkeletonAssetId);
        var skeletonDefinition = _skeletonHandle.Asset;
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

    private void AddAnimationClipAsset(
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

        var animationClipHandle = assetContentManager.Acquire<AnimationClip>(animationClipAssetId);
        _animationClipHandles.Add(animationClipHandle);
        animationClips.Add(animationClipHandle.Asset);
        loadedAnimationClipAssetIds.Add(animationClipAssetId);
    }
}