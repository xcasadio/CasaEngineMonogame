using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public sealed class SkinnedMeshAsset : ObjectBase
{
    public Guid SkeletonAssetId { get; set; } = Guid.Empty;

    public Guid GeometryAssetId { get; set; } = Guid.Empty;

    public Guid DefaultAnimationClipAssetId { get; set; } = Guid.Empty;

    public List<Guid> AnimationClipAssetIds { get; } = new();

    public override void Load(JObject element)
    {
        base.Load(element);
        SkinnedMeshAssetJsonSerializer.Load(this, element);
    }
}

public static class SkinnedMeshAssetJsonSerializer
{
    public static void Save(SkinnedMeshAsset skinnedMeshAsset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(skinnedMeshAsset);
        ArgumentNullException.ThrowIfNull(node);

        node["id"] = skinnedMeshAsset.Id.ToString();
        node["name"] = skinnedMeshAsset.Name;
        node["skeleton_asset_id"] = skinnedMeshAsset.SkeletonAssetId.ToString();
        node["geometry_asset_id"] = skinnedMeshAsset.GeometryAssetId.ToString();

        if (skinnedMeshAsset.DefaultAnimationClipAssetId != Guid.Empty)
        {
            node["default_animation_clip_asset_id"] = skinnedMeshAsset.DefaultAnimationClipAssetId.ToString();
        }

        var animationClipIdsNode = new JArray();
        for (var index = 0; index < skinnedMeshAsset.AnimationClipAssetIds.Count; index++)
        {
            animationClipIdsNode.Add(skinnedMeshAsset.AnimationClipAssetIds[index].ToString());
        }

        node["animation_clip_asset_ids"] = animationClipIdsNode;
    }

    public static void Load(SkinnedMeshAsset skinnedMeshAsset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(skinnedMeshAsset);
        ArgumentNullException.ThrowIfNull(node);

        skinnedMeshAsset.SkeletonAssetId = node["skeleton_asset_id"]?.GetGuid() ?? Guid.Empty;
        skinnedMeshAsset.GeometryAssetId = node["geometry_asset_id"]?.GetGuid()
            ?? node["rigged_model_asset_id"]?.GetGuid()
            ?? Guid.Empty;
        skinnedMeshAsset.DefaultAnimationClipAssetId = node["default_animation_clip_asset_id"]?.GetGuid() ?? Guid.Empty;

        skinnedMeshAsset.AnimationClipAssetIds.Clear();
        if (node["animation_clip_asset_ids"] is not JArray animationClipIdsNode)
        {
            return;
        }

        for (var index = 0; index < animationClipIdsNode.Count; index++)
        {
            if (animationClipIdsNode[index] is { } animationClipIdToken)
            {
                skinnedMeshAsset.AnimationClipAssetIds.Add(animationClipIdToken.GetGuid());
            }
        }
    }
}