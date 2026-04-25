using CasaEngine.Core.Logging;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Rendering.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices;

public enum EditorAssetSaveSource
{
    Unknown = 0,
    MaterialInspectorPanel = 1,
}

public sealed class EditorAssetSavedEventArgs : EventArgs
{
    public EditorAssetSavedEventArgs(string relativePath, string fullPath, Guid assetId, EditorAssetSaveSource saveSource)
    {
        RelativePath = relativePath;
        FullPath = fullPath;
        AssetId = assetId;
        SaveSource = saveSource;
    }

    public string RelativePath { get; }

    public string FullPath { get; }

    public Guid AssetId { get; }

    public EditorAssetSaveSource SaveSource { get; }
}

public static class EditorAssetWriterService
{
    public static event EventHandler<EditorAssetSavedEventArgs>? AssetSaved;

    public static void SaveAsset(string fileName, object asset, EditorAssetSaveSource saveSource = EditorAssetSaveSource.Unknown)
    {
        if (EditorAssetJsonSerializer.TrySerialize(asset, out var rootObject))
        {
            SaveDocument(fileName, rootObject, saveSource);
            Logs.WriteInfo($"Save '{fileName}'");
            return;
        }

        throw new InvalidOperationException($"Asset type '{asset.GetType().FullName}' is not supported by the editor writer service.");
    }

    public static void SaveDocument(string fileName, JObject rootObject, EditorAssetSaveSource saveSource = EditorAssetSaveSource.Unknown)
    {

        var fullFileName = Path.Combine(EngineEnvironment.ProjectPath, fileName);
        using (StreamWriter file = File.CreateText(fullFileName))
        using (JsonTextWriter writer = new JsonTextWriter(file) { Formatting = Formatting.Indented })
        {
            rootObject.WriteTo(writer);
        }

        AssetSaved?.Invoke(null, new EditorAssetSavedEventArgs(fileName, fullFileName, rootObject["id"]?.GetGuid() ?? Guid.Empty, saveSource));
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

        SaveDocument(fileName, rootObject);

        Logs.WriteInfo($"Save '{fileName}'");
    }
}