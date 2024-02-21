using CasaEngine.Core.Log;
using CasaEngine.Core.Serialization;
using CasaEngine.Engine;
using CasaEngine.Framework.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public static class AssetSaver
{
#if EDITOR

    public static void SaveAsset(string fileName, ISerializable asset)
    {
        JObject rootObject = new();
        asset.Save(rootObject);

        var fullFileName = Path.Combine(EngineEnvironment.ProjectPath, fileName);
        using StreamWriter file = File.CreateText(fullFileName);
        using JsonTextWriter writer = new JsonTextWriter(file) { Formatting = Formatting.Indented };
        rootObject.WriteTo(writer);

        Logs.WriteInfo($"Save '{fileName}'");
    }

    public static void SaveSkeletonAnimationFromRiggedModel(string fileName, RiggedModel.RiggedAnimation riggedAnimation)
    {
        JObject rootObject = new();

        rootObject.Add("name", riggedAnimation.AnimationName);

        var animationNodesNode = new JArray();
        foreach (var riggedAnimationAnimatedNode in riggedAnimation.AnimatedNodes)
        {
            JObject animationNode = new();
            animationNode.Add("name", riggedAnimationAnimatedNode.Name);
            animationNode.Add("node_name", riggedAnimationAnimatedNode.NodeRef.Name);

            animationNode.AddArray("positions", riggedAnimationAnimatedNode.Position, (v, o) => v.Save(o));
            animationNode.AddArray("position_times", riggedAnimationAnimatedNode.PositionTime);
            animationNode.AddArray("rotations", riggedAnimationAnimatedNode.Rotation, (q, o) => q.Save(o));
            animationNode.AddArray("rotation_times", riggedAnimationAnimatedNode.RotationTime);
            animationNode.AddArray("scales", riggedAnimationAnimatedNode.Scale, (v, o) => v.Save(o));
            animationNode.AddArray("scale_times", riggedAnimationAnimatedNode.ScaleTime);

            animationNodesNode.Add(animationNode);
        }

        rootObject.Add("animation_nodes", animationNodesNode);

        var fullFileName = Path.Combine(EngineEnvironment.ProjectPath, fileName);
        using StreamWriter file = File.CreateText(fullFileName);
        using JsonTextWriter writer = new JsonTextWriter(file) { Formatting = Formatting.Indented };
        rootObject.WriteTo(writer);

        Logs.WriteInfo($"Save '{fileName}'");
    }

#endif
}