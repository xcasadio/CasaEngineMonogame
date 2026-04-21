using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Animations;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public sealed class RetargetProfileAsset : ObjectBase
{
    public Guid SourceSkeletonAssetId { get; set; } = Guid.Empty;

    public Guid TargetSkeletonAssetId { get; set; } = Guid.Empty;

    public RetargetReferencePoseMode ReferencePoseMode { get; set; } = RetargetReferencePoseMode.BindPose;

    public RetargetAxis SourceForwardAxis { get; set; } = RetargetAxis.PositiveZ;

    public RetargetAxis SourceUpAxis { get; set; } = RetargetAxis.PositiveY;

    public RetargetAxis TargetForwardAxis { get; set; } = RetargetAxis.PositiveZ;

    public RetargetAxis TargetUpAxis { get; set; } = RetargetAxis.PositiveY;

    public float RootTranslationScale { get; set; } = 1f;

    public List<RetargetJointMappingAsset> JointMappings { get; } = new();

    public override void Load(JObject element)
    {
        base.Load(element);
        RetargetProfileAssetJsonSerializer.Load(this, element);
    }
}

public sealed class RetargetJointMappingAsset
{
    public string SourceJointName { get; set; } = string.Empty;

    public string TargetJointName { get; set; } = string.Empty;

    public float TranslationScale { get; set; } = 1f;
}

public static class RetargetProfileAssetJsonSerializer
{
    public static void Save(RetargetProfileAsset retargetProfileAsset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(retargetProfileAsset);
        ArgumentNullException.ThrowIfNull(node);

        node["id"] = retargetProfileAsset.Id.ToString();
        node["name"] = retargetProfileAsset.Name;
        node["source_skeleton_asset_id"] = retargetProfileAsset.SourceSkeletonAssetId.ToString();
        node["target_skeleton_asset_id"] = retargetProfileAsset.TargetSkeletonAssetId.ToString();
        node["reference_pose_mode"] = retargetProfileAsset.ReferencePoseMode.ToString();
        node["source_forward_axis"] = retargetProfileAsset.SourceForwardAxis.ToString();
        node["source_up_axis"] = retargetProfileAsset.SourceUpAxis.ToString();
        node["target_forward_axis"] = retargetProfileAsset.TargetForwardAxis.ToString();
        node["target_up_axis"] = retargetProfileAsset.TargetUpAxis.ToString();
        node["root_translation_scale"] = retargetProfileAsset.RootTranslationScale;

        var mappingsNode = new JArray();
        for (var mappingIndex = 0; mappingIndex < retargetProfileAsset.JointMappings.Count; mappingIndex++)
        {
            mappingsNode.Add(SaveMapping(retargetProfileAsset.JointMappings[mappingIndex]));
        }

        node["joint_mappings"] = mappingsNode;
    }

    public static void Load(RetargetProfileAsset retargetProfileAsset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(retargetProfileAsset);
        ArgumentNullException.ThrowIfNull(node);

        retargetProfileAsset.SourceSkeletonAssetId = node["source_skeleton_asset_id"]?.GetGuid() ?? Guid.Empty;
        retargetProfileAsset.TargetSkeletonAssetId = node["target_skeleton_asset_id"]?.GetGuid() ?? Guid.Empty;
        retargetProfileAsset.ReferencePoseMode = ParseEnum(node["reference_pose_mode"], RetargetReferencePoseMode.BindPose);
        retargetProfileAsset.SourceForwardAxis = ParseEnum(node["source_forward_axis"], RetargetAxis.PositiveZ);
        retargetProfileAsset.SourceUpAxis = ParseEnum(node["source_up_axis"], RetargetAxis.PositiveY);
        retargetProfileAsset.TargetForwardAxis = ParseEnum(node["target_forward_axis"], RetargetAxis.PositiveZ);
        retargetProfileAsset.TargetUpAxis = ParseEnum(node["target_up_axis"], RetargetAxis.PositiveY);
        retargetProfileAsset.RootTranslationScale = node["root_translation_scale"]?.Value<float>() ?? 1f;

        retargetProfileAsset.JointMappings.Clear();

        if (node["joint_mappings"] is not JArray mappingsNode)
        {
            return;
        }

        for (var mappingIndex = 0; mappingIndex < mappingsNode.Count; mappingIndex++)
        {
            if (mappingsNode[mappingIndex] is JObject mappingNode)
            {
                retargetProfileAsset.JointMappings.Add(LoadMapping(mappingNode));
            }
        }
    }

    private static JObject SaveMapping(RetargetJointMappingAsset mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        return new JObject
        {
            ["source_joint_name"] = mapping.SourceJointName,
            ["target_joint_name"] = mapping.TargetJointName,
            ["translation_scale"] = mapping.TranslationScale,
        };
    }

    private static RetargetJointMappingAsset LoadMapping(JObject node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return new RetargetJointMappingAsset
        {
            SourceJointName = node["source_joint_name"]?.Value<string>() ?? string.Empty,
            TargetJointName = node["target_joint_name"]?.Value<string>() ?? string.Empty,
            TranslationScale = node["translation_scale"]?.Value<float>() ?? 1f,
        };
    }

    private static TEnum ParseEnum<TEnum>(JToken? token, TEnum fallbackValue) where TEnum : struct
    {
        if (token == null)
        {
            return fallbackValue;
        }

        if (Enum.TryParse<TEnum>(token.Value<string>(), ignoreCase: true, out var parsedValue))
        {
            return parsedValue;
        }

        return fallbackValue;
    }
}