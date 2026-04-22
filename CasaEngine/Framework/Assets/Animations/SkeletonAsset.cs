using CasaEngine.Framework.Animations;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public sealed class SkeletonAsset : ObjectBase
{
    public List<SkeletonJointAsset> Joints { get; } = new();

    public override void Load(JObject element)
    {
        base.Load(element);
        SkeletonAssetJsonSerializer.Load(this, element);
    }
}

public sealed class SkeletonJointAsset
{
    public string Name { get; set; } = string.Empty;

    public int ParentIndex { get; set; } = -1;

    public BoneTransform LocalBindTransform { get; set; } = BoneTransform.Identity;

    public Matrix InverseBindMatrix { get; set; } = Matrix.Identity;

    public int SkinPaletteIndex { get; set; } = -1;
}

public static class SkeletonAssetJsonSerializer
{
    public static void Save(SkeletonAsset skeletonAsset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(skeletonAsset);
        ArgumentNullException.ThrowIfNull(node);

        node["id"] = skeletonAsset.Id.ToString();
        node["name"] = skeletonAsset.Name;

        var jointsNode = new JArray();
        for (var index = 0; index < skeletonAsset.Joints.Count; index++)
        {
            jointsNode.Add(SaveJoint(skeletonAsset.Joints[index]));
        }

        node["joints"] = jointsNode;
    }

    public static void Load(SkeletonAsset skeletonAsset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(skeletonAsset);
        ArgumentNullException.ThrowIfNull(node);

        skeletonAsset.Joints.Clear();

        if (node["joints"] is not JArray jointsNode)
        {
            return;
        }

        for (var index = 0; index < jointsNode.Count; index++)
        {
            if (jointsNode[index] is JObject jointNode)
            {
                skeletonAsset.Joints.Add(LoadJoint(jointNode));
            }
        }
    }

    private static JObject SaveJoint(SkeletonJointAsset joint)
    {
        ArgumentNullException.ThrowIfNull(joint);

        return new JObject
        {
            ["name"] = joint.Name,
            ["parent_index"] = joint.ParentIndex,
            ["local_bind_transform"] = AnimationAuthoringJsonSerialization.SaveBoneTransform(joint.LocalBindTransform),
            ["inverse_bind_matrix"] = AnimationAuthoringJsonSerialization.SaveMatrix(joint.InverseBindMatrix),
            ["skin_palette_index"] = joint.SkinPaletteIndex,
        };
    }

    private static SkeletonJointAsset LoadJoint(JObject node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return new SkeletonJointAsset
        {
            Name = node["name"]?.Value<string>() ?? string.Empty,
            ParentIndex = node["parent_index"]?.Value<int>() ?? -1,
            LocalBindTransform = AnimationAuthoringJsonSerialization.LoadBoneTransform(node["local_bind_transform"]),
            InverseBindMatrix = AnimationAuthoringJsonSerialization.LoadMatrix(node["inverse_bind_matrix"]),
            SkinPaletteIndex = node["skin_palette_index"]?.Value<int>() ?? -1,
        };
    }
}